using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Models.Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class InventoryController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public InventoryController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        private int? ResolveBranchScope(int? requestedBranchId)
        {
            if (User.IsInRole("Admin"))
            {
                return requestedBranchId;
            }

            var branchClaim = User.FindFirst("BranchId")?.Value;
            return int.TryParse(branchClaim, out var branchId) ? branchId : requestedBranchId;
        }

        private static async Task EnsureStockAdjustmentLossColumnsAsync(MySqlConnection conn)
        {
            var existingColumns = (await conn.QueryAsync<string>(@"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'StockAdjustments'
  AND COLUMN_NAME IN ('AdjustmentKind', 'LossReason', 'BaseQuantity', 'UnitCost', 'LossAmount')"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var additions = new List<string>();
            if (!existingColumns.Contains("AdjustmentKind")) additions.Add("ADD COLUMN AdjustmentKind VARCHAR(30) NOT NULL DEFAULT 'Correction'");
            if (!existingColumns.Contains("LossReason")) additions.Add("ADD COLUMN LossReason VARCHAR(30) NULL");
            if (!existingColumns.Contains("BaseQuantity")) additions.Add("ADD COLUMN BaseQuantity DECIMAL(18,4) NULL");
            if (!existingColumns.Contains("UnitCost")) additions.Add("ADD COLUMN UnitCost DECIMAL(18,4) NULL");
            if (!existingColumns.Contains("LossAmount")) additions.Add("ADD COLUMN LossAmount DECIMAL(18,4) NULL");

            if (additions.Count > 0)
            {
                await conn.ExecuteAsync($"ALTER TABLE StockAdjustments {string.Join(", ", additions)}");
            }
        }

        private static async Task EnsureSaleReturnsPaymentMethodColumnAsync(MySqlConnection conn, MySqlTransaction transaction)
        {
            var hasColumn = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'SaleReturns'
  AND COLUMN_NAME = 'ReturnPaymentMethod'", transaction: transaction);

            if (hasColumn == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE SaleReturns ADD COLUMN ReturnPaymentMethod VARCHAR(50) NULL", transaction: transaction);
            }
        }

        private static async Task ApplySaleReturnToCustomerCreditAsync(
            MySqlConnection conn,
            MySqlTransaction transaction,
            int saleId,
            decimal refundAmount)
        {
            if (refundAmount <= 0) return;

            var sale = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT
    s.CustomerId,
    IFNULL(s.DueAmount, 0) AS DueAmount,
    IFNULL((
        SELECT SUM(sr.RefundAmount)
        FROM SaleReturns sr
        WHERE sr.SaleId = s.SaleId
    ), 0) AS TotalReturnedAmount
FROM Sales s
WHERE s.SaleId = @saleId
FOR UPDATE",
                new { saleId }, transaction);

            if (sale?.CustomerId == null) return;

            decimal dueAmount = Convert.ToDecimal(sale.DueAmount);
            decimal totalReturnedAmount = Convert.ToDecimal(sale.TotalReturnedAmount);
            decimal returnedBeforeThisReturn = Math.Max(0m, totalReturnedAmount - refundAmount);
            decimal creditDueBeforeThisReturn = Math.Max(0m, dueAmount - returnedBeforeThisReturn);
            decimal creditReduction = Math.Min(refundAmount, creditDueBeforeThisReturn);

            if (creditReduction <= 0) return;

            await conn.ExecuteAsync(@"
UPDATE Customers
SET CurrentBalance = GREATEST(IFNULL(CurrentBalance, 0) - @creditReduction, 0)
WHERE CustomerId = @CustomerId",
                new { creditReduction, CustomerId = (int)sale.CustomerId }, transaction);
        }

        private static async Task<bool> HasRemainingSaleRefundCapacityAsync(
            MySqlConnection conn,
            MySqlTransaction transaction,
            int saleId,
            decimal refundAmount)
        {
            if (refundAmount <= 0) return true;

            var saleTotal = await conn.ExecuteScalarAsync<decimal?>(@"
SELECT IFNULL(TotalAmount, 0)
FROM Sales
WHERE SaleId = @SaleId
FOR UPDATE", new { SaleId = saleId }, transaction);

            if (saleTotal == null) return false;

            var returnedAmount = await conn.ExecuteScalarAsync<decimal>(@"
SELECT IFNULL(SUM(RefundAmount), 0)
FROM SaleReturns
WHERE SaleId = @SaleId", new { SaleId = saleId }, transaction);

            return returnedAmount + refundAmount <= saleTotal.Value;
        }

        private static async Task<bool> HasRecentMatchingSaleReturnAsync(
            MySqlConnection conn,
            MySqlTransaction transaction,
            int saleId,
            string itemType,
            int? productId,
            int? bundleId,
            int? serviceId,
            int? unitId,
            decimal quantity,
            decimal refundAmount,
            string? returnPaymentMethod)
        {
            var existing = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM SaleReturns
WHERE SaleId = @SaleId
  AND IFNULL(ItemType, 'Product') = IFNULL(@ItemType, 'Product')
  AND ProductId <=> @ProductId
  AND BundleId <=> @BundleId
  AND ServiceId <=> @ServiceId
  AND UnitId <=> @UnitId
  AND Quantity = @Quantity
  AND RefundAmount = @RefundAmount
  AND IFNULL(ReturnPaymentMethod, '') = IFNULL(@ReturnPaymentMethod, '')
  AND ReturnDate >= DATE_SUB(NOW(), INTERVAL 30 SECOND)",
                new
                {
                    SaleId = saleId,
                    ItemType = itemType,
                    ProductId = productId,
                    BundleId = bundleId,
                    ServiceId = serviceId,
                    UnitId = unitId,
                    Quantity = quantity,
                    RefundAmount = refundAmount,
                    ReturnPaymentMethod = returnPaymentMethod
                },
                transaction);

            return existing > 0;
        }

        private static bool HasExplicitPaymentMethod(string? method)
        {
            return !string.IsNullOrWhiteSpace(method)
                && !method.Trim().Equals("Unknown", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string> ResolveReturnPaymentMethodAsync(
            MySqlConnection conn,
            MySqlTransaction transaction,
            int saleId,
            string? preferredPaymentMethod)
        {
            if (HasExplicitPaymentMethod(preferredPaymentMethod))
            {
                return preferredPaymentMethod!.Trim();
            }

            var customerPaymentMethod = await conn.QueryFirstOrDefaultAsync<string>(@"
SELECT COALESCE(NULLIF(PaymentMethod, ''), 'Unknown')
FROM CustomerPayments
WHERE SaleId = @SaleId
GROUP BY COALESCE(NULLIF(PaymentMethod, ''), 'Unknown')
ORDER BY SUM(IFNULL(Amount, 0)) DESC
LIMIT 1",
                new { SaleId = saleId },
                transaction);

            if (HasExplicitPaymentMethod(customerPaymentMethod))
            {
                return customerPaymentMethod!.Trim();
            }

            var salePaymentMethod = await conn.QueryFirstOrDefaultAsync<string>(@"
SELECT COALESCE(NULLIF(PaymentMethod, ''), 'Unknown')
FROM Sales
WHERE SaleId = @SaleId",
                new { SaleId = saleId },
                transaction);

            return HasExplicitPaymentMethod(salePaymentMethod) ? salePaymentMethod!.Trim() : "Unknown";
        }

        private static int? ToNullableInt(object? value)
        {
            if (value == null || value is DBNull) return null;
            return Convert.ToInt32(value);
        }

        private static async Task EnsurePurchaseBatchRemainingColumnAsync(MySqlConnection conn, MySqlTransaction? transaction = null)
        {
            var hasColumn = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'PurchaseOrderItems'
  AND COLUMN_NAME = 'RemainingQuantity'",
                transaction: transaction);

            if (hasColumn == 0)
            {
                await conn.ExecuteAsync(
                    "ALTER TABLE PurchaseOrderItems ADD COLUMN RemainingQuantity DECIMAL(18,4) NULL",
                    transaction: transaction);
            }

            await conn.ExecuteAsync(@"
UPDATE PurchaseOrderItems poi
LEFT JOIN UnitConversions uc
  ON uc.ProductId = poi.ProductId
 AND uc.FromUnitId = poi.UnitId
SET poi.RemainingQuantity = (IFNULL(poi.Quantity, 0) + IFNULL(poi.FOCQuantity, 0)) * IFNULL(uc.Multiplier, 1)
WHERE poi.RemainingQuantity IS NULL",
                transaction: transaction);
        }

        private static async Task RestorePurchaseBatchesFefoAsync(
            MySqlConnection conn,
            MySqlTransaction transaction,
            int branchId,
            int productId,
            decimal baseQuantity)
        {
            if (baseQuantity <= 0) return;

            var batches = (await conn.QueryAsync<dynamic>(@"
SELECT
  poi.POItemId,
  IFNULL(poi.RemainingQuantity, 0) AS RemainingQuantity,
  ((IFNULL(poi.Quantity, 0) + IFNULL(poi.FOCQuantity, 0)) * IFNULL(uc.Multiplier, 1)) AS OriginalBaseQuantity
FROM PurchaseOrderItems poi
JOIN PurchaseOrders po ON po.POId = poi.POId
LEFT JOIN UnitConversions uc
  ON uc.ProductId = poi.ProductId
 AND uc.FromUnitId = poi.UnitId
WHERE po.BranchId = @BranchId
  AND poi.ProductId = @ProductId
  AND IFNULL(poi.RemainingQuantity, 0) < ((IFNULL(poi.Quantity, 0) + IFNULL(poi.FOCQuantity, 0)) * IFNULL(uc.Multiplier, 1))
ORDER BY
  CASE WHEN poi.ExpiryDate IS NULL THEN 1 ELSE 0 END,
  poi.ExpiryDate ASC,
  po.OrderDate ASC,
  poi.POItemId ASC
FOR UPDATE",
                new { BranchId = branchId, ProductId = productId },
                transaction)).ToList();

            var remaining = baseQuantity;
            foreach (var batch in batches)
            {
                if (remaining <= 0) break;

                var current = Convert.ToDecimal(batch.RemainingQuantity);
                var original = Convert.ToDecimal(batch.OriginalBaseQuantity);
                var capacity = Math.Max(0, original - current);
                var restore = Math.Min(capacity, remaining);
                if (restore <= 0) continue;

                await conn.ExecuteAsync(@"
UPDATE PurchaseOrderItems
SET RemainingQuantity = IFNULL(RemainingQuantity, 0) + @Restore
WHERE POItemId = @POItemId",
                    new { Restore = restore, POItemId = (int)batch.POItemId },
                    transaction);

                remaining -= restore;
            }
        }

        [HttpGet("stock-report/{branchId}")]
        public async Task<IActionResult> GetStockReport(int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            string sql = @"
            SELECT 
                p.ProductId, p.Name as ProductName, p.Sku, 
                s.Quantity as TotalBaseQuantity,
                u.UnitName as BaseUnitName
            FROM ProductStocks s
            JOIN Products p ON s.ProductId = p.ProductId
            JOIN Units u ON p.UnitId = u.UnitId
            WHERE s.BranchId = @branchId";

            var stocks = await conn.QueryAsync<dynamic>(sql, new { branchId });

            var report = new List<StockReportModel>();

            foreach (var stock in stocks)
            {
                var conversions = await conn.QueryAsync<dynamic>(
                    "SELECT u.UnitName, c.Multiplier FROM UnitConversions c JOIN Units u ON c.FromUnitId = u.UnitId WHERE c.ProductId = @ProductId ORDER BY c.Multiplier DESC",
                    new { stock.ProductId });

                decimal remaining = stock.TotalBaseQuantity;
                List<string> parts = new List<string>();

                foreach (var conv in conversions)
                {
                    if (remaining >= conv.Multiplier)
                    {
                        int count = (int)(remaining / conv.Multiplier);
                        remaining %= conv.Multiplier;
                        parts.Add($"{count} {conv.UnitName}");
                    }
                }

                if (remaining > 0 || parts.Count == 0)
                    parts.Add($"{remaining} {stock.BaseUnitName}");

                report.Add(new StockReportModel
                {
                    ProductId = stock.ProductId,
                    ProductName = stock.ProductName,
                    Sku = stock.Sku,
                    TotalBaseQuantity = stock.TotalBaseQuantity,
                    FormattedStock = string.Join(", ", parts)
                });
            }

            return Ok(report);
        }

        [HttpGet("branch/{branchId}")]
        public async Task<IActionResult> GetStockByBranch(int branchId, [FromQuery] int? supplierId = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            string sql = @"
    SELECT 
        ps.ProductId, p.Name AS ProductName, p.SKU, ps.Quantity AS BaseQuantity,
        IFNULL((
            SELECT SUM(ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity)
              FROM ConsignmentItems ci
              JOIN Consignments c ON c.ConsignId = ci.ConsignId
              WHERE ci.ProductId = ps.ProductId
                AND COALESCE(ci.BranchId, c.BranchId) = ps.BranchId
              AND LOWER(TRIM(IFNULL(c.Status, ''))) NOT IN ('cancelled', 'canceled', 'returned')
        ), 0) AS ConsignmentQuantity,
        (ps.Quantity - IFNULL((
            SELECT SUM(ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity)
              FROM ConsignmentItems ci
              JOIN Consignments c ON c.ConsignId = ci.ConsignId
              WHERE ci.ProductId = ps.ProductId
                AND COALESCE(ci.BranchId, c.BranchId) = ps.BranchId
              AND LOWER(TRIM(IFNULL(c.Status, ''))) NOT IN ('cancelled', 'canceled', 'returned')
        ), 0)) AS OwnedQuantity,
        u.UnitName AS BaseUnitName, IFNULL(p.CostPrice, 0) AS CostPrice,
        (SELECT poi.CostPrice FROM PurchaseOrderItems poi 
         WHERE poi.ProductId = ps.ProductId 
         ORDER BY poi.POItemId DESC LIMIT 1) AS LastPurchasePrice,
        IFNULL(p.SalePrice, 0) AS SalePrice, 
        IFNULL(p.AlertQuantity, 0) AS AlertQuantity
    FROM ProductStocks ps
    JOIN Products p ON ps.ProductId = p.ProductId
    LEFT JOIN Units u ON p.UnitId = u.UnitId
    WHERE ps.BranchId = @branchId 
    AND (@supplierId IS NULL OR ps.ProductId IN (
        SELECT DISTINCT poi2.ProductId 
        FROM PurchaseOrderItems poi2 
        JOIN PurchaseOrders po ON poi2.POId = po.POId 
        WHERE po.SupplierId = @supplierId
    ));";

            var stocks = await conn.QueryAsync<dynamic>(sql, new { branchId, supplierId });
            return Ok(stocks);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllStock([FromQuery] int? supplierId = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            string sql = @"
    SELECT  
        ps.ProductId,
        p.Name AS ProductName,
        p.SKU,
        SUM(ps.Quantity) AS BaseQuantity, 
        IFNULL((
            SELECT SUM(ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity)
            FROM ConsignmentItems ci
            JOIN Consignments c ON c.ConsignId = ci.ConsignId
            WHERE ci.ProductId = ps.ProductId
              AND LOWER(TRIM(IFNULL(c.Status, ''))) NOT IN ('cancelled', 'canceled', 'returned')
        ), 0) AS ConsignmentQuantity,
        (SUM(ps.Quantity) - IFNULL((
            SELECT SUM(ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity)
            FROM ConsignmentItems ci
            JOIN Consignments c ON c.ConsignId = ci.ConsignId
            WHERE ci.ProductId = ps.ProductId
              AND LOWER(TRIM(IFNULL(c.Status, ''))) NOT IN ('cancelled', 'canceled', 'returned')
        ), 0)) AS OwnedQuantity,
        u.UnitName AS BaseUnitName,
        IFNULL(p.CostPrice, 0) AS CostPrice,
        (SELECT poi.CostPrice FROM PurchaseOrderItems poi 
         WHERE poi.ProductId = ps.ProductId 
         ORDER BY poi.POItemId DESC LIMIT 1) AS LastPurchasePrice,
        IFNULL(p.SalePrice, 0) AS SalePrice,
        IFNULL(p.AlertQuantity, 0) AS AlertQuantity
    FROM ProductStocks ps
    JOIN Products p ON ps.ProductId = p.ProductId
    LEFT JOIN Units u ON p.UnitId = u.UnitId
    WHERE (@supplierId IS NULL OR ps.ProductId IN (
        SELECT DISTINCT poi2.ProductId 
        FROM PurchaseOrderItems poi2 
        JOIN PurchaseOrders po ON poi2.POId = po.POId 
        WHERE po.SupplierId = @supplierId
    ))
    GROUP BY ps.ProductId, p.Name, p.SKU, u.UnitName, p.CostPrice, p.SalePrice, p.AlertQuantity";

            try
            {
                var stocks = await conn.QueryAsync<dynamic>(sql, new { supplierId });
                return Ok(stocks);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Global Inventory Filter Error: " + ex.Message });
            }
        }

        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStock(int? branchId = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            string sql = @"
                SELECT p.ProductId, p.Name as ProductName, SUM(ps.Quantity) as BaseQuantity, 
                       u.UnitName as BaseUnitName, p.AlertQuantity
                FROM ProductStocks ps
                JOIN Products p ON ps.ProductId = p.ProductId
                JOIN Units u ON p.UnitId = u.UnitId
                WHERE (@branchId IS NULL OR ps.BranchId = @branchId)
                GROUP BY p.ProductId, p.Name, u.UnitName, p.AlertQuantity
                HAVING SUM(ps.Quantity) <= p.AlertQuantity AND p.AlertQuantity > 0";

            var items = await conn.QueryAsync<dynamic>(sql, new { branchId });
            return Ok(items);
        }

        [HttpPost("adjust")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> AdjustStock([FromBody] StockAdjustmentModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            await EnsureStockAdjustmentLossColumnsAsync(conn);
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                if (model.Quantity <= 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Return quantity must be greater than zero." });
                }

                if (!string.Equals(model.Type, "Addition", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(model.Type, "Subtraction", StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Adjustment type must be Addition or Subtraction." });
                }

                var isInventoryLoss = string.Equals(model.AdjustmentKind, "InventoryLoss", StringComparison.OrdinalIgnoreCase);
                var allowedLossReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Damaged", "Destroyed", "Expired", "Lost", "Theft", "Other"
                };
                if (isInventoryLoss && !allowedLossReasons.Contains(model.LossReason ?? string.Empty))
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Select a valid inventory loss reason." });
                }

                var effectiveBranchId = ResolveBranchScope(model.BranchId);
                if (!effectiveBranchId.HasValue)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "A branch is required." });
                }

                var conv = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                    new { model.ProductId, model.UnitId }, transaction);

                decimal multiplier = conv?.Multiplier ?? 1;
                decimal baseAdjustmentQty = model.Quantity * multiplier;
                var isAddition = string.Equals(model.Type, "Addition", StringComparison.OrdinalIgnoreCase) && !isInventoryLoss;
                decimal finalQtyToUpdate = isAddition ? baseAdjustmentQty : -baseAdjustmentQty;

                var stock = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT ps.Quantity, IFNULL(p.CostPrice, 0) AS UnitCost
FROM ProductStocks ps
JOIN Products p ON p.ProductId = ps.ProductId
WHERE ps.BranchId = @BranchId AND ps.ProductId = @ProductId
FOR UPDATE", new { BranchId = effectiveBranchId.Value, model.ProductId }, transaction);

                if (stock == null)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "This product does not have stock at the selected branch." });
                }

                var currentStock = Convert.ToDecimal(stock.Quantity);
                if (!isAddition && baseAdjustmentQty > currentStock)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = $"Only {currentStock:0.####} base units are available." });
                }

                decimal? unitCost = isInventoryLoss ? Convert.ToDecimal(stock.UnitCost) : null;
                decimal? lossAmount = isInventoryLoss ? baseAdjustmentQty * unitCost.GetValueOrDefault() : null;
                var adjustmentKind = isInventoryLoss ? "InventoryLoss" : "Correction";
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var effectiveUserId = int.TryParse(userIdClaim, out var authenticatedUserId)
                    ? authenticatedUserId
                    : model.UserId;

                string adjSql = @"INSERT INTO StockAdjustments
                          (BranchId, ProductId, Type, Quantity, UnitId, Reason, AdjustmentDate, UserId,
                           AdjustmentKind, LossReason, BaseQuantity, UnitCost, LossAmount)
                          VALUES
                          (@BranchId, @ProductId, @Type, @Quantity, @UnitId, @Reason, NOW(), @UserId,
                           @AdjustmentKind, @LossReason, @BaseQuantity, @UnitCost, @LossAmount)";

                await conn.ExecuteAsync(adjSql, new
                {
                    BranchId = effectiveBranchId.Value,
                    model.ProductId,
                    Type = isAddition ? "Addition" : "Subtraction",
                    model.Quantity,
                    model.UnitId,
                    model.Reason,
                    UserId = effectiveUserId,
                    AdjustmentKind = adjustmentKind,
                    LossReason = isInventoryLoss ? model.LossReason : null,
                    BaseQuantity = baseAdjustmentQty,
                    UnitCost = unitCost,
                    LossAmount = lossAmount
                }, transaction);

                string stockUpdate = @"UPDATE ProductStocks 
                                SET Quantity = Quantity + @finalQtyToUpdate 
                                WHERE BranchId = @BranchId AND ProductId = @ProductId";

                await conn.ExecuteAsync(stockUpdate, new
                {
                    finalQtyToUpdate,
                    BranchId = effectiveBranchId.Value,
                    model.ProductId
                }, transaction);

                await transaction.CommitAsync();
                return Ok(new
                {
                    Message = isInventoryLoss ? "Inventory loss recorded successfully." : "Stock adjustment completed successfully.",
                    LossAmount = lossAmount ?? 0m
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Adjustment Error: " + ex.Message });
            }
        }

        [HttpPost("full-sale-return/{saleId}")]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> FullSaleReturn(int saleId, [FromQuery] string reason = "Full Voucher Return")
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsureBundleItemsSupportsServicesAsync(conn, (MySqlTransaction)transaction);
                await EnsureSaleReturnsSupportsItemTypesAsync(conn, (MySqlTransaction)transaction);
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);

                var saleMaster = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT BranchId, TotalAmount FROM Sales WHERE SaleId = @saleId FOR UPDATE", new { saleId }, transaction);
                if (saleMaster == null) return BadRequest(new { Message = "Voucher not found." });

                string getItemsSql = "SELECT * FROM SaleItems WHERE SaleId = @saleId FOR UPDATE";
                var items = (await conn.QueryAsync<dynamic>(getItemsSql, new { saleId }, transaction)).ToList();

                if (!items.Any()) return BadRequest(new { Message = "Voucher ထဲတွင် ပစ္စည်းများ ရှာမတွေ့ပါ။" });

                decimal fullReturnAmount = 0m;
                foreach (var item in items)
                {
                    fullReturnAmount += Convert.ToDecimal(item.SubTotal ?? 0m);
                }

                if (!await HasRemainingSaleRefundCapacityAsync(conn, (MySqlTransaction)transaction, saleId, fullReturnAmount))
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Return amount exceeds remaining sale amount." });
                }

                var fullReturnPaymentMethod = await ResolveReturnPaymentMethodAsync(
                    conn,
                    (MySqlTransaction)transaction,
                    saleId,
                    null);

                foreach (var item in items)
                {
                    string itemType = Convert.ToString(item.ItemType ?? "Product") ?? "Product";
                    int? productId = ToNullableInt(item.ProductId);
                    int? bundleId = ToNullableInt(item.BundleId);
                    int? serviceId = ToNullableInt(item.ServiceId);
                    int? unitId = ToNullableInt(item.UnitId);
                    decimal quantity = Convert.ToDecimal(item.Quantity ?? 0m);
                    decimal refundAmount = Convert.ToDecimal(item.SubTotal ?? 0m);
                    int branchId = Convert.ToInt32(saleMaster.BranchId);

                    if (string.Equals(itemType, "Product", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!productId.HasValue)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { Message = "Product sale line is missing ProductId." });
                        }

                        decimal multiplier = 1;
                        if (unitId.HasValue)
                        {
                            multiplier = await conn.QueryFirstOrDefaultAsync<decimal?>(
                                "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                                new { ProductId = productId.Value, UnitId = unitId.Value }, transaction) ?? 1;
                            if (multiplier <= 0) multiplier = 1;
                        }

                        decimal baseQtyToReturn = quantity * multiplier;
                        await RestorePurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, branchId, productId.Value, baseQtyToReturn);

                        string stockUpdate = @"UPDATE ProductStocks SET Quantity = Quantity + @baseQtyToReturn 
                                   WHERE BranchId = @BranchId AND ProductId = @ProductId";
                        await conn.ExecuteAsync(stockUpdate, new
                        {
                            baseQtyToReturn,
                            BranchId = branchId,
                            ProductId = productId.Value
                        }, transaction);
                    }
                    else if (string.Equals(itemType, "Bundle", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!bundleId.HasValue)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { Message = "Bundle sale line is missing BundleId." });
                        }

                        var components = await conn.QueryAsync<dynamic>(
                            @"SELECT bi.ComponentProductId, bi.Quantity, IFNULL(uc.Multiplier, 1) as UnitMultiplier
                              FROM BundleItems bi
                              LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                              WHERE bi.BundleId = @BundleId
                                AND bi.ComponentProductId IS NOT NULL
                                AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'",
                            new { BundleId = bundleId.Value }, transaction);

                        foreach (var comp in components)
                        {
                            decimal compQty = Convert.ToDecimal(comp.Quantity) * Convert.ToDecimal(comp.UnitMultiplier) * quantity;
                            int componentProductId = Convert.ToInt32(comp.ComponentProductId);
                            await RestorePurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, branchId, componentProductId, compQty);
                            await conn.ExecuteAsync(
                                "UPDATE ProductStocks SET Quantity = Quantity + @qty WHERE BranchId = @BranchId AND ProductId = @ProductId",
                                new { qty = compQty, BranchId = branchId, ProductId = componentProductId }, transaction);
                        }
                    }

                    string returnSql = @"
