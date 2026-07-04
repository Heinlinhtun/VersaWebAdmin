using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;
using Versa.POS.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ProductsController : BaseShopController
{
    private readonly DatabaseService _dbService;
    public ProductsController(DatabaseService dbService) => _dbService = dbService;
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductEntryViewModel model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            // 0. Handle Category Auto-creation
            int? resolvedCategoryId = model.CategoryId;
            if (!resolvedCategoryId.HasValue && !string.IsNullOrEmpty(model.CategoryName))
            {
                string checkCat = "SELECT CategoryId FROM Categories WHERE CategoryName = @CategoryName LIMIT 1";
                resolvedCategoryId = await conn.QueryFirstOrDefaultAsync<int?>(checkCat, new { model.CategoryName }, transaction);
                if (!resolvedCategoryId.HasValue)
                {
                    string insCat = "INSERT INTO Categories (CategoryName) VALUES (@CategoryName); SELECT LAST_INSERT_ID();";
                    resolvedCategoryId = await conn.ExecuteScalarAsync<int>(insCat, new { model.CategoryName }, transaction);
                }
            }

            // 0. Handle Unit Auto-creation
            int? resolvedUnitId = model.UnitId;
            if (!resolvedUnitId.HasValue && !string.IsNullOrEmpty(model.UnitName))
            {
                string checkUnit = "SELECT UnitId FROM Units WHERE UnitName = @UnitName LIMIT 1";
                resolvedUnitId = await conn.QueryFirstOrDefaultAsync<int?>(checkUnit, new { model.UnitName }, transaction);
                if (!resolvedUnitId.HasValue)
                {
                    string insUnit = "INSERT INTO Units (UnitName, ShortName) VALUES (@UnitName, @ShortName); SELECT LAST_INSERT_ID();";
                    resolvedUnitId = await conn.ExecuteScalarAsync<int>(insUnit, new { model.UnitName, ShortName = model.UnitName }, transaction);
                }
            }

            // Generate Sku if not provided
            if (string.IsNullOrWhiteSpace(model.Sku))
            {
                // Generate a unique SKU: SKU + YearMonthDay + millisecond part of ticks
                model.Sku = "SKU" + DateTime.Now.ToString("yyyyMMdd") + (DateTime.Now.Ticks % 1000000).ToString("D6");
            }

            // ၁။ Product ကို အရင်ဆုံး Insert လုပ်သည်
            string productSql = @"INSERT INTO Products (Name, Sku, CategoryId, UnitId, CostPrice, SalePrice, AlertQuantity) 
                                 VALUES (@Name, @Sku, @CategoryId, @UnitId, @CostPrice, @SalePrice, @AlertQuantity); 
                                 SELECT LAST_INSERT_ID();";

            int productId = await conn.ExecuteScalarAsync<int>(productSql, new
            {
                model.Name,
                model.Sku,
                CategoryId = resolvedCategoryId,
                UnitId = resolvedUnitId,
                model.CostPrice,
                model.SalePrice,
                model.AlertQuantity
            }, transaction);

            // ၂။ Unit Conversions သိမ်းဆည်းခြင်း
            if (model.Conversions != null && model.Conversions.Any())
            {
                string convSql = @"INSERT INTO UnitConversions (ProductId, FromUnitId, ToUnitId, Multiplier, BaseSalePrice) 
                                  VALUES (@ProductId, @FromUnitId, @ToUnitId, @Multiplier, @BaseSalePrice)";

                foreach (var conv in model.Conversions)
                {
                    conv.ProductId = productId;
                    await conn.ExecuteAsync(convSql, conv, transaction);
                }
            }

            // ၃။ Additional Prices (Wholesale/VIP) သိမ်းဆည်းခြင်း
            if (model.AdditionalPrices != null && model.AdditionalPrices.Any())
            {
                string priceSql = @"INSERT INTO ProductPrices (ProductId, PriceName, PriceValue, MinQuantity, UnitId) 
                                    VALUES (@productId, @PriceName, @PriceValue, @MinQuantity, @UnitId)";

                foreach (var price in model.AdditionalPrices)
                {
                    await conn.ExecuteAsync(priceSql, new
                    {
                        productId,
                        price.PriceName,
                        price.PriceValue,
                        price.MinQuantity,
                        price.UnitId
                    }, transaction);
                }
            }

            await transaction.CommitAsync();
            return Ok(new { productId, message = "ကုန်ပစ္စည်းအသစ်ကို အောင်မြင်စွာ သိမ်းဆည်းပြီးပါပြီ။" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        // CostPrice (Moving Average) နှင့် နောက်ဆုံးဝယ်ဈေး (LastPurchasePrice) ကိုပါ ဆွဲထုတ်ခြင်း
        var sql = @"
            SELECT 
                p.ProductId, p.Name, p.Sku, p.CategoryId, p.UnitId, 
                p.CostPrice AS CostPrice, p.SalePrice AS SalePrice, p.AlertQuantity AS AlertQuantity,
                (
                    SELECT 
                        (poi.CostPrice / IFNULL(uc.Multiplier, 1))
                    FROM PurchaseOrderItems poi
                    LEFT JOIN UnitConversions uc 
                        ON uc.ProductId = poi.ProductId AND uc.FromUnitId = poi.UnitId
                    WHERE poi.ProductId = p.ProductId
                    ORDER BY poi.POItemId DESC
                    LIMIT 1
                ) AS LastPurchasePrice
            FROM Products p 
            WHERE p.IsActive = 1";
        
        var list = await conn.QueryAsync<dynamic>(sql);
        return Ok(list);
    }

    [HttpGet("units/{productId}")]
    public async Task<IActionResult> GetProductUnits(int productId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // ✅ မူရင်းဈေး (Products) နှင့် conversion ဈေး (UnitConversions) ကိုပါ တွဲယူခြင်း
        string sql = @"
    SELECT u.UnitId, u.UnitName, 1 AS Multiplier, p.SalePrice 
    FROM Products p 
    JOIN Units u ON p.UnitId = u.UnitId 
    WHERE p.ProductId = @productId
    UNION
    SELECT uc.FromUnitId AS UnitId, u.UnitName, uc.Multiplier, uc.BaseSalePrice AS SalePrice 
    FROM UnitConversions uc
    JOIN Units u ON uc.FromUnitId = u.UnitId
    WHERE uc.ProductId = @productId";

        var units = await conn.QueryAsync<dynamic>(sql, new { productId });
        return Ok(units);
    }

    [HttpGet("top-selling/{branchId}")]
    public async Task<IActionResult> GetTopSellingProducts(int branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // အရေအတွက်အများဆုံး ရောင်းရသည့် ပစ္စည်း ၁၀ ခုကို ဆွဲထုတ်ခြင်း
        string sql = @"
        SELECT p.Name as ProductName, SUM(si.Quantity) as TotalQty, SUM(si.SubTotal) as TotalRevenue
        FROM SaleItems si
        JOIN Sales s ON si.SaleId = s.SaleId
        JOIN Products p ON si.ProductId = p.ProductId
        WHERE s.BranchId = @branchId
        GROUP BY si.ProductId
        ORDER BY TotalQty DESC
        LIMIT 10";

        var topProducts = await conn.QueryAsync<dynamic>(sql, new { branchId });
        return Ok(topProducts);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ProductEntryViewModel model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            // 0. Handle Category Auto-creation
            int? resolvedCategoryId = model.CategoryId;
            if (!resolvedCategoryId.HasValue && !string.IsNullOrEmpty(model.CategoryName))
            {
                string checkCat = "SELECT CategoryId FROM Categories WHERE CategoryName = @CategoryName LIMIT 1";
                resolvedCategoryId = await conn.QueryFirstOrDefaultAsync<int?>(checkCat, new { model.CategoryName }, transaction);
                if (!resolvedCategoryId.HasValue)
                {
                    string insCat = "INSERT INTO Categories (CategoryName) VALUES (@CategoryName); SELECT LAST_INSERT_ID();";
                    resolvedCategoryId = await conn.ExecuteScalarAsync<int>(insCat, new { model.CategoryName }, transaction);
                }
            }

            // 0. Handle Unit Auto-creation
            int? resolvedUnitId = model.UnitId;
            if (!resolvedUnitId.HasValue && !string.IsNullOrEmpty(model.UnitName))
            {
                string checkUnit = "SELECT UnitId FROM Units WHERE UnitName = @UnitName LIMIT 1";
                resolvedUnitId = await conn.QueryFirstOrDefaultAsync<int?>(checkUnit, new { model.UnitName }, transaction);
                if (!resolvedUnitId.HasValue)
                {
                    string insUnit = "INSERT INTO Units (UnitName, ShortName) VALUES (@UnitName, @ShortName); SELECT LAST_INSERT_ID();";
                    resolvedUnitId = await conn.ExecuteScalarAsync<int>(insUnit, new { model.UnitName, ShortName = model.UnitName }, transaction);
                }
            }

            // ၁။ Product Master Update (AlertQuantity ထည့်သွင်းထားသည်)
            string updateSql = @"UPDATE Products SET Name=@Name, Sku=@Sku, CategoryId=@CategoryId, 
                               UnitId=@UnitId, CostPrice=@CostPrice, SalePrice=@SalePrice, 
                               AlertQuantity=@AlertQuantity WHERE ProductId=@id";

            await conn.ExecuteAsync(updateSql, new
            {
                model.Name,
                model.Sku,
                CategoryId = resolvedCategoryId,
                UnitId = resolvedUnitId,
                model.CostPrice,
                model.SalePrice,
                model.AlertQuantity,
                id
            }, transaction);

            // ၂။ Unit Conversions (Delete-then-Insert)
            await conn.ExecuteAsync("DELETE FROM UnitConversions WHERE ProductId = @id", new { id }, transaction);
            if (model.Conversions?.Any() == true)
            {
                string convSql = @"INSERT INTO UnitConversions (ProductId, FromUnitId, ToUnitId, Multiplier, BaseSalePrice) 
                                   VALUES (@id, @FromUnitId, @ToUnitId, @Multiplier, @BaseSalePrice)";
                foreach (var conv in model.Conversions)
                {
                    await conn.ExecuteAsync(convSql, new { id, conv.FromUnitId, conv.ToUnitId, conv.Multiplier, conv.BaseSalePrice }, transaction);
                }
            }

            // ၃။ Additional Prices (Delete-then-Insert)
            await conn.ExecuteAsync("DELETE FROM ProductPrices WHERE ProductId = @id", new { id }, transaction);
            if (model.AdditionalPrices?.Any() == true)
            {
                string priceSql = @"INSERT INTO ProductPrices (ProductId, PriceName, PriceValue, MinQuantity, UnitId) 
                                    VALUES (@id, @PriceName, @PriceValue, @MinQuantity, @UnitId)";
                foreach (var lp in model.AdditionalPrices)
                {
                    await conn.ExecuteAsync(priceSql, new
                    {
                        id,
                        PriceName = lp.PriceName,
                        PriceValue = lp.PriceValue,
                        MinQuantity = lp.MinQuantity,
                        UnitId = lp.UnitId
                    }, transaction);
                }
            }

            await transaction.CommitAsync();
            return Ok(new { message = "ပြင်ဆင်မှုများ အောင်မြင်စွာ သိမ်းဆည်းပြီးပါပြီ။" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = "SELECT * FROM Products WHERE ProductId = @id";
        var product = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { id });

        if (product == null) return NotFound();

        // ဈေးနှုန်းအဆင့်ဆင့်နှင့် ယူနစ်ပြောင်းလဲမှုများကိုပါ တစ်ခါတည်း ဆွဲယူပေးခြင်း
        product.AdditionalPrices = await conn.QueryAsync<dynamic>("SELECT * FROM ProductPrices WHERE ProductId = @id", new { id });
        product.Conversions = await conn.QueryAsync<dynamic>("SELECT * FROM UnitConversions WHERE ProductId = @id", new { id });

        return Ok(product);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            // ၁။ UnitConversions ကို အရင်ဖျက်သည်
            await conn.ExecuteAsync("DELETE FROM UnitConversions WHERE ProductId = @id", new { id }, transaction);

            // ၂။ Product ကို ဖျက်သည်
            var productExists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Products WHERE ProductId = @id",
                new { id },
                transaction);

            if (productExists == 0) return NotFound(new { message = "Product not found." });

            var usageCount = await conn.ExecuteScalarAsync<int>(@"
SELECT SUM(RefCount)
FROM (
    SELECT COUNT(*) AS RefCount FROM SaleItems WHERE ProductId = @id
    UNION ALL SELECT COUNT(*) FROM PurchaseOrderItems WHERE ProductId = @id
    UNION ALL SELECT COUNT(*) FROM SaleReturns WHERE ProductId = @id
    UNION ALL SELECT COUNT(*) FROM PurchaseReturns WHERE ProductId = @id
    UNION ALL SELECT COUNT(*) FROM StockAdjustments WHERE ProductId = @id
    UNION ALL SELECT COUNT(*) FROM StockTransferItems WHERE ProductId = @id
    UNION ALL SELECT COUNT(*) FROM BundleItems WHERE ComponentProductId = @id
    UNION ALL SELECT COUNT(*) FROM ConsignmentItems WHERE ProductId = @id
    UNION ALL SELECT COUNT(*) FROM ProductStocks WHERE ProductId = @id AND IFNULL(Quantity, 0) <> 0
) refs",
                new { id },
                transaction);

            if (usageCount > 0)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "This product already has stock, purchase, sale, return, adjustment, transfer, bundle, or consignment history. It cannot be deleted." });
            }

            await conn.ExecuteAsync("DELETE FROM ProductPrices WHERE ProductId = @id", new { id }, transaction);
            await conn.ExecuteAsync("DELETE FROM ProductStocks WHERE ProductId = @id AND IFNULL(Quantity, 0) = 0", new { id }, transaction);

            string sql = "DELETE FROM Products WHERE ProductId = @id";
            int affected = await conn.ExecuteAsync(sql, new { id }, transaction);

            if (affected == 0) return NotFound(new { message = "Product မတွေ့ပါ" });

            await transaction.CommitAsync();
            return Ok(new { message = "Product အောင်မြင်စွာ ဖျက်ပြီးပါပြီ။" });
        }
        catch (MySqlException ex) when (ex.Number == 1451) // Foreign Key Constraint Error
        {
            await transaction.RollbackAsync();
            // ဒီ Product ကို ရောင်းထားတာ (သို့မဟုတ်) ဝယ်ထားတာ ရှိနေရင် ဖျက်လို့မရပါဘူး
            return BadRequest(new { message = "ဤကုန်ပစ္စည်းကို အရောင်း/အဝယ် စာရင်းများတွင် သုံးထားသဖြင့် ဖျက်၍မရပါ။" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = ex.Message });
        }
    }


}
