using System.Text.Json;
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

    private static Task EnsureProductionRecipesAsync(MySqlConnection conn) => conn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS ProductionRecipes (
    ProductId INT NOT NULL PRIMARY KEY,
    UnitId INT NOT NULL,
    RecipeJson LONGTEXT NOT NULL
) ENGINE=InnoDB");

    [HttpGet("production/{productId}")]
    public async Task<IActionResult> GetProductionRecipe(int productId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        await EnsureProductionRecipesAsync(conn);
        var product = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT ProductId, Name, SalePrice, UnitId FROM Products WHERE ProductId = @productId AND IsActive = 1", new { productId });
        if (product == null) return NotFound();
        var recipe = await conn.QuerySingleOrDefaultAsync<dynamic>(
            "SELECT UnitId, RecipeJson FROM ProductionRecipes WHERE ProductId = @productId", new { productId });
        var items = recipe == null ? new List<BundleComponentSelector>()
            : JsonSerializer.Deserialize<List<BundleComponentSelector>>((string)recipe.RecipeJson)!;
        bool recovered = false;
        int? recipeUnitId = recipe == null ? (int?)product.UnitId : (int?)recipe.UnitId;
        if (recipe == null)
        {
            // Earlier production created one batch and recorded each component in base units.
            // Only recover an unambiguous single batch; never infer a recipe from sales or purchases.
            var reason = $"Production of product #{productId}";
            var additions = (await conn.QueryAsync<dynamic>(@"
SELECT Quantity, BranchId, UnitId FROM StockAdjustments
WHERE ProductId = @productId AND Type = 'Addition' AND Reason = @reason", new { productId, reason })).ToList();
            if (additions.Count == 1 && Convert.ToDecimal(additions[0].Quantity) > 0)
            {
                items = (await conn.QueryAsync<BundleComponentSelector>(@"
SELECT 'Product' AS ItemType, ProductId, UnitId, SUM(Quantity) / @quantity AS Quantity
FROM StockAdjustments WHERE Type = 'Subtraction' AND Reason = @reason AND BranchId = @branchId
GROUP BY ProductId, UnitId", new { reason, quantity = Convert.ToDecimal(additions[0].Quantity), branchId = Convert.ToInt32(additions[0].BranchId) })).ToList();
                recovered = items.Count > 0;
                if (recovered) recipeUnitId = (int?)additions[0].UnitId;
            }
        }
        return Ok(new { ProductId = productId, BundleName = (string)product.Name, BundlePrice = (decimal)product.SalePrice,
            ProductionUnitId = recipeUnitId,
            BundleItems = items, RecipeRecovered = recovered });
    }

    [HttpPost("production/{productId}")]
    public Task<IActionResult> ProduceMore(int productId, [FromBody] BundleEntryModel model) =>
        CreateProductionAsync(model, productId);

    private int ProductionBranch(BundleEntryModel model)
    {
        var branchId = User.IsInRole("Admin") && model.BranchId.HasValue
            ? model.BranchId.Value
            : int.TryParse(User.FindFirst("BranchId")?.Value, out var claimBranch) ? claimBranch : 0;
        if (branchId <= 0) throw new BundleProductionException("branchRequired", "A production branch is required.");
        return branchId;
    }

    private static async Task<List<ProductionComponent>> LoadProductionComponentsAsync(
        MySqlConnection conn, MySqlTransaction transaction, BundleEntryModel model, int branchId, bool lockStock)
    {
        if (model.BundleItems == null || model.BundleItems.Count == 0)
            throw new BundleProductionException("componentsRequired", "Add component items before producing a product.");
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Branches WHERE BranchId = @branchId", new { branchId }, transaction) == 0)
            throw new BundleProductionException("branchMissing", "The production branch does not exist.");

        var components = new List<ProductionComponent>();
        // A consistent lock order prevents production requests from locking components in opposite orders.
        foreach (var item in model.BundleItems.OrderBy(x => x.ProductId))
        {
            if (item.Quantity <= 0) throw new BundleProductionException("componentQuantity", "Component quantities must be positive.");
            var type = string.IsNullOrWhiteSpace(item.ItemType) ? "Product" : item.ItemType.Trim();
            if (type.Equals("Service", StringComparison.OrdinalIgnoreCase))
            {
                if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Services WHERE ServiceId = @ServiceId AND IsActive = 1", item, transaction) == 0)
                    throw new BundleProductionException("serviceUnavailable", "A selected service is unavailable.");
                continue; // Services have no physical stock to consume.
            }
            if (!type.Equals("Product", StringComparison.OrdinalIgnoreCase) || !item.ProductId.HasValue)
                throw new BundleProductionException("invalidComponent", "Invalid production component.");

            var product = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT UnitId, CostPrice FROM Products WHERE ProductId = @ProductId AND IsActive = 1" + (lockStock ? " FOR UPDATE" : ""), item, transaction);
            if (product == null || product.UnitId == null)
                throw new BundleProductionException("productUnavailable", "A component product is unavailable or has no base unit.");
            int unitId = Convert.ToInt32(product.UnitId);
            decimal multiplier = 1;
            if (item.UnitId.HasValue && item.UnitId.Value != unitId)
            {
                var conversion = await conn.QuerySingleOrDefaultAsync<decimal?>(
                    "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId AND ToUnitId = @BaseUnitId",
                    new { item.ProductId, item.UnitId, BaseUnitId = unitId }, transaction);
                if (!conversion.HasValue || conversion.Value <= 0)
                    throw new BundleProductionException("invalidConversion", "A component unit conversion is invalid.");
                multiplier = conversion.Value;
            }
            decimal baseQuantity = item.Quantity * multiplier;
            if (decimal.Round(baseQuantity, 5) != baseQuantity)
                throw new BundleProductionException("componentPrecision", "Component consumption cannot exceed five decimal places in base units.");
            var stock = await conn.QuerySingleOrDefaultAsync<decimal?>(
                "SELECT Quantity FROM ProductStocks WHERE BranchId = @branchId AND ProductId = @ProductId" + (lockStock ? " FOR UPDATE" : ""),
                new { branchId, item.ProductId }, transaction);
            components.Add(new ProductionComponent(item.ProductId.Value, unitId, baseQuantity, stock ?? 0, Convert.ToDecimal(product.CostPrice ?? 0)));
        }
        return BundleProductionCalculation.Combine(components);
    }

    [HttpPost("production-availability")]
    public async Task<IActionResult> ProductionAvailability([FromBody] BundleProductionAvailabilityModel request)
    {
        var model = new BundleEntryModel { BranchId = request.BranchId, BundleItems = request.BundleItems };
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();
        try
        {
            var branchId = ProductionBranch(model);
            var components = await LoadProductionComponentsAsync(conn, transaction, model, branchId, false);
            return Ok(new { MaximumAvailability = BundleProductionCalculation.MaximumAvailability(components), BranchId = branchId });
        }
        catch (BundleProductionException ex) { return BadRequest(new { Code = ex.Code, Message = ex.Message }); }
    }

    private async Task<IActionResult> CreateProductionAsync(BundleEntryModel model, int? existingProductId = null)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        // Schema setup must finish before starting the stock transaction.
        await EnsureProductionRecipesAsync(conn);
        using var transaction = await conn.BeginTransactionAsync();
        try
        {
            var branchId = ProductionBranch(model);
            dynamic? existingProduct = null;
            if (existingProductId.HasValue)
            {
                existingProduct = await conn.QuerySingleOrDefaultAsync<dynamic>(
                    "SELECT Name, SalePrice, UnitId, CostPrice FROM Products WHERE ProductId = @ProductId AND IsActive = 1 FOR UPDATE",
                    new { ProductId = existingProductId.Value }, transaction);
                if (existingProduct == null)
                    throw new BundleProductionException("productUnavailable", "The finished product is unavailable.");
                if (model.ProductionUnitId != (int?)existingProduct.UnitId)
                    throw new BundleProductionException("unitChanged", "The finished product unit changed. Review the recipe before producing.");
                if (model.BundleItems?.Any(x => x.ProductId == existingProductId) == true)
                    throw new BundleProductionException("selfComponent", "A finished product cannot consume itself.");
                model.BundleName = (string)existingProduct.Name;
                model.BundlePrice = (decimal)existingProduct.SalePrice;
            }
            if (string.IsNullOrWhiteSpace(model.BundleName) || model.BundlePrice < 0)
                throw new BundleProductionException("namePriceRequired", "A product name and a non-negative selling price are required.");
            if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Units WHERE UnitId = @ProductionUnitId", model, transaction) == 0)
                throw new BundleProductionException("unitRequired", "Select a unit for the produced product.");
            var components = await LoadProductionComponentsAsync(conn, transaction, model, branchId, true);
            BundleProductionCalculation.ValidateQuantity(model.ProductionQuantity, BundleProductionCalculation.MaximumAvailability(components));
            var productId = existingProductId ?? await conn.ExecuteScalarAsync<int>(@"
INSERT INTO Products (Name, Sku, UnitId, CostPrice, SalePrice, AlertQuantity)
VALUES (@Name, @Sku, @UnitId, @CostPrice, @SalePrice, 0); SELECT LAST_INSERT_ID();",
                new { Name = model.BundleName.Trim(), Sku = "PRD" + Guid.NewGuid().ToString("N"), UnitId = model.ProductionUnitId,
                    CostPrice = components.Sum(x => x.BaseQuantity * x.UnitCost), SalePrice = model.BundlePrice }, transaction);

            if (existingProductId.HasValue)
            {
                var currentStock = await conn.ExecuteScalarAsync<decimal>(
                    "SELECT COALESCE(SUM(Quantity), 0) FROM ProductStocks WHERE ProductId = @productId", new { productId }, transaction);
                var unitCost = BundleProductionCalculation.FinishedUnitCost(currentStock, (decimal)existingProduct.CostPrice,
                    model.ProductionQuantity, components.Sum(x => x.BaseQuantity * x.UnitCost));
                await conn.ExecuteAsync("UPDATE Products SET CostPrice = @unitCost WHERE ProductId = @productId", new { unitCost, productId }, transaction);
            }
            foreach (var item in model.BundleItems.Where(x => !string.Equals(x.ItemType?.Trim(), "Service", StringComparison.OrdinalIgnoreCase)))
                item.UnitId ??= components.Single(x => x.ProductId == item.ProductId).UnitId;
            await conn.ExecuteAsync(@"
INSERT INTO ProductionRecipes (ProductId, UnitId, RecipeJson) VALUES (@productId, @ProductionUnitId, @recipeJson)
ON DUPLICATE KEY UPDATE UnitId = @ProductionUnitId, RecipeJson = @recipeJson",
                new { productId, model.ProductionUnitId, recipeJson = JsonSerializer.Serialize(model.BundleItems) }, transaction);

            var userId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? (int?)id : null;
            var reason = $"Production of product #{productId}";
            foreach (var component in components)
            {
                var quantity = component.BaseQuantity * model.ProductionQuantity;
                var affected = await conn.ExecuteAsync(@"
UPDATE ProductStocks SET Quantity = Quantity - @quantity
WHERE BranchId = @branchId AND ProductId = @ProductId AND Quantity >= @quantity",
                    new { quantity, branchId, component.ProductId }, transaction);
                if (affected != 1) throw new BundleProductionException("stockChanged", "Component stock changed. Refresh availability and try again.");
                await conn.ExecuteAsync(@"
INSERT INTO StockAdjustments (BranchId, ProductId, Type, Quantity, UnitId, Reason, AdjustmentDate, UserId)
VALUES (@branchId, @ProductId, 'Subtraction', @quantity, @UnitId, @reason, NOW(), @userId)",
                    new { branchId, component.ProductId, quantity, component.UnitId, reason, userId }, transaction);
            }
            await conn.ExecuteAsync("INSERT INTO ProductStocks (BranchId, ProductId, Quantity) VALUES (@branchId, @productId, @ProductionQuantity) ON DUPLICATE KEY UPDATE Quantity = Quantity + @ProductionQuantity",
                new { branchId, productId, model.ProductionQuantity }, transaction);
            await conn.ExecuteAsync(@"
INSERT INTO StockAdjustments (BranchId, ProductId, Type, Quantity, UnitId, Reason, AdjustmentDate, UserId)
VALUES (@branchId, @productId, 'Addition', @ProductionQuantity, @ProductionUnitId, @reason, NOW(), @userId)",
                new { branchId, productId, model.ProductionQuantity, model.ProductionUnitId, reason, userId }, transaction);
            await transaction.CommitAsync();
            return Ok(new { ProductId = productId, Message = "Product produced successfully." });
        }
        catch (BundleProductionException ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { Code = ex.Code, Message = ex.Message });
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

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
    public async Task<IActionResult> GetAll([FromQuery] bool includeProduction = false)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = @"SELECT b.*, (SELECT COUNT(*) FROM BundleItems WHERE BundleId = b.BundleId) as ItemsCount FROM Bundles b WHERE IsActive = 1";
        var rows = (await conn.QueryAsync(sql)).ToList();
        // Management-only expansion: POS callers still receive standard bundles only.
        if (includeProduction)
        {
            await conn.OpenAsync();
            await EnsureProductionRecipesAsync(conn);
            rows.AddRange(await conn.QueryAsync(@"
SELECT p.ProductId AS ProductionProductId, 1 AS IsProduction, p.Name AS BundleName, p.SalePrice AS BundlePrice,
       COALESCE(JSON_LENGTH(r.RecipeJson), (SELECT COUNT(*) FROM StockAdjustments sa
         WHERE sa.Reason = CONCAT('Production of product #', p.ProductId) AND sa.Type = 'Subtraction')) AS ItemsCount
FROM Products p LEFT JOIN ProductionRecipes r ON r.ProductId = p.ProductId
WHERE p.IsActive = 1 AND (r.ProductId IS NOT NULL OR EXISTS (
  SELECT 1 FROM StockAdjustments sa WHERE sa.ProductId = p.ProductId AND sa.Type = 'Addition'
  AND sa.Reason = CONCAT('Production of product #', p.ProductId)))"));
        }
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BundleEntryModel model)
    {
        if (model.AddAsProduct) return await CreateProductionAsync(model);
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
        if (model.AddAsProduct) return BadRequest(new { Code = "creationOnly", Message = "Production is only available when creating a new product." });
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
