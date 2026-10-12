using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using VersaWebAdmin.Models;
using VersaWebAdmin.Services;
using Versa.Core.Models;
using Versa.Core.Services; // DatabaseService ရှိရာ Namespace

namespace VersaWebAdmin.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly DatabaseService _dbService;
    private readonly EncryptionService _encryptionService;
    private readonly ITenantBackupService _tenantBackupService;
    private readonly IWebHostEnvironment _env;
    private readonly ISystemHealthService _systemHealthService;

    public AdminController(
        DatabaseService dbService,
        EncryptionService encryptionService,
        ITenantBackupService tenantBackupService,
        IWebHostEnvironment env,
        ISystemHealthService systemHealthService)
    {
        _dbService = dbService;
        _encryptionService = encryptionService;
        _tenantBackupService = tenantBackupService;
        _env = env;
        _systemHealthService = systemHealthService;
    }

    public async Task<IActionResult> SystemHealth(CancellationToken cancellationToken) =>
        View(await _systemHealthService.GetSnapshotAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> SystemHealthSnapshot(CancellationToken cancellationToken) =>
        Json(await _systemHealthService.GetSnapshotAsync(cancellationToken));

    // ဆိုင်အသစ်ဖွင့်ပေးမယ့် Form View
    public IActionResult Index() => View(new RegisterShopViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterShop(RegisterShopViewModel model, int PlanMonths, int MaxUsers)
    {
        if (!ModelState.IsValid) return View("Index", model);

        string safeLoginId = model.ShopLoginId.ToLowerInvariant().Trim();
        bool isRestaurant = IsRestaurant(model.ShopType);
        if (!IsValidShopLoginId(safeLoginId, isRestaurant))
        {
            ModelState.AddModelError(nameof(model.ShopLoginId),
                isRestaurant
                    ? "Restaurant Shop ID must be 1-45 characters using only letters, numbers, and underscore."
                    : "Shop ID must use only letters, numbers, and underscore.");
            return View("Index", model);
        }

        string newDbName = BuildTenantDatabaseName(safeLoginId, isRestaurant);
        string masterConn = _dbService.GetMasterConnectionString();
        bool databaseCreated = false;

        try
        {
            using (var conn = new MySqlConnection(masterConn))
            {
                await conn.OpenAsync();

                using (var duplicateCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM Tenants WHERE ShopLoginId = @loginId OR DbName = @dbName", conn))
                {
                    duplicateCmd.Parameters.AddWithValue("@loginId", safeLoginId);
                    duplicateCmd.Parameters.AddWithValue("@dbName", newDbName);
                    if (Convert.ToInt32(await duplicateCmd.ExecuteScalarAsync()) > 0)
                    {
                        ModelState.AddModelError(nameof(model.ShopLoginId), "This Shop ID is already registered.");
                        return View("Index", model);
                    }
                }

                if (await _dbService.DatabaseExists(newDbName))
                {
                    ModelState.AddModelError(nameof(model.ShopLoginId), $"Database {newDbName} already exists.");
                    return View("Index", model);
                }

                // 1. Database အသစ်ဆောက်ခြင်း
                using (var createDbCmd = new MySqlCommand(
                    $"CREATE DATABASE `{newDbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", conn))
                {
                    await createDbCmd.ExecuteNonQueryAsync();
                    databaseCreated = true;
                }

                // 2. Structure ကူးယူခြင်း သို့မဟုတ် Template Run ခြင်း
                if (isRestaurant)
                {
                    await _dbService.CloneDatabaseStructure("restaurant_template", newDbName);
                    await _dbService.CloneDatabaseData("restaurant_template", newDbName);

                    // 3. Admin User သတ်မှတ်ခြင်း (Restaurant schema use Staff table)
                    await SetupRestaurantAdmin(newDbName, model.AdminUsername, model.AdminPassword);
                    await UpdateRestaurantSettingsAsync(
                        newDbName,
                        model.ShopName,
                        model.Address,
                        model.ContactNumber);
                }
                else
                {
                    await _dbService.CloneDatabaseStructure("pos_template", newDbName);

                    // 3. Admin User သတ်မှတ်ခြင်း (POS schema use ShopUsers table)
                    await SetupDefaultAdmin(newDbName, model.AdminUsername, model.AdminPassword);
                }

                // 4. POS_Master.Tenants ထဲတွင် မှတ်ပုံတင်ခြင်း
                DateTime startDate = GetMyanmarNow();
                DateTime endDate = startDate.AddMonths(PlanMonths);

                // Plan အမည်ကို လအလိုက် သတ်မှတ်ခြင်း
                string planName = PlanMonths switch
                {
                    1 => "Trial",
                    12 => "1 Year Pro",
                    24 => "2 Years Pro",
                    36 => "3 Years Business",
                    60 => "5 Years Enterprise",
                    _ => $"{PlanMonths} Months Plan"
                };

                string insertSql = @"INSERT INTO Tenants 
                (ShopName, ShopLoginId, DbName, SubscriptionPlan, SubscriptionStartDate, SubscriptionEndDate, IsActive, MaxUsers, AllowBranch, ShopType, Address, ContactNumber)
                VALUES (@name, @loginId, @db, @plan, @start, @end, 1, @maxUsers, @allowBranch, @type, @addr, @contact)";

                using (var insertMaster = new MySqlCommand(insertSql, conn))
                {
                    insertMaster.Parameters.AddWithValue("@name", model.ShopName);
                    insertMaster.Parameters.AddWithValue("@loginId", safeLoginId);
                    insertMaster.Parameters.AddWithValue("@db", newDbName);
                    insertMaster.Parameters.AddWithValue("@plan", planName);
                    insertMaster.Parameters.AddWithValue("@start", startDate);
                    insertMaster.Parameters.AddWithValue("@end", endDate);
                    insertMaster.Parameters.AddWithValue("@maxUsers", MaxUsers);
                    insertMaster.Parameters.AddWithValue("@allowBranch", !isRestaurant && model.AllowBranch);
                    insertMaster.Parameters.AddWithValue("@type", model.ShopType ?? "");
                    insertMaster.Parameters.AddWithValue("@addr", model.Address ?? "");
                    insertMaster.Parameters.AddWithValue("@contact", model.ContactNumber ?? "");
                    await insertMaster.ExecuteNonQueryAsync();
                }
            }
            return RedirectToAction("ShopList");
        }
        catch (Exception ex)
        {
            if (databaseCreated)
            {
                await TryDropDatabaseAsync(newDbName);
            }

            ModelState.AddModelError("", "Error: " + ex.Message);
            return View("Index", model);
        }
    }

    // ဆိုင်စာရင်း ပြသရန် (ဒီကနေ ဆိုင်ရှင်ကို အချက်အလက် ပြန်ပေးလို့ရမယ်)
    public async Task<IActionResult> ShopList()
    {
        var shops = new List<Tenant>();
        string masterConn = _dbService.GetMasterConnectionString();

        using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync();
            // Subscription အချက်အလက်တွေပါ ဆွဲထုတ်ပါမယ်
            string sql = @"SELECT TenantId, ShopName, ShopLoginId, DbName, 
                              SubscriptionPlan, SubscriptionStartDate, 
                              SubscriptionEndDate, IsActive, MaxUsers, AllowBranch,
                              ShopType, Address, ContactNumber, CreatedAt 
                       FROM Tenants ORDER BY CreatedAt DESC";

            using var cmd = new MySqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                shops.Add(new Tenant
                {
                    TenantId = reader.GetInt32("TenantId"),
                    ShopName = reader.IsDBNull(reader.GetOrdinal("ShopName")) ? "" : reader.GetString("ShopName"),
                    ShopLoginId = reader.IsDBNull(reader.GetOrdinal("ShopLoginId")) ? "" : reader.GetString("ShopLoginId"),
                    DbName = reader.IsDBNull(reader.GetOrdinal("DbName")) ? "" : reader.GetString("DbName"),

                    // SubscriptionPlan NULL စစ်ခြင်း
                    SubscriptionPlan = reader.IsDBNull(reader.GetOrdinal("SubscriptionPlan")) ? "Basic" : reader.GetString("SubscriptionPlan"),

                    // DateTime NULL စစ်ခြင်း (NULL ဖြစ်နေလျှင် ယနေ့ရက်စွဲကို Default ထားပါမည်)
                    SubscriptionStartDate = reader.IsDBNull(reader.GetOrdinal("SubscriptionStartDate"))
                             ? DateTime.Now : reader.GetDateTime("SubscriptionStartDate"),

                    SubscriptionEndDate = reader.IsDBNull(reader.GetOrdinal("SubscriptionEndDate"))
                           ? DateTime.Now.AddMonths(1) : reader.GetDateTime("SubscriptionEndDate"),

                    // IsActive NULL စစ်ခြင်း
                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? true : reader.GetBoolean("IsActive"),

                    MaxUsers = reader.IsDBNull(reader.GetOrdinal("MaxUsers")) ? 5 : reader.GetInt32("MaxUsers"),
                    AllowBranch = reader.IsDBNull(reader.GetOrdinal("AllowBranch")) || reader.GetBoolean("AllowBranch"),
                    ShopType = reader.IsDBNull(reader.GetOrdinal("ShopType")) ? "" : reader.GetString("ShopType"),
                    Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? "" : reader.GetString("Address"),
                    ContactNumber = reader.IsDBNull(reader.GetOrdinal("ContactNumber")) ? "" : reader.GetString("ContactNumber"),

                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("CreatedAt"))
                 ? DateTime.Now : reader.GetDateTime("CreatedAt")
                });
            }
        }
        return View(shops);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RunBackupNow([FromServices] IManualBackupQueue backupQueue)
    {
        if (!backupQueue.TryQueueBackup())
        {
            TempData["BackupError"] = "A manual backup is already queued or running.";
            return RedirectToAction("ShopList");
        }

        TempData["BackupSuccess"] = "Backup started in the background. A Telegram alert will be sent when it finishes.";
        return RedirectToAction("ShopList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BackupShop(BackupShopRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["BackupError"] = "Admin password is required.";
            return RedirectToAction("ShopList");
        }

        string masterConn = _dbService.GetMasterConnectionString();
        await using var conn = new MySqlConnection(masterConn);
        await conn.OpenAsync(cancellationToken);

        if (!await VerifyCurrentAdminPasswordAsync(conn, model.AdminPassword, "BackupError", cancellationToken))
        {
            return RedirectToAction("ShopList");
        }

        var tenant = await GetTenantForActionAsync(conn, model.Id, cancellationToken);
        if (tenant == null)
        {
            TempData["BackupError"] = "Shop not found.";
            return RedirectToAction("ShopList");
        }

        var result = await _tenantBackupService.RunBackupForTenantAsync(
            tenant.DbName,
            $"Admin manual backup for {tenant.ShopName}",
            cancellationToken);

        TempData[result.IsSuccess ? "BackupSuccess" : "BackupError"] =
            $"{tenant.ShopName}: {result.Message}";

        return RedirectToAction("ShopList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadBackup(BackupShopRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["BackupError"] = "Admin password is required.";
            return RedirectToAction("ShopList");
        }

        string masterConn = _dbService.GetMasterConnectionString();
        await using var conn = new MySqlConnection(masterConn);
        await conn.OpenAsync(cancellationToken);

        if (!await VerifyCurrentAdminPasswordAsync(conn, model.AdminPassword, "BackupError", cancellationToken))
        {
            return RedirectToAction("ShopList");
        }

        var tenant = await GetTenantForActionAsync(conn, model.Id, cancellationToken);
        if (tenant == null)
        {
            TempData["BackupError"] = "Shop not found.";
            return RedirectToAction("ShopList");
        }

        var backupFilePath = await _tenantBackupService.CreateTenantBackupArchiveAsync(tenant.DbName, cancellationToken);
        var downloadName = $"{tenant.DbName}_{DateTime.Now:yyyyMMdd_HHmmss}.sql.gz";
        HttpContext.Response.OnCompleted(() =>
        {
            try
            {
                if (System.IO.File.Exists(backupFilePath))
                {
                    System.IO.File.Delete(backupFilePath);
                }
            }
            catch
            {
            }

            return Task.CompletedTask;
        });
        return PhysicalFile(backupFilePath, "application/gzip", downloadName, enableRangeProcessing: false);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateShop(DuplicateShopRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["DuplicateError"] = "All duplicate fields are required.";
            return RedirectToAction("ShopList");
        }

        string masterConn = _dbService.GetMasterConnectionString();
        await using var conn = new MySqlConnection(masterConn);
        await conn.OpenAsync(cancellationToken);

        if (!await VerifyCurrentAdminPasswordAsync(conn, model.AdminPassword, "DuplicateError", cancellationToken))
        {
            return RedirectToAction("ShopList");
        }

        var sourceTenant = await GetTenantForActionAsync(conn, model.Id, cancellationToken);
        if (sourceTenant == null)
        {
            TempData["DuplicateError"] = "Source shop not found.";
            return RedirectToAction("ShopList");
        }

        var safeLoginId = model.NewShopLoginId.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeLoginId))
        {
            TempData["DuplicateError"] = "New shop login ID is required.";
            return RedirectToAction("ShopList");
        }

        var sourceIsRestaurant = IsRestaurant(sourceTenant.ShopType);
        if (!IsValidShopLoginId(safeLoginId, sourceIsRestaurant))
        {
            TempData["DuplicateError"] = sourceIsRestaurant
                ? "Restaurant Shop ID must be 1-45 characters using only letters, numbers, and underscore."
                : "Shop ID must use only letters, numbers, and underscore.";
            return RedirectToAction("ShopList");
        }

        var newDbName = BuildTenantDatabaseName(safeLoginId, sourceIsRestaurant);
        if (await _dbService.DatabaseExists(newDbName))
        {
            TempData["DuplicateError"] = $"Database {newDbName} already exists.";
            return RedirectToAction("ShopList");
        }

        var newShopName = model.NewShopName.Trim();
        var newAdminUsername = model.NewAdminUsername.Trim();
        var newAdminPassword = model.NewAdminPassword.Trim();
        if (string.IsNullOrWhiteSpace(newShopName) ||
            string.IsNullOrWhiteSpace(newAdminUsername) ||
            string.IsNullOrWhiteSpace(newAdminPassword))
        {
            TempData["DuplicateError"] = "New shop name, admin username, and admin password are required.";
            return RedirectToAction("ShopList");
        }

        var connString = _dbService.GetMasterConnectionString();
        await using var dbConn = new MySqlConnection(connString);
        await dbConn.OpenAsync(cancellationToken);

        bool databaseCreated = false;
        try
        {
            await new MySqlCommand($"CREATE DATABASE `{newDbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", dbConn)
                .ExecuteNonQueryAsync(cancellationToken);
            databaseCreated = true;

            await _dbService.CloneDatabaseStructure(sourceTenant.DbName, newDbName);
            await _dbService.CloneDatabaseData(sourceTenant.DbName, newDbName);

            if (string.Equals(sourceTenant.ShopType, "Restaurant", StringComparison.OrdinalIgnoreCase))
            {
                await RewriteRestaurantAdminAsync(newDbName, newAdminUsername, newAdminPassword);
                await UpdateRestaurantSettingsAsync(
                    newDbName,
                    newShopName,
                    sourceTenant.Address,
                    sourceTenant.ContactNumber);
            }
            else
            {
                await RewriteDefaultAdminAsync(newDbName, newAdminUsername, newAdminPassword);
            }

            var now = GetMyanmarNow();
            var planMonths = Math.Max(1, (sourceTenant.SubscriptionEndDate - sourceTenant.SubscriptionStartDate).Days / 30);
            var newEndDate = now.AddMonths(planMonths);

            var insertSql = @"INSERT INTO Tenants
                (ShopName, ShopLoginId, DbName, SubscriptionPlan, SubscriptionStartDate, SubscriptionEndDate, IsActive, MaxUsers, AllowBranch, ShopType, Address, ContactNumber)
                VALUES (@name, @loginId, @db, @plan, @start, @end, 1, @maxUsers, @allowBranch, @type, @addr, @contact)";

            await using (var insertCmd = new MySqlCommand(insertSql, dbConn))
            {
                insertCmd.Parameters.AddWithValue("@name", newShopName);
                insertCmd.Parameters.AddWithValue("@loginId", safeLoginId);
                insertCmd.Parameters.AddWithValue("@db", newDbName);
                insertCmd.Parameters.AddWithValue("@plan", sourceTenant.SubscriptionPlan);
                insertCmd.Parameters.AddWithValue("@start", now);
                insertCmd.Parameters.AddWithValue("@end", newEndDate);
                insertCmd.Parameters.AddWithValue("@maxUsers", sourceTenant.MaxUsers);
                insertCmd.Parameters.AddWithValue("@allowBranch", !sourceIsRestaurant && sourceTenant.AllowBranch);
                insertCmd.Parameters.AddWithValue("@type", sourceTenant.ShopType ?? "");
                insertCmd.Parameters.AddWithValue("@addr", sourceTenant.Address ?? "");
                insertCmd.Parameters.AddWithValue("@contact", sourceTenant.ContactNumber ?? "");
                await insertCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            TempData["DuplicateSuccess"] = $"{sourceTenant.ShopName} was duplicated to {newShopName}.";
        }
        catch (Exception ex)
        {
            if (databaseCreated)
            {
                await TryDropDatabaseAsync(newDbName);
            }

            TempData["DuplicateError"] = $"Duplicate failed: {ex.Message}";
        }

        return RedirectToAction("ShopList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetShop(ResetShopRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ResetError"] = "Admin password, shop admin username, and new shop admin password are required.";
            return RedirectToAction("ShopList");
        }

        string masterConn = _dbService.GetMasterConnectionString();
        Tenant? tenant;

        await using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync(cancellationToken);

            if (!await VerifyCurrentAdminPasswordAsync(conn, model.AdminPassword, "ResetError", cancellationToken))
            {
                return RedirectToAction("ShopList");
            }

            tenant = await GetTenantForActionAsync(conn, model.Id, cancellationToken);
        }

        if (tenant == null)
        {
            TempData["ResetError"] = "Shop not found.";
            return RedirectToAction("ShopList");
        }

        var backupResult = await _tenantBackupService.RunBackupForTenantAsync(
            tenant.DbName,
            $"Before reset backup for {tenant.ShopName}",
            cancellationToken);

        if (!backupResult.IsSuccess)
        {
            TempData["ResetError"] = $"Reset stopped because backup failed for {tenant.ShopName}: {backupResult.Message}";
            return RedirectToAction("ShopList");
        }

        try
        {
            await ResetTenantDatabaseAsync(
                tenant,
                model.ShopAdminUsername.Trim(),
                model.NewShopAdminPassword,
                cancellationToken);

            TempData["ResetSuccess"] = $"{tenant.ShopName} was reset successfully. A backup was created first.";
        }
        catch (Exception ex)
        {
            TempData["ResetError"] = $"Reset failed for {tenant.ShopName}: {ex.Message}";
        }

        return RedirectToAction("ShopList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend([FromRoute] int id)
    {
        string masterConn = _dbService.GetMasterConnectionString();
        using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync();
            // Toggle the IsActive status
            string sql = "UPDATE Tenants SET IsActive = NOT IsActive WHERE TenantId = @id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        return RedirectToAction("ShopList");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShop(DeleteShopRequest model)
    {
        if (!ModelState.IsValid)
        {
            TempData["DeleteError"] = "Admin password is required.";
            return RedirectToAction("ShopList");
        }

        string masterConn = _dbService.GetMasterConnectionString();

        using var conn = new MySqlConnection(masterConn);
        await conn.OpenAsync();

        // 1. Verify current logged-in admin password
        if (!await VerifyCurrentAdminPasswordAsync(conn, model.AdminPassword, "DeleteError", HttpContext.RequestAborted))
        {
            return RedirectToAction("ShopList");
        }

        // 2. Get DB Name
        string? dbName = null;
        using (var getDbCmd = new MySqlCommand(
            "SELECT DbName FROM Tenants WHERE TenantId = @id LIMIT 1", conn))
        {
            getDbCmd.Parameters.AddWithValue("@id", model.Id);
            dbName = (string?)await getDbCmd.ExecuteScalarAsync();
        }

        if (string.IsNullOrWhiteSpace(dbName))
        {
            TempData["DeleteError"] = "Shop not found.";
            return RedirectToAction("ShopList");
        }

        // Optional: use transaction for tenant delete
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            // 3. Drop DB
            using (var dropDbCmd = new MySqlCommand($"DROP DATABASE IF EXISTS `{dbName}`;", conn, (MySqlTransaction)tx))
            {
                await dropDbCmd.ExecuteNonQueryAsync();
            }

            // 4. Delete tenant record
            using (var deleteCmd = new MySqlCommand(
                "DELETE FROM Tenants WHERE TenantId = @id", conn, (MySqlTransaction)tx))
            {
                deleteCmd.Parameters.AddWithValue("@id", model.Id);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            TempData["DeleteSuccess"] = "Shop deleted successfully.";
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            TempData["DeleteError"] = $"Delete failed: {ex.Message}";
        }

        return RedirectToAction("ShopList");
    }

    public async Task<IActionResult> Renew(int id)
    {
        string masterConn = _dbService.GetMasterConnectionString();
        Tenant shop = null;
        using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync();
            string sql = "SELECT * FROM Tenants WHERE TenantId = @id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                shop = new Tenant
                {
                    TenantId = reader.GetInt32("TenantId"),
                    ShopName = reader.IsDBNull(reader.GetOrdinal("ShopName")) ? "" : reader.GetString("ShopName"),
                    SubscriptionEndDate = reader.GetDateTime("SubscriptionEndDate")
                };
            }
        }
        if (shop == null) return NotFound();
        return View(shop);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessRenew(int TenantId, int PlanMonths)
    {
        string masterConn = _dbService.GetMasterConnectionString();
        using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync();

            string planName = PlanMonths switch
            {
                1 => "Trial",
                12 => "1 Year Pro",
                24 => "2 Years Pro",
                36 => "3 Years Business",
                60 => "5 Years Enterprise",
                _ => $"{PlanMonths} Months Plan"
            };

            string sql = @"UPDATE Tenants SET 
                           SubscriptionEndDate = DATE_ADD(SubscriptionEndDate, INTERVAL @months MONTH),
                           SubscriptionPlan = @plan 
                           WHERE TenantId = @id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@months", PlanMonths);
            cmd.Parameters.AddWithValue("@plan", planName);
            cmd.Parameters.AddWithValue("@id", TenantId);
            await cmd.ExecuteNonQueryAsync();
        }
        return RedirectToAction("ShopList");
    }

    public async Task<IActionResult> Edit(int id)
    {
        string masterConn = _dbService.GetMasterConnectionString();
        EditShopViewModel shop = null;
        using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync();
            string sql = "SELECT * FROM Tenants WHERE TenantId = @id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                shop = new EditShopViewModel
                {
                    TenantId = reader.GetInt32("TenantId"),
                    ShopName = reader.IsDBNull(reader.GetOrdinal("ShopName")) ? "" : reader.GetString("ShopName"),
                    ShopLoginId = reader.IsDBNull(reader.GetOrdinal("ShopLoginId")) ? "" : reader.GetString("ShopLoginId"),
                    DbName = reader.IsDBNull(reader.GetOrdinal("DbName")) ? "" : reader.GetString("DbName"),
                    ShopType = reader.IsDBNull(reader.GetOrdinal("ShopType")) ? "" : reader.GetString("ShopType"),
                    Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? "" : reader.GetString("Address"),
                    ContactNumber = reader.IsDBNull(reader.GetOrdinal("ContactNumber")) ? "" : reader.GetString("ContactNumber"),
                    MaxUsers = reader.IsDBNull(reader.GetOrdinal("MaxUsers")) ? 5 : reader.GetInt32("MaxUsers"),
                    SubscriptionPlan = reader.IsDBNull(reader.GetOrdinal("SubscriptionPlan")) ? "" : reader.GetString("SubscriptionPlan"),
                    AllowBranch = reader.IsDBNull(reader.GetOrdinal("AllowBranch")) || reader.GetBoolean("AllowBranch"),
                    IsActive = reader.GetBoolean("IsActive")
                };
            }
        }
        if (shop == null) return NotFound();
        return View(shop);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateShop(EditShopViewModel model)
    {
        string masterConn = _dbService.GetMasterConnectionString();
        using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync();
            string sql = "UPDATE Tenants SET ShopName = @name, ShopType = @type, Address = @addr, ContactNumber = @contact, MaxUsers = @users, SubscriptionPlan = @plan, AllowBranch = @allowBranch WHERE TenantId = @id";
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@name", model.ShopName);
            cmd.Parameters.AddWithValue("@type", model.ShopType ?? "");
            cmd.Parameters.AddWithValue("@addr", model.Address ?? "");
            cmd.Parameters.AddWithValue("@contact", model.ContactNumber ?? "");
            cmd.Parameters.AddWithValue("@users", model.MaxUsers);
            cmd.Parameters.AddWithValue("@plan", model.SubscriptionPlan);
            cmd.Parameters.AddWithValue("@allowBranch", model.AllowBranch);
            cmd.Parameters.AddWithValue("@id", model.TenantId);
            await cmd.ExecuteNonQueryAsync();
        }

        // Handle Password Reset if provided
        if (!string.IsNullOrWhiteSpace(model.NewAdminPassword))
        {
            string tenantConnStr = _dbService.GetTenantConnectionString(model.DbName);
            using (var tConn = new MySqlConnection(tenantConnStr))
            {
                await tConn.OpenAsync();
                string hashedPass = BCrypt.Net.BCrypt.HashPassword(model.NewAdminPassword);
                string updatePassSql = "UPDATE ShopUsers SET PasswordHash = @pass WHERE Role = 'Admin'";
                using var pCmd = new MySqlCommand(updatePassSql, tConn);
                pCmd.Parameters.AddWithValue("@pass", hashedPass);
                await pCmd.ExecuteNonQueryAsync();
            }
        }

        return RedirectToAction("ShopList");
    }

    private async Task<bool> VerifyCurrentAdminPasswordAsync(
        MySqlConnection conn,
        string adminPassword,
        string tempDataKey,
        CancellationToken cancellationToken)
    {
        string currentUsername = User.Identity?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentUsername))
        {
            TempData[tempDataKey] = "Your session is invalid. Please log in again.";
            return false;
        }

        string? passwordHash;
        await using (var verifyCmd = new MySqlCommand(
            "SELECT PasswordHash FROM MasterUsers WHERE Username = @u LIMIT 1", conn))
        {
            verifyCmd.Parameters.AddWithValue("@u", currentUsername);
            passwordHash = (string?)await verifyCmd.ExecuteScalarAsync(cancellationToken);
        }

        if (passwordHash == null || !BCrypt.Net.BCrypt.Verify(adminPassword, passwordHash))
        {
            TempData[tempDataKey] = "Admin password is incorrect.";
            return false;
        }

        return true;
    }

    private static async Task<Tenant?> GetTenantForActionAsync(
        MySqlConnection conn,
        int tenantId,
        CancellationToken cancellationToken)
    {
        const string sql = @"SELECT TenantId, ShopName, ShopLoginId, DbName, ShopType,
                                    SubscriptionPlan, SubscriptionStartDate, SubscriptionEndDate,
                                    MaxUsers, AllowBranch, Address, ContactNumber
                             FROM Tenants
                             WHERE TenantId = @id
                             LIMIT 1";

        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Tenant
        {
            TenantId = reader.GetInt32("TenantId"),
            ShopName = reader.IsDBNull(reader.GetOrdinal("ShopName")) ? "" : reader.GetString("ShopName"),
            ShopLoginId = reader.IsDBNull(reader.GetOrdinal("ShopLoginId")) ? "" : reader.GetString("ShopLoginId"),
            DbName = reader.IsDBNull(reader.GetOrdinal("DbName")) ? "" : reader.GetString("DbName"),
            ShopType = reader.IsDBNull(reader.GetOrdinal("ShopType")) ? "" : reader.GetString("ShopType"),
            SubscriptionPlan = reader.IsDBNull(reader.GetOrdinal("SubscriptionPlan")) ? "Basic" : reader.GetString("SubscriptionPlan"),
            SubscriptionStartDate = reader.IsDBNull(reader.GetOrdinal("SubscriptionStartDate")) ? DateTime.Now : reader.GetDateTime("SubscriptionStartDate"),
            SubscriptionEndDate = reader.IsDBNull(reader.GetOrdinal("SubscriptionEndDate")) ? DateTime.Now.AddMonths(1) : reader.GetDateTime("SubscriptionEndDate"),
            MaxUsers = reader.IsDBNull(reader.GetOrdinal("MaxUsers")) ? 5 : reader.GetInt32("MaxUsers"),
            AllowBranch = reader.IsDBNull(reader.GetOrdinal("AllowBranch")) || reader.GetBoolean("AllowBranch"),
            Address = reader.IsDBNull(reader.GetOrdinal("Address")) ? "" : reader.GetString("Address"),
            ContactNumber = reader.IsDBNull(reader.GetOrdinal("ContactNumber")) ? "" : reader.GetString("ContactNumber")
        };
    }

    private async Task ResetTenantDatabaseAsync(
        Tenant tenant,
        string shopAdminUsername,
        string newShopAdminPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant.DbName))
        {
            throw new InvalidOperationException("Tenant database name is missing.");
        }

        if (string.IsNullOrWhiteSpace(shopAdminUsername))
        {
            throw new InvalidOperationException("Shop admin username is required.");
        }

        if (string.IsNullOrWhiteSpace(newShopAdminPassword))
        {
            throw new InvalidOperationException("New shop admin password is required.");
        }

        string quotedDbName = QuoteIdentifier(tenant.DbName);
        string masterConn = _dbService.GetMasterConnectionString();

        await using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync(cancellationToken);

            await using (var dropCmd = new MySqlCommand($"DROP DATABASE IF EXISTS {quotedDbName};", conn))
            {
                await dropCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var createCmd = new MySqlCommand($"CREATE DATABASE {quotedDbName} CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;", conn))
            {
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        if (string.Equals(tenant.ShopType, "Restaurant", StringComparison.OrdinalIgnoreCase))
        {
            await _dbService.CloneDatabaseStructure("restaurant_template", tenant.DbName);
            await _dbService.CloneDatabaseData("restaurant_template", tenant.DbName);
            await SetupRestaurantAdmin(tenant.DbName, shopAdminUsername, newShopAdminPassword);
            await UpdateRestaurantSettingsAsync(
                tenant.DbName,
                tenant.ShopName,
                tenant.Address,
                tenant.ContactNumber);
            return;
        }

        await _dbService.CloneDatabaseStructure("pos_template", tenant.DbName);
        await SetupDefaultAdmin(tenant.DbName, shopAdminUsername, newShopAdminPassword);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new InvalidOperationException("Database name is missing.");
        }

        return $"`{identifier.Replace("`", "``")}`";
    }

    private async Task SetupDefaultAdmin(string newDbName, string adminUsername, string adminPassword)
    {
        string tenantConnStr = _dbService.GetTenantConnectionString(newDbName);

        using (var conn = new MySqlConnection(tenantConnStr))
        {
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ၁။ ပထမဆုံး Branch (Main Branch) ကို အလိုအလျောက် သွင်းပေးခြင်း
                string insertBranchSql = @"INSERT INTO Branches (BranchName, Location, Phone, IsMainBranch, IsActive, CreatedAt)
                                           VALUES ('Main Branch', '', '', 1, 1, NOW());
                                           SELECT LAST_INSERT_ID();";
                long branchId;
                using (var cmdBranch = new MySqlCommand(insertBranchSql, conn, transaction))
                {
                    branchId = Convert.ToInt64(await cmdBranch.ExecuteScalarAsync());
                }

                // System defaults are created with each POS tenant, not by Excel import.
                // They remain available when users leave a product's category or unit blank.
                const string insertDefaultMasterDataSql = @"
INSERT INTO Categories (CategoryName)
SELECT 'General'
WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE LOWER(CategoryName) = 'general');

INSERT INTO Units (UnitName, ShortName)
SELECT 'pcs', 'pcs'
WHERE NOT EXISTS (SELECT 1 FROM Units WHERE LOWER(ShortName) = 'pcs');";
                using (var defaultMasterDataCmd = new MySqlCommand(insertDefaultMasterDataSql, conn, transaction))
                {
                    await defaultMasterDataCmd.ExecuteNonQueryAsync();
                }

                // ၂။ BCrypt ဖြင့် Password ကို Hash လုပ်ခြင်း
                string hashedPass = BCrypt.Net.BCrypt.HashPassword(adminPassword);

                // ၃။ ဆိုင်ရှင်အကောင့်သွင်းခြင်း (BranchId ကို 1 ဟု အသေ သတ်မှတ်ပေးလိုက်ပါသည်)
                // Role ကိုလည်း 'Admin' ဟု ပေးလိုက်ပါ (မိတ်ဆွေ၏ Frontend နှင့် ကိုက်ညီစေရန်)
                string sql = @"INSERT INTO ShopUsers (BranchId, Username, PasswordHash, Role, IsActive) 
                           VALUES (@branchId, @user, @pass, 'Admin', 1);";

                using (var cmd = new MySqlCommand(sql, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@branchId", branchId);
                    cmd.Parameters.AddWithValue("@user", adminUsername);
                    cmd.Parameters.AddWithValue("@pass", hashedPass);
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    private async Task RewriteDefaultAdminAsync(string newDbName, string adminUsername, string adminPassword)
    {
        string tenantConnStr = _dbService.GetTenantConnectionString(newDbName);

        using var conn = new MySqlConnection(tenantConnStr);
        await conn.OpenAsync();

        string hashedPass = BCrypt.Net.BCrypt.HashPassword(adminPassword);
        string sql = @"UPDATE ShopUsers
                       SET Username = @user, PasswordHash = @pass, IsActive = 1
                       WHERE Role = 'Admin'
                       LIMIT 1";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@user", adminUsername);
        cmd.Parameters.AddWithValue("@pass", hashedPass);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SetupRestaurantAdmin(string newDbName, string adminUsername, string adminPin)
    {
        string tenantConnStr = _dbService.GetTenantConnectionString(newDbName);

        using (var conn = new MySqlConnection(tenantConnStr))
        {
            await conn.OpenAsync();
            // Restaurant schema use Staff table: FullName, Role, Phone, Email, PinCode, IsActive, CreatedAt
            // We first delete the seed admin from script if any
            using var deleteCmd = new MySqlCommand("DELETE FROM Staff;", conn);
            await deleteCmd.ExecuteNonQueryAsync();

            string sql = @"INSERT INTO Staff (FullName, Role, Phone, Email, PinCode, IsActive, CreatedAt) 
                         VALUES (@user, 0, '09000000000', 'admin@example.com', @pin, 1, @now);";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@user", adminUsername);
                cmd.Parameters.AddWithValue("@pin", adminPin);
                cmd.Parameters.AddWithValue("@now", GetMyanmarNow());
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task UpdateRestaurantSettingsAsync(
        string databaseName,
        string shopName,
        string? address,
        string? contactNumber)
    {
        string tenantConnStr = _dbService.GetTenantConnectionString(databaseName);
        await using var conn = new MySqlConnection(tenantConnStr);
        await conn.OpenAsync();

        const string sql = """
            INSERT INTO Settings
                (Id, ShopName, ShopAddress, ShopPhone, CurrencySymbol, Language)
            VALUES
                (1, @shopName, @address, @phone, 'Ks', 'my-MM')
            ON DUPLICATE KEY UPDATE
                ShopName = VALUES(ShopName),
                ShopAddress = VALUES(ShopAddress),
                ShopPhone = VALUES(ShopPhone),
                CurrencySymbol = VALUES(CurrencySymbol),
                Language = VALUES(Language);
            """;
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@shopName", shopName);
        cmd.Parameters.AddWithValue("@address", address ?? string.Empty);
        cmd.Parameters.AddWithValue("@phone", contactNumber ?? string.Empty);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task TryDropDatabaseAsync(string databaseName)
    {
        try
        {
            await using var conn = new MySqlConnection(_dbService.GetMasterConnectionString());
            await conn.OpenAsync();
            await using var cmd = new MySqlCommand(
                $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)};", conn);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Preserve the original registration error. The orphan database can
            // still be identified by its validated, deterministic name.
        }
    }

    private static bool IsRestaurant(string? shopType) =>
        string.Equals(shopType, "Restaurant", StringComparison.OrdinalIgnoreCase);

    private static bool IsValidShopLoginId(string shopLoginId, bool isRestaurant)
    {
        int maximumLength = isRestaurant ? 45 : 50;
        return shopLoginId.Length is > 0
            && shopLoginId.Length <= maximumLength
            && shopLoginId.All(character =>
                character is >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_');
    }

    private static string BuildTenantDatabaseName(string shopLoginId, bool isRestaurant) =>
        isRestaurant
            ? $"versa_restaurantDB_{shopLoginId}"
            : $"pos_db_{shopLoginId}";

    private static DateTime GetMyanmarNow() =>
        DateTime.UtcNow.AddMinutes(390);

    private async Task RewriteRestaurantAdminAsync(string newDbName, string adminUsername, string adminPin)
    {
        string tenantConnStr = _dbService.GetTenantConnectionString(newDbName);

        using var conn = new MySqlConnection(tenantConnStr);
        await conn.OpenAsync();

        string sql = @"UPDATE Staff
                       SET FullName = @name,
                           PinCode = @pin,
                           IsActive = 1
                       WHERE Role = 0
                       LIMIT 1";

        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", adminUsername);
        cmd.Parameters.AddWithValue("@pin", adminPin);
        await cmd.ExecuteNonQueryAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Monitor()
    {
        var shops = new List<ShopMonitoringViewModel>();
        string masterConn = _dbService.GetMasterConnectionString();

        using (var conn = new MySqlConnection(masterConn))
        {
            await conn.OpenAsync();

            // 1. Get all tenants
            string tenantSql = @"SELECT TenantId, ShopName, ShopLoginId, DbName, 
                                        SubscriptionPlan, SubscriptionStartDate, 
                                        SubscriptionEndDate, IsActive, MaxUsers
                                 FROM Tenants ORDER BY ShopName";

            var tenants = new List<(int tenantId, string shopName, string shopLoginId, string dbName,
                                     string plan, DateTime start, DateTime end, bool isActive, int maxUsers)>();

            using (var cmd = new MySqlCommand(tenantSql, conn))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tenants.Add((
                        reader.GetInt32("TenantId"),
                        reader.IsDBNull(reader.GetOrdinal("ShopName")) ? "" : reader.GetString("ShopName"),
                        reader.IsDBNull(reader.GetOrdinal("ShopLoginId")) ? "" : reader.GetString("ShopLoginId"),
                        reader.IsDBNull(reader.GetOrdinal("DbName")) ? "" : reader.GetString("DbName"),
                        reader.IsDBNull(reader.GetOrdinal("SubscriptionPlan")) ? "Basic" : reader.GetString("SubscriptionPlan"),
                        reader.IsDBNull(reader.GetOrdinal("SubscriptionStartDate")) ? DateTime.Now : reader.GetDateTime("SubscriptionStartDate"),
                        reader.IsDBNull(reader.GetOrdinal("SubscriptionEndDate")) ? DateTime.Now.AddMonths(1) : reader.GetDateTime("SubscriptionEndDate"),
                        reader.IsDBNull(reader.GetOrdinal("IsActive")) || reader.GetBoolean("IsActive"),
                        reader.IsDBNull(reader.GetOrdinal("MaxUsers")) ? 5 : reader.GetInt32("MaxUsers")
                    ));
                }
            }

            // 2. For each tenant, query their database for monitoring stats
            foreach (var t in tenants)
            {
                var shop = new ShopMonitoringViewModel
                {
                    TenantId = t.tenantId,
                    ShopName = t.shopName,
                    ShopLoginId = t.shopLoginId,
                    DbName = t.dbName,
                    SubscriptionPlan = t.plan,
                    IsActive = t.isActive,
                    SubscriptionStartDate = t.start,
                    SubscriptionEndDate = t.end,
                    MaxUsers = t.maxUsers
                };

                if (!string.IsNullOrEmpty(t.dbName))
                {
                    try
                    {
                        string tenantConnStr = _dbService.GetTenantConnectionString(t.dbName);
                        using var tConn = new MySqlConnection(tenantConnStr);
                        await tConn.OpenAsync();

                        // --- Online Users (have a non-null SessionToken) ---
                        try
                        {
                            string onlineSql = @"SELECT 
                                COUNT(*) AS TotalUsers,
                                SUM(CASE WHEN SessionToken IS NOT NULL AND SessionToken <> '' THEN 1 ELSE 0 END) AS OnlineUsers
                            FROM ShopUsers WHERE IsActive = 1";
                            using var onlineCmd = new MySqlCommand(onlineSql, tConn);
                            using var onlineReader = await onlineCmd.ExecuteReaderAsync();
                            if (await onlineReader.ReadAsync())
                            {
                                shop.TotalUserCount = onlineReader.IsDBNull(0) ? 0 : onlineReader.GetInt32(0);
                                shop.OnlineUserCount = onlineReader.IsDBNull(1) ? 0 : onlineReader.GetInt32(1);
                                shop.IsOnline = shop.OnlineUserCount > 0;
                            }
                            onlineReader.Close();
                        }
                        catch { /* Table may not exist yet */ }

                        // --- Last Activity ---
                        try
                        {
                            string activitySql = "SELECT MAX(CreatedAt) FROM ActivityLogs";
                            using var actCmd = new MySqlCommand(activitySql, tConn);
                            var lastAct = await actCmd.ExecuteScalarAsync();
                            if (lastAct != null && lastAct != DBNull.Value)
                                shop.LastActivityDate = Convert.ToDateTime(lastAct);
                        }
                        catch { /* Table may not exist */ }

                        // --- Database Size ---
                        try
                        {
                            string sizeSql = $@"SELECT 
                                ROUND(SUM(data_length + index_length) / 1024 / 1024, 2) 
                                FROM information_schema.tables 
                                WHERE table_schema = @dbName";
                            using var sizeCmd = new MySqlCommand(sizeSql, tConn);
                            sizeCmd.Parameters.AddWithValue("@dbName", t.dbName);
                            var sizeResult = await sizeCmd.ExecuteScalarAsync();
                            if (sizeResult != null && sizeResult != DBNull.Value)
                                shop.DbSizeMb = Convert.ToDouble(sizeResult);
                        }
                        catch { /* ignore */ }

                        // --- Total Sales ---
                        try
                        {
                            string salesSql = "SELECT COUNT(*) FROM Sales";
                            using var salesCmd = new MySqlCommand(salesSql, tConn);
                            var salesCount = await salesCmd.ExecuteScalarAsync();
                            if (salesCount != null && salesCount != DBNull.Value)
                                shop.TotalSales = Convert.ToInt64(salesCount);
                        }
                        catch { /* Table may not exist */ }

                        // --- Total Products ---
                        try
                        {
                            string prodSql = "SELECT COUNT(*) FROM Products";
                            using var prodCmd = new MySqlCommand(prodSql, tConn);
                            var prodCount = await prodCmd.ExecuteScalarAsync();
                            if (prodCount != null && prodCount != DBNull.Value)
                                shop.TotalProducts = Convert.ToInt64(prodCount);
                        }
                        catch { /* ignore */ }

                        // --- Total Customers ---
                        try
                        {
                            string custSql = "SELECT COUNT(*) FROM Customers";
                            using var custCmd = new MySqlCommand(custSql, tConn);
                            var custCount = await custCmd.ExecuteScalarAsync();
                            if (custCount != null && custCount != DBNull.Value)
                                shop.TotalCustomers = Convert.ToInt64(custCount);
                        }
                        catch { /* ignore */ }
                    }
                    catch (MySqlException)
                    {
                        // Database might not exist or be inaccessible — leave defaults
                    }
                }

                shops.Add(shop);
            }
        }

        return View(shops);
    }

    [HttpGet]
    public IActionResult MacEncryption()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EncryptMac([FromBody] MacRequest model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.MacAddress))
        {
            return Json(new { success = false, message = "Invalid MAC Address" });
        }

        string encryptedKey = _encryptionService.EncryptMacAddress(model.MacAddress);
        return Json(new { success = true, encryptedKey });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GenerateAndroidLicense([FromBody] DeviceCodeRequest model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.DeviceCode))
        {
            return Json(new { success = false, message = "Invalid device code" });
        }

        string activationKey = LotusLicense.GenerateActivationKey(model.DeviceCode);
        return Json(new { success = true, activationKey });
    }
}
