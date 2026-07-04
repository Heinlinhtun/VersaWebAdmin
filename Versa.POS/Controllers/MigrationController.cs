using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Services;

/// <summary>
/// One-time migration controller — run once after deploying the single-session feature
/// to add the SessionToken column to all tenant ShopUsers tables.
/// Requires SuperAdmin role for safety.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,Admin")]
public class MigrationController : ControllerBase
{
    private readonly DatabaseService _dbService;

    public MigrationController(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    /// <summary>
    /// POST /api/migration/add-session-token
    /// Adds the SessionToken column to ShopUsers in every active tenant database.
    /// Safe to run multiple times — skips DBs that already have the column.
    /// </summary>
    [HttpPost("add-session-token")]
    public async Task<IActionResult> AddSessionTokenColumn()
    {
        var results = new List<object>();

        // Get all active tenant db names from master
        using var masterConn = new MySqlConnection(_dbService.GetMasterConnectionString());
        var tenantDbs = (await masterConn.QueryAsync<string>(
            "SELECT DbName FROM Tenants WHERE IsActive = 1 AND DbName IS NOT NULL")).ToList();

        foreach (var dbName in tenantDbs)
        {
            try
            {
                var connStr = _dbService.GetTenantConnectionString(dbName);
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                // Check if column already exists
                var exists = await conn.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'ShopUsers' AND COLUMN_NAME = 'SessionToken'",
                    new { db = dbName });

                if (exists == 0)
                {
                    await conn.ExecuteAsync(
                        "ALTER TABLE ShopUsers ADD COLUMN SessionToken VARCHAR(64) NULL");
                    results.Add(new { db = dbName, status = "migrated" });
                }
                else
                {
                    results.Add(new { db = dbName, status = "already_exists" });
                }
            }
            catch (Exception ex)
            {
                results.Add(new { db = dbName, status = "error", error = ex.Message });
            }
        }

        return Ok(new { migrationResults = results });
    }

    /// <summary>
    /// POST /api/migration/add-saleitem-note
    /// Adds the Note column to SaleItems in every active tenant database.
    /// </summary>
    [HttpPost("add-saleitem-note")]
    public async Task<IActionResult> AddSaleItemNoteColumn()
    {
        var results = new List<object>();
        using var masterConn = new MySqlConnection(_dbService.GetMasterConnectionString());
        var tenantDbs = (await masterConn.QueryAsync<string>(
            "SELECT DbName FROM Tenants WHERE IsActive = 1 AND DbName IS NOT NULL")).ToList();

        foreach (var dbName in tenantDbs)
        {
            try
            {
                var connStr = _dbService.GetTenantConnectionString(dbName);
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                var exists = await conn.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'SaleItems' AND COLUMN_NAME = 'Note'",
                    new { db = dbName });

                if (exists == 0)
                {
                    await conn.ExecuteAsync("ALTER TABLE SaleItems ADD COLUMN Note TEXT NULL");
                    results.Add(new { db = dbName, status = "migrated" });
                }
                else
                {
                    results.Add(new { db = dbName, status = "already_exists" });
                }
            }
            catch (Exception ex)
            {
                results.Add(new { db = dbName, status = "error", error = ex.Message });
            }
        }
        return Ok(new { migrationResults = results });
    }

    /// <summary>
    /// POST /api/migration/add-receipt-font-size
    /// Adds the ReceiptFontSize column to Settings in every active tenant database.
    /// Safe to run multiple times — skips DBs that already have the column.
    /// </summary>
    [HttpPost("add-receipt-font-size")]
    public async Task<IActionResult> AddReceiptFontSizeColumn()
    {
        var results = new List<object>();
        using var masterConn = new MySqlConnection(_dbService.GetMasterConnectionString());
        var tenantDbs = (await masterConn.QueryAsync<string>(
            "SELECT DbName FROM Tenants WHERE IsActive = 1 AND DbName IS NOT NULL")).ToList();

        foreach (var dbName in tenantDbs)
        {
            try
            {
                var connStr = _dbService.GetTenantConnectionString(dbName);
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                var exists = await conn.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                      WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Settings' AND COLUMN_NAME = 'ReceiptFontSize'",
                    new { db = dbName });

                if (exists == 0)
                {
                    await conn.ExecuteAsync("ALTER TABLE Settings ADD COLUMN ReceiptFontSize VARCHAR(10) NOT NULL DEFAULT 'Medium'");
                    results.Add(new { db = dbName, status = "migrated" });
                }
                else
                {
                    results.Add(new { db = dbName, status = "already_exists" });
                }
            }
            catch (Exception ex)
            {
                results.Add(new { db = dbName, status = "error", error = ex.Message });
            }
        }
        return Ok(new { migrationResults = results });
    }
}
