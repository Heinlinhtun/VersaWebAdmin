// Services/DatabaseService.cs
using Dapper;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Data;
using Versa.Core.Models;
namespace Versa.Core.Services;

public class DatabaseService
{
    private readonly IConfiguration _config;
    private readonly string _masterConn;

    public DatabaseService(IConfiguration configuration)
    {
        _config = configuration;
        _masterConn = _config.GetConnectionString("MasterDbConnection");
    }
    public async Task<ShopUser?> GetShopUser(string dbName, string username)
    {
        // Tenant (ဆိုင်) ရဲ့ Connection String ကို ယူခြင်း
        var connectionString = GetTenantConnectionString(dbName);

        using (var conn = new MySqlConnection(connectionString))
        {
            // Database Schema အရ UserId, Username, PasswordHash, Role များကို ဆွဲထုတ်ခြင်း
            string sql = "SELECT UserId, Username, PasswordHash, Role, BranchId, IsActive FROM ShopUsers WHERE Username = @uname AND IsActive = 1";
            return await conn.QueryFirstOrDefaultAsync<ShopUser>(sql, new { uname = username });
        }
    }

    /// <summary>Overwrites the session token for a user, invalidating any prior active sessions.</summary>
    public async Task UpdateSessionTokenAsync(string dbName, int userId, string sessionToken)
    {
        var connectionString = GetTenantConnectionString(dbName);
        using var conn = new MySqlConnection(connectionString);
        string sql = "UPDATE ShopUsers SET SessionToken = @token WHERE UserId = @uid";
        await conn.ExecuteAsync(sql, new { token = sessionToken, uid = userId });
    }

    /// <summary>Returns the currently stored session token for a user (used by middleware to validate requests).</summary>
    public async Task<string?> GetSessionTokenAsync(string dbName, int userId)
    {
        var connectionString = GetTenantConnectionString(dbName);
        using var conn = new MySqlConnection(connectionString);
        string sql = "SELECT SessionToken FROM ShopUsers WHERE UserId = @uid AND IsActive = 1";
        return await conn.QueryFirstOrDefaultAsync<string?>(sql, new { uid = userId });
    }


    public async Task<string> GetBranchNameAsync(string dbName, int branchId)
    {
        var connectionString = GetTenantConnectionString(dbName);
        using (var conn = new MySqlConnection(connectionString))
        {
            string sql = "SELECT BranchName FROM Branches WHERE BranchId = @bid AND IsActive = 1";
            var branchName = await conn.QueryFirstOrDefaultAsync<string>(sql, new { bid = branchId });
            return !string.IsNullOrEmpty(branchName) ? branchName : "Main Branch";
        }
    }

    public string GetMasterConnectionString() => _masterConn;

    // Tenant တစ်ခုချင်းစီအတွက် Connection String ထုတ်ပေးရန်
    public string GetTenantConnectionString(string dbName)
    {
        if (string.IsNullOrEmpty(dbName)) return "";

        // Master Connection string ထဲက database name နေရာမှာ tenant db name နဲ့ အစားထိုးခြင်း
        var builder = new MySqlConnectionStringBuilder(_masterConn);
        builder.Database = dbName;
        return builder.ConnectionString;
    }

    // DatabaseService.cs (Versa.Core သို့မဟုတ် POS)
    public async Task<string> GetDbNameByShopId(string shopLoginId)
    {
        // Master DB Connection ကို သုံးပြီး ဆိုင်ရဲ့ DB Name ကို ရှာခြင်း
        using var conn = new MySqlConnection(_masterConn);
        string sql = "SELECT DbName FROM Shops WHERE ShopLoginId = @sid AND IsActive = 1";
        return await conn.QueryFirstOrDefaultAsync<string>(sql, new { sid = shopLoginId });
    }
    // Tenants Table မှ အချက်အလက်ယူရန် (Master DB)
    public async Task<Tenant?> GetTenantByLoginId(string shopLoginId)
    {
        using var conn = new MySqlConnection(_masterConn);
        var allowBranchExists = await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'Tenants'
              AND COLUMN_NAME = 'AllowBranch'");
        if (allowBranchExists == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE Tenants ADD COLUMN AllowBranch TINYINT(1) NOT NULL DEFAULT 1");
        }
        // မိတ်ဆွေရဲ့ Model ထဲက Column နာမည်တွေအတိုင်း SELECT လုပ်ခြင်း
        string sql = @"SELECT TenantId, ShopName, ShopLoginId, DbName, 
                          SubscriptionPlan, SubscriptionStartDate, 
                          SubscriptionEndDate, IsActive, MaxUsers, AllowBranch,
                          ShopType, Address, ContactNumber, CreatedAt 
                   FROM Tenants WHERE ShopLoginId = @sid";
        return await conn.QueryFirstOrDefaultAsync<Tenant>(sql, new { sid = shopLoginId });
    }

