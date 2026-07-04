using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;
using Versa.POS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BundlesController : BaseShopController
{
    private readonly DatabaseService _dbService;
    public BundlesController(DatabaseService dbService) => _dbService = dbService;

    private static async Task EnsureBundleItemsSupportsServicesAsync(MySqlConnection conn, MySqlTransaction transaction)
    {
        var hasItemType = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'ItemType'", transaction: transaction);

        if (hasItemType == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN ItemType VARCHAR(20) NOT NULL DEFAULT 'Product'", transaction: transaction);
        }

        var hasServiceId = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'ServiceId'", transaction: transaction);

        if (hasServiceId == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN ServiceId INT NULL", transaction: transaction);
        }
        
        var hasUnitId = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'UnitId'", transaction: transaction);

        if (hasUnitId == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN UnitId INT NULL", transaction: transaction);
        }

        // Allow ComponentProductId to be nullable (for service components).
        try
        {
            await conn.ExecuteAsync("ALTER TABLE BundleItems MODIFY COLUMN ComponentProductId INT NULL", transaction: transaction);
        }
        catch
        {
            // Ignore if already nullable or restricted by existing schema.
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = @"SELECT b.*, (SELECT COUNT(*) FROM BundleItems WHERE BundleId = b.BundleId) as ItemsCount FROM Bundles b WHERE IsActive = 1";
        return Ok(await conn.QueryAsync(sql));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BundleEntryModel model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();
        try
        {
            await EnsureBundleItemsSupportsServicesAsync(conn, (MySqlTransaction)transaction);

            int bundleId = await conn.ExecuteScalarAsync<int>(
                "INSERT INTO Bundles (BundleName, BundlePrice) VALUES (@BundleName, @BundlePrice); SELECT LAST_INSERT_ID();",
                model, transaction);

            foreach (var item in model.BundleItems)
            {
                var itemType = string.IsNullOrWhiteSpace(item.ItemType) ? "Product" : item.ItemType.Trim();
                if (itemType.Equals("Service", StringComparison.OrdinalIgnoreCase))
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO BundleItems (BundleId, ItemType, ComponentProductId, ServiceId, UnitId, Quantity) VALUES (@BundleId, 'Service', NULL, @ServiceId, NULL, @Quantity)",
                        new { BundleId = bundleId, ServiceId = item.ServiceId, Quantity = item.Quantity }, transaction);
                }
                else
                {
                    int? effectiveUnitId = item.UnitId;
                    if (!effectiveUnitId.HasValue && item.ProductId.HasValue)
                    {
                        effectiveUnitId = await conn.QueryFirstOrDefaultAsync<int?>(
                            "SELECT UnitId FROM Products WHERE ProductId = @ProductId",
                            new { ProductId = item.ProductId.Value }, transaction);
                    }

                    await conn.ExecuteAsync(
                        "INSERT INTO BundleItems (BundleId, ItemType, ComponentProductId, ServiceId, UnitId, Quantity) VALUES (@BundleId, 'Product', @ProductId, NULL, @UnitId, @Quantity)",
                        new { BundleId = bundleId, ProductId = item.ProductId, UnitId = effectiveUnitId, Quantity = item.Quantity }, transaction);
                }
            }
            await transaction.CommitAsync();
            return Ok(new { Message = "Bundle Created" });
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // Bundle Master အချက်အလက်
        var master = await conn.QueryFirstOrDefaultAsync(@"
        SELECT BundleId, BundleName, BundlePrice 
        FROM Bundles WHERE BundleId = @id", new { id });

        if (master == null) return NotFound();

        // Bundle ထဲတွင် ပါဝင်သော ပစ္စည်းများစာရင်း (Product Name ပါတွဲယူရန်)
        var items = await conn.QueryAsync(@"
        SELECT 
            bi.BundleItemId,
            IFNULL(bi.ItemType,'Product') as ItemType,
            bi.ComponentProductId as ProductId,
            bi.ServiceId,
            bi.UnitId,
            CASE 
                WHEN UPPER(TRIM(IFNULL(bi.ItemType,''))) LIKE '%SERVICE%' THEN sv.ServiceName
                ELSE p.Name
            END as ItemName,
            u.UnitName,
            bi.Quantity
        FROM BundleItems bi
        LEFT JOIN Products p ON bi.ComponentProductId = p.ProductId
        LEFT JOIN Services sv ON bi.ServiceId = sv.ServiceId
        LEFT JOIN Units u ON bi.UnitId = u.UnitId
        WHERE bi.BundleId = @id", new { id });

        return Ok(new { master, items });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] BundleEntryModel model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            await EnsureBundleItemsSupportsServicesAsync(conn, (MySqlTransaction)transaction);

            // ၁။ Bundle Master အချက်အလက်ကို Update လုပ်ခြင်း
            await conn.ExecuteAsync(
                "UPDATE Bundles SET BundleName = @BundleName, BundlePrice = @BundlePrice WHERE BundleId = @BundleId",
                new { model.BundleName, model.BundlePrice, BundleId = id }, transaction);

            // ၂။ ရှိပြီးသား BundleItems များကို အရင်ဖျက်ခြင်း (Clean & Insert logic)
            await conn.ExecuteAsync("DELETE FROM BundleItems WHERE BundleId = @BundleId", new { BundleId = id }, transaction);

            // ၃။ ပစ္စည်းအသစ်များကို ပြန်လည်ထည့်သွင်းခြင်း
            foreach (var item in model.BundleItems)
            {
                var itemType = string.IsNullOrWhiteSpace(item.ItemType) ? "Product" : item.ItemType.Trim();
                if (itemType.Equals("Service", StringComparison.OrdinalIgnoreCase))
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO BundleItems (BundleId, ItemType, ComponentProductId, ServiceId, UnitId, Quantity) VALUES (@BundleId, 'Service', NULL, @ServiceId, NULL, @Quantity)",
                        new { BundleId = id, ServiceId = item.ServiceId, Quantity = item.Quantity }, transaction);
                }
                else
                {
                    int? effectiveUnitId = item.UnitId;
                    if (!effectiveUnitId.HasValue && item.ProductId.HasValue)
                    {
                        effectiveUnitId = await conn.QueryFirstOrDefaultAsync<int?>(
                            "SELECT UnitId FROM Products WHERE ProductId = @ProductId",
                            new { ProductId = item.ProductId.Value }, transaction);
                    }

                    await conn.ExecuteAsync(
                        "INSERT INTO BundleItems (BundleId, ItemType, ComponentProductId, ServiceId, UnitId, Quantity) VALUES (@BundleId, 'Product', @ProductId, NULL, @UnitId, @Quantity)",
                        new { BundleId = id, ProductId = item.ProductId, UnitId = effectiveUnitId, Quantity = item.Quantity }, transaction);
                }
            }

            await transaction.CommitAsync();
            return Ok(new { Message = "Bundle Updated Successfully" });
        }
        catch
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { Message = "Update Failed" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        // IsActive ကို 0 ပြောင်းခြင်း သို့မဟုတ် အပြီးဖျက်ခြင်း (ဒီမှာတော့ IsActive သုံးပါမည်)
        await conn.ExecuteAsync("UPDATE Bundles SET IsActive = 0 WHERE BundleId = @id", new { id });
        return Ok(new { Message = "Bundle Removed Successfully" });
    }
}