INSERT INTO SaleReturns (SaleId, ItemType, ProductId, BundleId, ServiceId, BranchId, Quantity, UnitId, RefundAmount, Reason, ReturnPaymentMethod, ReturnDate) 
VALUES (@saleId, @ItemType, @ProductId, @BundleId, @ServiceId, @BranchId, @Quantity, @UnitId, @RefundAmount, @Reason, @ReturnPaymentMethod, NOW())";

                    await conn.ExecuteAsync(returnSql, new
                    {
                        saleId,
                        ItemType = itemType,
                        ProductId = productId,
                        BundleId = bundleId,
                        ServiceId = serviceId,
                        BranchId = branchId,
                        Quantity = quantity,
                        UnitId = unitId,
                        RefundAmount = refundAmount,
                        Reason = reason,
                        ReturnPaymentMethod = fullReturnPaymentMethod
                    }, transaction);
                }

                // Reduce customer credit while the original due amount is still available.
                await ApplySaleReturnToCustomerCreditAsync(conn, (MySqlTransaction)transaction, saleId, fullReturnAmount);
                await conn.ExecuteAsync("DELETE FROM SaleItems WHERE SaleId = @saleId", new { saleId }, transaction);
                await conn.ExecuteAsync("UPDATE Sales SET TotalAmount = 0, DiscountAmount = 0, DeliveryFee = 0, DueAmount = 0 WHERE SaleId = @saleId", new { saleId }, transaction);

                await transaction.CommitAsync();
                return Ok(new { Message = "Voucher တစ်စောင်လုံး Return ပြန်ခြင်း အောင်မြင်ပါသည်။ ပစ္စည်းအားလုံး Stock ထဲ ပြန်ရောက်သွားပါပြီ။" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Error: " + ex.Message });
            }
        }

        [HttpPost("purchase-return")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> PurchaseReturn([FromBody] PurchaseReturnModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);

                var conv = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                    new { model.ProductId, model.UnitId }, transaction);

                decimal multiplier = conv?.Multiplier ?? 1;
                decimal baseQtyToDeduct = model.Quantity * multiplier;

                var purchaseItem = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT poi.POItemId, poi.Quantity, po.BranchId