    public async Task<int> GetMaxUsersByDbName(string dbName)
    {
        using var conn = new MySqlConnection(_masterConn);
        string sql = "SELECT MaxUsers FROM Tenants WHERE DbName = @dbName";
        return await conn.QueryFirstOrDefaultAsync<int>(sql, new { dbName });
    }
    public async Task CloneDatabaseStructure(string sourceDb, string targetDb)
    {
        using var conn = new MySqlConnection(_masterConn);
        await conn.OpenAsync();

        var tables = (await conn.QueryAsync<string>(
            @"SELECT TABLE_NAME
              FROM INFORMATION_SCHEMA.TABLES
              WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'
              ORDER BY TABLE_NAME",
            new { schema = sourceDb })).ToList();

        conn.ChangeDatabase(targetDb);
        await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0;");
        try
        {
            foreach (var table in tables)
            {
                string createSql;
                await using (var showCreateCmd = new MySqlCommand(
                    $"SHOW CREATE TABLE `{EscapeIdentifier(sourceDb)}`.`{EscapeIdentifier(table)}`;", conn))
                await using (var reader = await showCreateCmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        continue;
                    }

                    createSql = reader.GetString(1);
                }

                await new MySqlCommand(createSql, conn).ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 1;");
        }

        var views = await conn.QueryAsync<string>(
            @"SELECT TABLE_NAME
              FROM INFORMATION_SCHEMA.TABLES
              WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'VIEW'
              ORDER BY TABLE_NAME",
            new { schema = sourceDb });

        foreach (var view in views)
        {
            using var showViewCmd = new MySqlCommand(
                $"SHOW CREATE VIEW `{EscapeIdentifier(sourceDb)}`.`{EscapeIdentifier(view)}`;",
                conn);
            using var reader = await showViewCmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                continue;
            }

            var createViewSql = reader.GetString("Create View")
                .Replace($"`{sourceDb.Replace("`", "``")}`.`{view.Replace("`", "``")}`", $"`{targetDb.Replace("`", "``")}`.`{view.Replace("`", "``")}`")
                .Replace($"`{sourceDb.Replace("`", "``")}`.", $"`{targetDb.Replace("`", "``")}`.");

            await reader.CloseAsync();
            await new MySqlCommand(createViewSql, conn).ExecuteNonQueryAsync();
        }
    }

    public async Task CloneDatabaseData(string sourceDb, string targetDb, ISet<string>? excludedTables = null)
    {
        using var conn = new MySqlConnection(_masterConn);
        await conn.OpenAsync();

        await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 0;");
        try
        {
            var tables = await conn.QueryAsync<string>(
                @"SELECT TABLE_NAME
                  FROM INFORMATION_SCHEMA.TABLES
                  WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'
                  ORDER BY TABLE_NAME",
                new { schema = sourceDb });

            foreach (var table in tables)
            {
                if (excludedTables != null && excludedTables.Contains(table))
                {
                    continue;
                }

                await new MySqlCommand(
                    $"INSERT INTO `{EscapeIdentifier(targetDb)}`.`{EscapeIdentifier(table)}` SELECT * FROM `{EscapeIdentifier(sourceDb)}`.`{EscapeIdentifier(table)}`;",
                    conn).ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await conn.ExecuteAsync("SET FOREIGN_KEY_CHECKS = 1;");
        }
    }

    public async Task<bool> DatabaseExists(string dbName)
    {
        using var conn = new MySqlConnection(_masterConn);
        await conn.OpenAsync();
        const string sql = @"SELECT COUNT(*)
                             FROM INFORMATION_SCHEMA.SCHEMATA
                             WHERE SCHEMA_NAME = @dbName";
        var count = await conn.ExecuteScalarAsync<long>(sql, new { dbName });
        return count > 0;
    }

    private static string EscapeIdentifier(string identifier)
        => identifier.Replace("`", "``");

    // Units စာရင်းယူရန်
    public async Task<IEnumerable<Unit>> GetUnits(string dbName)
    {
        using var conn = new MySqlConnection(GetTenantConnectionString(dbName));
        return await conn.QueryAsync<Unit>("SELECT * FROM Units");
    }

    // Unit အသစ်ထည့်ရန်
    public async Task<int> AddUnit(string dbName, Unit unit)
    {
        using var conn = new MySqlConnection(GetTenantConnectionString(dbName));
        string sql = "INSERT INTO Units (UnitName, ShortName) VALUES (@UnitName, @ShortName); SELECT LAST_INSERT_ID();";
        return await conn.ExecuteScalarAsync<int>(sql, unit);
    }

    // --- အရေးကြီးသောအပိုင်း: Unit အလိုက် ဈေးနှုန်းမျိုးစုံ သတ်မှတ်ခြင်း ---
    public async Task SetProductUnitPrice(string dbName, ProductPrice price)
    {
        using var conn = new MySqlConnection(GetTenantConnectionString(dbName));
        string sql = @"INSERT INTO ProductPrices (ProductId, UnitId, PriceName, PriceValue, MinQuantity) 
                   VALUES (@ProductId, @UnitId, @PriceName, @PriceValue, @MinQuantity)
                   ON DUPLICATE KEY UPDATE PriceValue = @PriceValue, MinQuantity = @MinQuantity";
        await conn.ExecuteAsync(sql, price);
    }
    public async Task ExecuteSqlScriptAsync(string dbName, string script)
    {
        var connectionString = GetMasterConnectionString();
        using (var conn = new MySqlConnection(connectionString))
        {
            await conn.OpenAsync();
            
            // We replace {DatabaseName} placeholder if present
            var finalScript = script.Replace("{DatabaseName}", dbName);
            
            // Split by semicolon and execute each part
            // Note: This is a simple splitter and might fail on complex scripts with semicolons in strings.
            // For the provided script, it should work fine.
            var commands = finalScript.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var cmdText in commands)
            {
                var trimmedText = cmdText.Trim();
                if (string.IsNullOrEmpty(trimmedText)) continue;
                
                using (var cmd = new MySqlCommand(trimmedText, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }

}