FROM PurchaseOrderItems poi
JOIN PurchaseOrders po ON po.POId = poi.POId
WHERE (@POItemId IS NULL OR poi.POItemId = @POItemId)
  AND poi.POId = @POId
  AND poi.ProductId = @ProductId
  AND poi.UnitId = @UnitId
  AND po.BranchId = @BranchId
  AND IFNULL(poi.Quantity, 0) >= @Quantity
ORDER BY poi.POItemId ASC
LIMIT 1
FOR UPDATE",
                    new { model.POItemId, model.POId, model.ProductId, model.UnitId, model.BranchId, model.Quantity }, transaction);

                if (purchaseItem == null)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Purchase batch not found or return quantity exceeds remaining quantity." });
                }

                string checkStockSql = "SELECT Quantity FROM ProductStocks WHERE BranchId = @BranchId AND ProductId = @ProductId";
                decimal currentStock = await conn.ExecuteScalarAsync<decimal>(checkStockSql, new { model.BranchId, model.ProductId }, transaction);

                if (currentStock < baseQtyToDeduct)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = $"လက်ကျန်မလုံလောက်ပါ။ လက်ရှိလက်ကျန်: {currentStock} ခုသာ ရှိပါသည်။" });
                }

                string returnSql = @"INSERT INTO PurchaseReturns (POId, ProductId, BranchId, Quantity, UnitId, RefundAmount, Reason, ReturnDate) 
                             VALUES (@POId, @ProductId, @BranchId, @Quantity, @UnitId, @RefundAmount, @Reason, NOW())";
                await conn.ExecuteAsync(returnSql, model, transaction);

                string stockUpdate = "UPDATE ProductStocks SET Quantity = Quantity - @baseQtyToDeduct WHERE BranchId = @BranchId AND ProductId = @ProductId";
                await conn.ExecuteAsync(stockUpdate, new { baseQtyToDeduct, model.BranchId, model.ProductId }, transaction);

                string poItemUpdate = @"
UPDATE PurchaseOrderItems
SET Quantity = Quantity - @Quantity,
    RemainingQuantity = GREATEST(IFNULL(RemainingQuantity, 0) - @baseQtyToDeduct, 0)
WHERE POItemId = @POItemId
  AND Quantity >= @Quantity";
                var updatedPurchaseRows = await conn.ExecuteAsync(poItemUpdate, new
                {
                    model.Quantity,
                    baseQtyToDeduct,
                    POItemId = (int)purchaseItem.POItemId
                }, transaction);

                if (updatedPurchaseRows != 1)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Purchase batch was changed by another operation. Please refresh and try again." });
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Purchase return successful." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Error: " + ex.Message });
            }
        }

        [HttpPost("sale-return")]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> SaleReturn([FromBody] SaleReturnModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsureSaleReturnsPaymentMethodColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);

                var originalItem = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Quantity, UnitId FROM SaleItems WHERE SaleId = @SaleId AND ProductId = @ProductId FOR UPDATE",
                    new { model.SaleId, model.ProductId }, transaction);

                if (originalItem == null) throw new Exception("မူရင်းအရောင်းစာရင်း ရှာမတွေ့ပါ။");

                var originalConv = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                    new { model.ProductId, UnitId = (int)originalItem.UnitId }, transaction);

                decimal originalMultiplier = originalConv?.Multiplier ?? 1;
                decimal totalBaseQtySold = (decimal)originalItem.Quantity * originalMultiplier;

                var returnConv = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                    new { model.ProductId, model.UnitId }, transaction);

                decimal returnMultiplier = returnConv?.Multiplier ?? 1;
                decimal baseQtyReturned = model.Quantity * returnMultiplier;

                decimal remainingBaseQty = totalBaseQtySold - baseQtyReturned;
                if (remainingBaseQty < 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Return qty exceeds remaining sold qty." });
                }

                if (!await HasRemainingSaleRefundCapacityAsync(conn, (MySqlTransaction)transaction, model.SaleId, model.RefundAmount))
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Return amount exceeds remaining sale amount." });
                }

                var legacyReturnPaymentMethod = await ResolveReturnPaymentMethodAsync(
                    conn,
                    (MySqlTransaction)transaction,
                    model.SaleId,
                    model.PreferredPaymentMethod);
                if (await HasRecentMatchingSaleReturnAsync(
                    conn,
                    (MySqlTransaction)transaction,
                    model.SaleId,
                    "Product",
                    model.ProductId,
                    null,
                    null,
                    model.UnitId,
                    model.Quantity,
                    model.RefundAmount,
                    legacyReturnPaymentMethod))
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Duplicate return request ignored. Please refresh the voucher." });
                }

                string returnSql = @"INSERT INTO SaleReturns (SaleId, ProductId, BranchId, Quantity, UnitId, RefundAmount, Reason, ReturnPaymentMethod, ReturnDate) 
                             VALUES (@SaleId, @ProductId, @BranchId, @Quantity, @UnitId, @RefundAmount, @Reason, @ReturnPaymentMethod, NOW())";
                await conn.ExecuteAsync(returnSql, new
                {
                    model.SaleId,
                    model.ProductId,
                    model.BranchId,
                    model.Quantity,
                    model.UnitId,
                    model.RefundAmount,
                    model.Reason,
                    ReturnPaymentMethod = legacyReturnPaymentMethod
                }, transaction);

                string stockUpdate = @"UPDATE ProductStocks SET Quantity = Quantity + @baseQtyReturned 
                               WHERE BranchId = @BranchId AND ProductId = @ProductId";
                await RestorePurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, model.BranchId, model.ProductId, baseQtyReturned);
                await conn.ExecuteAsync(stockUpdate, new { baseQtyReturned, model.BranchId, model.ProductId }, transaction);

                string saleItemUpdate = @"UPDATE SaleItems 
                                  SET Quantity = @remainingBaseQty, 
                                      UnitId = @UnitId,
                                      SubTotal = GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)) - @RefundAmount)
                                  WHERE SaleId = @SaleId AND ProductId = @ProductId";

                await conn.ExecuteAsync(saleItemUpdate, new
                {
                    remainingBaseQty,
                    model.UnitId,
                    model.SaleId,
                    model.ProductId,
                    model.RefundAmount
                }, transaction);

                await ApplySaleReturnToCustomerCreditAsync(conn, (MySqlTransaction)transaction, model.SaleId, model.RefundAmount);

                await transaction.CommitAsync();
                return Ok(new { Message = "Sale return successful. Units adjusted to base format." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Sale Return Error: " + ex.Message });
            }
        }

        private static async Task EnsureBundleItemsSupportsServicesAsync(MySqlConnection conn, MySqlTransaction transaction)
        {
            var hasItemType = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'ItemType'",
                transaction: transaction);

            if (hasItemType == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN ItemType VARCHAR(20) NOT NULL DEFAULT 'Product'", transaction: transaction);
            }

            var hasServiceId = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'ServiceId'",
                transaction: transaction);

            if (hasServiceId == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN ServiceId INT NULL", transaction: transaction);
            }

            var hasUnitId = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'UnitId'",
                transaction: transaction);

            if (hasUnitId == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN UnitId INT NULL", transaction: transaction);
            }

            try
            {
                await conn.ExecuteAsync("ALTER TABLE BundleItems MODIFY COLUMN ComponentProductId INT NULL", transaction: transaction);
            }
            catch
            {
            }
        }

        private static async Task EnsureSaleReturnsSupportsItemTypesAsync(MySqlConnection conn, MySqlTransaction transaction)
        {
            var hasItemType = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'SaleReturns'
  AND COLUMN_NAME = 'ItemType'",
                transaction: transaction);

            if (hasItemType == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE SaleReturns ADD COLUMN ItemType VARCHAR(20) NOT NULL DEFAULT 'Product'", transaction: transaction);
            }

            var hasBundleId = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'SaleReturns'
  AND COLUMN_NAME = 'BundleId'",
                transaction: transaction);

            if (hasBundleId == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE SaleReturns ADD COLUMN BundleId INT NULL", transaction: transaction);
            }

            var hasServiceId = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'SaleReturns'
  AND COLUMN_NAME = 'ServiceId'",
                transaction: transaction);

            if (hasServiceId == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE SaleReturns ADD COLUMN ServiceId INT NULL", transaction: transaction);
            }

            try
            {
                await conn.ExecuteAsync("ALTER TABLE SaleReturns MODIFY COLUMN ProductId INT NULL", transaction: transaction);
            }
            catch
            {
            }

            var hasReturnPaymentMethod = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'SaleReturns'
  AND COLUMN_NAME = 'ReturnPaymentMethod'", transaction: transaction);

            if (hasReturnPaymentMethod == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE SaleReturns ADD COLUMN ReturnPaymentMethod VARCHAR(50) NULL", transaction: transaction);
            }
        }

        public class SaleReturnAnyModel
        {
            public int SaleId { get; set; }
            public int BranchId { get; set; }
            public string ItemType { get; set; } = "Product"; // Product | Service | Bundle
            public int? ProductId { get; set; }
            public int? ServiceId { get; set; }
            public int? BundleId { get; set; }
            public decimal Quantity { get; set; }
            public int? UnitId { get; set; }
            public decimal RefundAmount { get; set; }
            public string? Reason { get; set; }
            public string? PreferredPaymentMethod { get; set; }
        }

        [HttpGet("sale-payment-methods/{saleId}")]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> GetSalePaymentMethods(int saleId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            var customerPayments = (await conn.QueryAsync<dynamic>(@"
SELECT 
    COALESCE(NULLIF(PaymentMethod, ''), 'Unknown') AS Method,
    IFNULL(SUM(Amount), 0) AS Amount
FROM CustomerPayments
WHERE SaleId = @saleId
GROUP BY COALESCE(NULLIF(PaymentMethod, ''), 'Unknown')
ORDER BY Amount DESC", new { saleId })).ToList();

            if (customerPayments.Count > 0)
            {
                return Ok(customerPayments.Select(x => new
                {
                    Method = (string)x.Method,
                    Amount = (decimal)x.Amount
                }));
            }

            var sale = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT 
    COALESCE(NULLIF(PaymentMethod, ''), 'Unknown') AS Method,
    IFNULL(PaidAmount, 0) AS Amount
FROM Sales
WHERE SaleId = @saleId", new { saleId });

            if (sale is null)
            {
                return NotFound(new { Message = "Sale not found." });
            }

            return Ok(new[]
            {
                new { Method = (string)sale.Method, Amount = (decimal)sale.Amount }
            });
        }

        [HttpPost("sale-return-any")]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> SaleReturnAny([FromBody] SaleReturnAnyModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsureBundleItemsSupportsServicesAsync(conn, (MySqlTransaction)transaction);
                await EnsureSaleReturnsSupportsItemTypesAsync(conn, (MySqlTransaction)transaction);
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);

                var itemType = string.IsNullOrWhiteSpace(model.ItemType) ? "Product" : model.ItemType.Trim();
                if (model.Quantity <= 0) return BadRequest(new { Message = "Invalid Quantity." });
                decimal appliedRefundAmount = 0m;

                if (itemType.Equals("Product", StringComparison.OrdinalIgnoreCase))
                {
                    if (!model.ProductId.HasValue) return BadRequest(new { Message = "ProductId is required." });
                    if (!model.UnitId.HasValue) return BadRequest(new { Message = "UnitId is required." });

                    var originalItem = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT Quantity, UnitId, IFNULL(IsFOC, 0) AS IsFOC FROM SaleItems WHERE SaleId = @SaleId AND ProductId = @ProductId AND ItemType = 'Product' FOR UPDATE",
                        new { model.SaleId, model.ProductId }, transaction);
                    if (originalItem == null) return BadRequest(new { Message = "Voucher item not found." });

                    if ((decimal)originalItem.Quantity < model.Quantity) return BadRequest(new { Message = "Return qty exceeds sold qty." });
                    appliedRefundAmount = Convert.ToBoolean(originalItem.IsFOC) ? 0m : Math.Max(0m, model.RefundAmount);

                    var originalConv = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                        new { ProductId = model.ProductId.Value, UnitId = (int)originalItem.UnitId }, transaction);

                    decimal originalMultiplier = originalConv?.Multiplier ?? 1;
                    decimal totalBaseQtySold = (decimal)originalItem.Quantity * originalMultiplier;

                    var returnConv = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                        new { ProductId = model.ProductId.Value, UnitId = model.UnitId.Value }, transaction);

                    decimal returnMultiplier = returnConv?.Multiplier ?? 1;
                    decimal baseQtyReturned = model.Quantity * returnMultiplier;
                    decimal remainingBaseQty = totalBaseQtySold - baseQtyReturned;
                    if (remainingBaseQty < 0)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { Message = "Return qty exceeds remaining sold qty." });
                    }

                    if (!await HasRemainingSaleRefundCapacityAsync(conn, (MySqlTransaction)transaction, model.SaleId, appliedRefundAmount))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { Message = "Return amount exceeds remaining sale amount." });
                    }

                    var productReturnPaymentMethod = await ResolveReturnPaymentMethodAsync(
                        conn,
                        (MySqlTransaction)transaction,
                        model.SaleId,
                        model.PreferredPaymentMethod);
                    if (await HasRecentMatchingSaleReturnAsync(
                        conn,
                        (MySqlTransaction)transaction,
                        model.SaleId,
                        "Product",
                        model.ProductId.Value,
                        null,
                        null,
                        model.UnitId.Value,
                        model.Quantity,
                        appliedRefundAmount,
                        productReturnPaymentMethod))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { Message = "Duplicate return request ignored. Please refresh the voucher." });
                    }

                    await conn.ExecuteAsync(@"
INSERT INTO SaleReturns (SaleId, ItemType, ProductId, BundleId, ServiceId, BranchId, Quantity, UnitId, RefundAmount, Reason, ReturnPaymentMethod, ReturnDate)
VALUES (@SaleId, 'Product', @ProductId, NULL, NULL, @BranchId, @Quantity, @UnitId, @RefundAmount, @Reason, @ReturnPaymentMethod, NOW())",
                        new
                        {
                            model.SaleId,
                            ProductId = model.ProductId.Value,
                            model.BranchId,
                            model.Quantity,
                            UnitId = model.UnitId.Value,
                            RefundAmount = appliedRefundAmount,
                            Reason = model.Reason ?? "Customer Return",
                            ReturnPaymentMethod = productReturnPaymentMethod
                        }, transaction);

                    await conn.ExecuteAsync(
                        @"UPDATE ProductStocks SET Quantity = Quantity + @baseQtyReturned WHERE BranchId = @BranchId AND ProductId = @ProductId",
                        new { baseQtyReturned, model.BranchId, ProductId = model.ProductId.Value }, transaction);
                    await RestorePurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, model.BranchId, model.ProductId.Value, baseQtyReturned);

                    await conn.ExecuteAsync(
                        @"UPDATE SaleItems 
                          SET Quantity = @remainingBaseQty, 
                              UnitId = @UnitId,
                              SubTotal = GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)) - @RefundAmount)
                          WHERE SaleId = @SaleId AND ProductId = @ProductId AND ItemType = 'Product'",
                        new { remainingBaseQty, UnitId = model.UnitId.Value, model.SaleId, ProductId = model.ProductId.Value, RefundAmount = appliedRefundAmount }, transaction);
                }
                else if (itemType.Equals("Bundle", StringComparison.OrdinalIgnoreCase))
                {
                    if (!model.BundleId.HasValue) return BadRequest(new { Message = "BundleId is required." });

                    var originalItem = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT Quantity, IFNULL(IsFOC, 0) AS IsFOC FROM SaleItems WHERE SaleId = @SaleId AND BundleId = @BundleId AND ItemType = 'Bundle' FOR UPDATE",
                        new { model.SaleId, model.BundleId }, transaction);
                    if (originalItem == null) return BadRequest(new { Message = "Bundle voucher item not found." });

                    decimal soldQty = (decimal)originalItem.Quantity;
                    if (soldQty < model.Quantity) return BadRequest(new { Message = "Return qty exceeds sold qty." });
                    appliedRefundAmount = Convert.ToBoolean(originalItem.IsFOC) ? 0m : Math.Max(0m, model.RefundAmount);

                    if (!await HasRemainingSaleRefundCapacityAsync(conn, (MySqlTransaction)transaction, model.SaleId, appliedRefundAmount))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { Message = "Return amount exceeds remaining sale amount." });
                    }

                    var bundleReturnPaymentMethod = await ResolveReturnPaymentMethodAsync(
                        conn,
                        (MySqlTransaction)transaction,
                        model.SaleId,
                        model.PreferredPaymentMethod);
                    if (await HasRecentMatchingSaleReturnAsync(
                        conn,
                        (MySqlTransaction)transaction,
                        model.SaleId,
                        "Bundle",
                        null,
                        model.BundleId.Value,
                        null,
                        null,
                        model.Quantity,
                        appliedRefundAmount,
                        bundleReturnPaymentMethod))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { Message = "Duplicate return request ignored. Please refresh the voucher." });
                    }

                    // Restock component products (services have no stock).
                    var components = await conn.QueryAsync<dynamic>(
                        @"SELECT bi.ComponentProductId, bi.Quantity, IFNULL(uc.Multiplier, 1) as UnitMultiplier FROM BundleItems bi
                          LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                          WHERE BundleId = @BundleId 
                            AND bi.ComponentProductId IS NOT NULL 
                            AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'",
                        new { BundleId = model.BundleId.Value }, transaction);

                    foreach (var comp in components)
                    {
                        decimal compQty = Convert.ToDecimal(comp.Quantity) * Convert.ToDecimal(comp.UnitMultiplier) * model.Quantity;
                        await conn.ExecuteAsync(
                            "UPDATE ProductStocks SET Quantity = Quantity + @qty WHERE BranchId = @BranchId AND ProductId = @ProductId",
                            new { qty = compQty, model.BranchId, ProductId = (int)comp.ComponentProductId }, transaction);
                        await RestorePurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, model.BranchId, (int)comp.ComponentProductId, compQty);
                    }

                    await conn.ExecuteAsync(@"
INSERT INTO SaleReturns (SaleId, ItemType, ProductId, BundleId, ServiceId, BranchId, Quantity, UnitId, RefundAmount, Reason, ReturnPaymentMethod, ReturnDate)
VALUES (@SaleId, 'Bundle', NULL, @BundleId, NULL, @BranchId, @Quantity, NULL, @RefundAmount, @Reason, @ReturnPaymentMethod, NOW())",
                        new
                        {
                            model.SaleId,
                            BundleId = model.BundleId.Value,
                            model.BranchId,
                            model.Quantity,
                            RefundAmount = appliedRefundAmount,
                            Reason = model.Reason ?? "Customer Return",
                            ReturnPaymentMethod = bundleReturnPaymentMethod
                        }, transaction);

                    await conn.ExecuteAsync(
                        @"UPDATE SaleItems 
                          SET Quantity = Quantity - @qty,
                              SubTotal = GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)) - @RefundAmount)
                          WHERE SaleId = @SaleId AND BundleId = @BundleId AND ItemType = 'Bundle'",
                        new { qty = model.Quantity, model.SaleId, BundleId = model.BundleId.Value, RefundAmount = appliedRefundAmount }, transaction);
                }
                else if (itemType.Equals("Service", StringComparison.OrdinalIgnoreCase))
                {
                    if (!model.ServiceId.HasValue) return BadRequest(new { Message = "ServiceId is required." });

                    var originalItem = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT Quantity, IFNULL(IsFOC, 0) AS IsFOC FROM SaleItems WHERE SaleId = @SaleId AND ServiceId = @ServiceId AND ItemType = 'Service' FOR UPDATE",
                        new { model.SaleId, model.ServiceId }, transaction);
                    if (originalItem == null) return BadRequest(new { Message = "Service voucher item not found." });

                    decimal soldQty = (decimal)originalItem.Quantity;
                    if (soldQty < model.Quantity) return BadRequest(new { Message = "Return qty exceeds sold qty." });
                    appliedRefundAmount = Convert.ToBoolean(originalItem.IsFOC) ? 0m : Math.Max(0m, model.RefundAmount);

                    if (!await HasRemainingSaleRefundCapacityAsync(conn, (MySqlTransaction)transaction, model.SaleId, appliedRefundAmount))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { Message = "Return amount exceeds remaining sale amount." });
                    }

                    var serviceReturnPaymentMethod = await ResolveReturnPaymentMethodAsync(
                        conn,
                        (MySqlTransaction)transaction,
                        model.SaleId,
                        model.PreferredPaymentMethod);
                    if (await HasRecentMatchingSaleReturnAsync(
                        conn,
                        (MySqlTransaction)transaction,
                        model.SaleId,
                        "Service",
                        null,
                        null,
                        model.ServiceId.Value,
                        null,
                        model.Quantity,
                        appliedRefundAmount,
                        serviceReturnPaymentMethod))
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { Message = "Duplicate return request ignored. Please refresh the voucher." });
                    }

                    await conn.ExecuteAsync(@"
INSERT INTO SaleReturns (SaleId, ItemType, ProductId, BundleId, ServiceId, BranchId, Quantity, UnitId, RefundAmount, Reason, ReturnPaymentMethod, ReturnDate)
VALUES (@SaleId, 'Service', NULL, NULL, @ServiceId, @BranchId, @Quantity, NULL, @RefundAmount, @Reason, @ReturnPaymentMethod, NOW())",
                        new
                        {
                            model.SaleId,
                            ServiceId = model.ServiceId.Value,
                            model.BranchId,
                            model.Quantity,
                            RefundAmount = appliedRefundAmount,
                            Reason = model.Reason ?? "Customer Return",
                            ReturnPaymentMethod = serviceReturnPaymentMethod
                        }, transaction);

                    await conn.ExecuteAsync(
                        @"UPDATE SaleItems 
                          SET Quantity = Quantity - @qty,
                              SubTotal = GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)) - @RefundAmount)
                          WHERE SaleId = @SaleId AND ServiceId = @ServiceId AND ItemType = 'Service'",
                        new { qty = model.Quantity, model.SaleId, ServiceId = model.ServiceId.Value, RefundAmount = appliedRefundAmount }, transaction);
                }
                else
                {
                    return BadRequest(new { Message = "Invalid ItemType." });
                }

                await ApplySaleReturnToCustomerCreditAsync(conn, (MySqlTransaction)transaction, model.SaleId, appliedRefundAmount);

                await transaction.CommitAsync();
                return Ok(new { Message = "Sale return successful." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Sale Return Error: " + ex.Message });
            }
        }

        [HttpGet("find-po/{poReference}")]
        public async Task<IActionResult> GetPOItems(string poReference, [FromQuery] int? branchId = null)
        {
            var numericReference = new string((poReference ?? string.Empty).Where(char.IsDigit).ToArray());
            if (!int.TryParse(numericReference, out var poId) || poId <= 0)
                return BadRequest(new { Message = "Enter a valid PO number, for example 15, PO-00015, or PU0000015." });

            using (var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName)))
            {
                try
                {
                    await conn.OpenAsync();

                    string sql = @"
                SELECT 
                    poi.*, 
                    p.Name as ProductName, 
                    u.UnitName,
                    IFNULL(ps.Quantity, 0) as StockQty,
                    IFNULL(uc.Multiplier, 1) as UnitMultiplier,
                    GREATEST(0, LEAST(IFNULL(poi.Quantity, 0), IFNULL(ps.Quantity, 0) / IFNULL(uc.Multiplier, 1))) as ReturnableQty
                FROM PurchaseOrderItems poi
                JOIN PurchaseOrders po ON poi.POId = po.POId
                JOIN Products p ON poi.ProductId = p.ProductId
                LEFT JOIN Units u ON poi.UnitId = u.UnitId
                LEFT JOIN UnitConversions uc ON uc.ProductId = poi.ProductId AND uc.FromUnitId = poi.UnitId
                LEFT JOIN ProductStocks ps ON poi.ProductId = ps.ProductId 
                    AND ps.BranchId = po.BranchId
                WHERE poi.POId = @poId 
                AND (@branchId IS NULL OR po.BranchId = @branchId)";

                    var items = await conn.QueryAsync<dynamic>(sql, new { poId, branchId });

                    if (items == null || !items.Any())
                        return NotFound(new { Message = "ဝယ်ယူမှုမှတ်တမ်း ရှာမတွေ့ပါ။ (သို့မဟုတ် အခြားဆိုင်မှ စာရင်းဖြစ်နေသည်)" });

                    return Ok(items);
                }
                catch (MySqlException ex)
                {
                    return StatusCode(500, "Database Connection Error: " + ex.Message);
                }
            }
        }

        [HttpPost("transfer")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> TransferStock([FromBody] StockTransferModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                string masterSql = @"INSERT INTO StockTransfers (FromBranchId, ToBranchId, UserId, TransferDate, Status, Note) 
                             VALUES (@FromBranchId, @ToBranchId, @UserId, NOW(), 'Completed', @Note);
                             SELECT LAST_INSERT_ID();";

                int transferId = await conn.ExecuteScalarAsync<int>(masterSql, new
                {
                    model.FromBranchId,
                    model.ToBranchId,
                    model.UserId,
                    Note = model.Note ?? ""
                }, transaction);

                foreach (var item in model.Items)
                {
                    var productInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT UnitId as BaseUnitId FROM Products WHERE ProductId = @ProductId",
                        new { item.ProductId }, transaction);

                    decimal multiplier = 1;
                    if (item.UnitId != (int)productInfo.BaseUnitId)
                    {
                        multiplier = await conn.ExecuteScalarAsync<decimal>(
                            "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                            new { item.ProductId, item.UnitId }, transaction);
                    }

                    decimal baseQuantity = item.Quantity * multiplier;

                    await conn.ExecuteAsync(@"INSERT INTO StockTransferItems (TransferId, ProductId, Quantity, UnitId) 
                                     VALUES (@TransferId, @ProductId, @Quantity, @UnitId)",
                                             new { TransferId = transferId, item.ProductId, item.Quantity, item.UnitId }, transaction);

                    await conn.ExecuteAsync(@"UPDATE ProductStocks SET Quantity = Quantity - @baseQuantity 
                                     WHERE BranchId = @FromBranchId AND ProductId = @ProductId",
                                             new { baseQuantity, model.FromBranchId, item.ProductId }, transaction);

                    string upsertStockSql = @"INSERT INTO ProductStocks (BranchId, ProductId, Quantity) 
                                     VALUES (@ToBranchId, @ProductId, @baseQuantity)
                                     ON DUPLICATE KEY UPDATE Quantity = Quantity + @baseQuantity";

                    await conn.ExecuteAsync(upsertStockSql, new { model.ToBranchId, item.ProductId, baseQuantity }, transaction);
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Transfer successful", TransferId = transferId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Transfer Error: " + ex.Message });
            }
        }

        [HttpGet("expired")]
        public async Task<IActionResult> GetExpiredItems([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int? branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await EnsurePurchaseBatchRemainingColumnAsync(conn);
            var scopedBranchId = ResolveBranchScope(branchId);
            
            string whereClause = "WHERE poi.ExpiryDate IS NOT NULL AND poi.ExpiryDate < CURDATE() AND IFNULL(poi.RemainingQuantity, 0) > 0 AND (@branchId IS NULL OR po.BranchId = @branchId)";
            if (toDate.HasValue)
            {
                whereClause += " AND DATE(poi.ExpiryDate) <= @cutoffDate";
            }

            string sql = $@"
                SELECT 
                    poi.POItemId,
                    poi.ProductId,
                    (IFNULL(poi.RemainingQuantity, 0) / IFNULL(uc.Multiplier, 1)) AS Quantity,
                    IFNULL(poi.RemainingQuantity, 0) AS RemainingBaseQuantity,
                    poi.ExpiryDate,
                    p.Name as ProductName,
                    p.SKU as Sku,
                    u.UnitName,
                    DATEDIFF(CURDATE(), poi.ExpiryDate) as DaysPast
                FROM PurchaseOrderItems poi
                JOIN PurchaseOrders po ON poi.POId = po.POId
                JOIN Products p ON poi.ProductId = p.ProductId
                LEFT JOIN Units u ON poi.UnitId = u.UnitId
                LEFT JOIN UnitConversions uc ON uc.ProductId = poi.ProductId AND uc.FromUnitId = poi.UnitId
                {whereClause}
                ORDER BY poi.ExpiryDate ASC";

            var items = await conn.QueryAsync<dynamic>(sql, new { 
                cutoffDate = toDate?.Date,
                branchId = scopedBranchId
            });
            return Ok(items);
        }

        [HttpGet("adjustments")]
        public async Task<IActionResult> GetStockAdjustments([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int? branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            await EnsureStockAdjustmentLossColumnsAsync(conn);
            var scopedBranchId = ResolveBranchScope(branchId);
            
            string whereClause = "WHERE (@branchId IS NULL OR sa.BranchId = @branchId) AND sa.AdjustmentKind <> 'InventoryLoss'";
            if (fromDate.HasValue && toDate.HasValue)
            {
                var start = fromDate.Value.Date;
                var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                whereClause += " AND sa.AdjustmentDate BETWEEN @start AND @end";
            }

            string sql = $@"
                SELECT 
                    sa.AdjustmentId,
                    p.Name as ProductName,
                    sa.Type,
                    sa.Quantity,
                    u.UnitName,
                    sa.Reason,
                    sa.AdjustmentDate,
                    b.BranchName,
                    COALESCE(us.Username, 'System') as UserName
                FROM StockAdjustments sa
                JOIN Products p ON sa.ProductId = p.ProductId
                LEFT JOIN Units u ON sa.UnitId = u.UnitId
                LEFT JOIN Branches b ON sa.BranchId = b.BranchId
                LEFT JOIN ShopUsers us ON sa.UserId = us.UserId
                {whereClause}
                ORDER BY sa.AdjustmentDate DESC";

            var items = await conn.QueryAsync<dynamic>(sql, new { 
                start = fromDate?.Date, 
                end = toDate?.Date.AddDays(1).AddTicks(-1),
                branchId = scopedBranchId
            });
            return Ok(items);
        }

        [HttpGet("losses")]
        public async Task<IActionResult> GetInventoryLosses([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int? branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            await EnsureStockAdjustmentLossColumnsAsync(conn);
            var scopedBranchId = ResolveBranchScope(branchId);

            var start = fromDate?.Date;
            var end = toDate?.Date.AddDays(1).AddTicks(-1);
            const string sql = @"
SELECT
    sa.AdjustmentId AS LossId,
    sa.AdjustmentDate AS LossDate,
    p.Name AS ProductName,
    p.SKU AS Sku,
    sa.Quantity,
    IFNULL(sa.BaseQuantity, sa.Quantity * IFNULL(uc.Multiplier, 1)) AS BaseQuantity,
    u.UnitName,
    bu.UnitName AS BaseUnitName,
    sa.LossReason,
    sa.Reason AS Notes,
    IFNULL(sa.UnitCost, p.CostPrice) AS UnitCost,
    IFNULL(sa.LossAmount, IFNULL(sa.BaseQuantity, sa.Quantity * IFNULL(uc.Multiplier, 1)) * IFNULL(sa.UnitCost, p.CostPrice)) AS LossAmount,
    b.BranchName,
    COALESCE(us.Username, 'System') AS UserName
FROM StockAdjustments sa
JOIN Products p ON p.ProductId = sa.ProductId
LEFT JOIN Units u ON u.UnitId = sa.UnitId
LEFT JOIN Units bu ON bu.UnitId = p.UnitId
LEFT JOIN UnitConversions uc ON uc.ProductId = sa.ProductId AND uc.FromUnitId = sa.UnitId
LEFT JOIN Branches b ON b.BranchId = sa.BranchId
LEFT JOIN ShopUsers us ON us.UserId = sa.UserId
WHERE sa.AdjustmentKind = 'InventoryLoss'
  AND (@branchId IS NULL OR sa.BranchId = @branchId)
  AND (@start IS NULL OR sa.AdjustmentDate >= @start)
  AND (@end IS NULL OR sa.AdjustmentDate <= @end)
ORDER BY sa.AdjustmentDate DESC, sa.AdjustmentId DESC";

            var items = await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId });
            return Ok(items);
        }

        [HttpGet("find-po-by-product/{productId}")]
        public async Task<IActionResult> FindLatestPOByProduct(int productId, [FromQuery] int? branchId = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // Find the latest PO that contains this product
            var latestPO = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT poi.POId, po.BranchId
                FROM PurchaseOrderItems poi
                JOIN PurchaseOrders po ON poi.POId = po.POId
                WHERE poi.ProductId = @productId
                  AND (@branchId IS NULL OR po.BranchId = @branchId)
                ORDER BY po.POId DESC
                LIMIT 1",
                new { productId, branchId });

            if (latestPO == null)
                return NotFound(new { Message = "No purchase order found containing this product." });

            int poId = (int)latestPO.POId;

            // Return all items from that PO (same format as find-po/{poId})
            string sql = @"
                SELECT 
                    poi.*, 
                    p.Name as ProductName, 
                    u.UnitName,
                    IFNULL(ps.Quantity, 0) as StockQty,
                    IFNULL(uc.Multiplier, 1) as UnitMultiplier,
                    GREATEST(0, LEAST(IFNULL(poi.Quantity, 0), IFNULL(ps.Quantity, 0) / IFNULL(uc.Multiplier, 1))) as ReturnableQty
                FROM PurchaseOrderItems poi
                JOIN PurchaseOrders po ON poi.POId = po.POId
                JOIN Products p ON poi.ProductId = p.ProductId
                LEFT JOIN Units u ON poi.UnitId = u.UnitId
                LEFT JOIN UnitConversions uc ON uc.ProductId = poi.ProductId AND uc.FromUnitId = poi.UnitId
                LEFT JOIN ProductStocks ps ON poi.ProductId = ps.ProductId 
                    AND ps.BranchId = po.BranchId
                WHERE poi.POId = @poId";

            var items = await conn.QueryAsync<dynamic>(sql, new { poId });

            if (items == null || !items.Any())
                return NotFound(new { Message = "Purchase order items not found." });

            return Ok(items);
        }
    }
}
