using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConsignmentsController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public ConsignmentsController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateConsignment([FromBody] PurchaseEntryModel model)
        {
            if (model.SupplierId <= 0 || model.Items == null || model.Items.Count == 0)
                return BadRequest(new { Message = "Supplier and items are required." });

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                var orderDate = model.PurchaseDate?.Date ?? DateTime.Now.Date;
                var consignId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO Consignments (SupplierId, BranchId, OrderDate, Status, Remark)
VALUES (@SupplierId, @BranchId, @OrderDate, 'Active', @Remark);
SELECT LAST_INSERT_ID();",
                    new
                    {
                        model.SupplierId,
                        model.BranchId,
                        OrderDate = orderDate,
                        Remark = model.Remarks
                    },
                    transaction);

                foreach (var item in model.Items)
                {
                    var multiplier = await ResolveMultiplierAsync(conn, transaction, item.ProductId, item.UnitId);
                    var totalQty = item.Quantity + (item.FOCQuantity ?? 0m);
                    var baseQty = totalQty * multiplier;
                    var baseUnitCost = multiplier > 0 ? item.CostPrice / multiplier : item.CostPrice;

                    await conn.ExecuteAsync(@"
INSERT INTO ConsignmentItems
    (ConsignId, ProductId, UnitId, BranchId, Quantity, SoldQuantity, SettledQuantity, ReturnedQuantity, UnitPrice)
VALUES
    (@ConsignId, @ProductId, @UnitId, @BranchId, @Quantity, 0, 0, 0, @UnitPrice)",
                        new
                        {
                            ConsignId = consignId,
                            item.ProductId,
                            item.UnitId,
                            model.BranchId,
                            Quantity = baseQty,
                            UnitPrice = baseUnitCost
                        },
                        transaction);

                    await conn.ExecuteAsync(@"
INSERT INTO ProductStocks (BranchId, ProductId, Quantity)
VALUES (@BranchId, @ProductId, @Quantity)
ON DUPLICATE KEY UPDATE Quantity = Quantity + @Quantity",
                        new { model.BranchId, item.ProductId, Quantity = baseQty },
                        transaction);
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Consignment received successfully.", ConsignId = consignId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int? branchId = null, [FromQuery] int? supplierId = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            var sql = @"
SELECT
    c.ConsignId,
    c.SupplierId,
    s.SupplierName,
    c.BranchId,
    b.BranchName,
    c.OrderDate,
    c.Status,
    c.Remark,
    IFNULL(SUM(ci.Quantity * ci.UnitPrice), 0) AS TotalCost,
    IFNULL(SUM(ci.Quantity), 0) AS TotalQuantity,
    IFNULL(SUM(ci.SoldQuantity), 0) AS SoldQuantity,
    IFNULL(SUM(ci.SettledQuantity), 0) AS SettledQuantity,
    IFNULL(SUM(ci.SoldQuantity - ci.SettledQuantity), 0) AS UnsettledQuantity,
    IFNULL(SUM((ci.SoldQuantity - ci.SettledQuantity) * ci.UnitPrice), 0) AS UnsettledAmount
FROM Consignments c
LEFT JOIN ConsignmentItems ci ON ci.ConsignId = c.ConsignId
LEFT JOIN Suppliers s ON s.SupplierId = c.SupplierId
LEFT JOIN Branches b ON b.BranchId = c.BranchId
WHERE (@branchId IS NULL OR c.BranchId = @branchId)
  AND (@supplierId IS NULL OR c.SupplierId = @supplierId)
GROUP BY c.ConsignId, c.SupplierId, s.SupplierName, c.BranchId, b.BranchName, c.OrderDate, c.Status, c.Remark
ORDER BY c.OrderDate DESC, c.ConsignId DESC";

            return Ok(await conn.QueryAsync<dynamic>(sql, new { branchId, supplierId }));
        }

        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventory([FromQuery] int? branchId = null, [FromQuery] int? supplierId = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            var sql = @"
SELECT
    ci.ConsignItemId,
    ci.ConsignId,
    c.SupplierId,
    s.SupplierName,
    COALESCE(ci.BranchId, c.BranchId) AS BranchId,
    b.BranchName,
    ci.ProductId,
    p.Name AS ProductName,
    p.Sku,
    u.UnitName AS BaseUnitName,
    ci.Quantity,
    ci.SoldQuantity,
    ci.SettledQuantity,
    ci.ReturnedQuantity,
    (ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity) AS AvailableQuantity,
    (ci.SoldQuantity - ci.SettledQuantity) AS UnsettledQuantity,
    ci.UnitPrice,
    ((ci.SoldQuantity - ci.SettledQuantity) * ci.UnitPrice) AS UnsettledAmount,
    c.Status,
    c.OrderDate
FROM ConsignmentItems ci
JOIN Consignments c ON c.ConsignId = ci.ConsignId
LEFT JOIN Suppliers s ON s.SupplierId = c.SupplierId
LEFT JOIN Branches b ON b.BranchId = COALESCE(ci.BranchId, c.BranchId)
LEFT JOIN Products p ON p.ProductId = ci.ProductId
LEFT JOIN Units u ON u.UnitId = p.UnitId
WHERE (@branchId IS NULL OR COALESCE(ci.BranchId, c.BranchId) = @branchId)
  AND (@supplierId IS NULL OR c.SupplierId = @supplierId)
ORDER BY c.OrderDate DESC, ci.ConsignItemId DESC";

            return Ok(await conn.QueryAsync<dynamic>(sql, new { branchId, supplierId }));
        }

        [HttpGet("unsettled")]
        public async Task<IActionResult> GetUnsettled([FromQuery] int? branchId = null, [FromQuery] int? supplierId = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            var sql = @"
SELECT *
FROM vw_ConsignmentInventory
WHERE UnsettledQuantity > 0
  AND (@branchId IS NULL OR BranchId = @branchId)
  AND (@supplierId IS NULL OR SupplierId = @supplierId)
ORDER BY OrderDate, ConsignItemId";

            return Ok(await conn.QueryAsync<dynamic>(sql, new { branchId, supplierId }));
        }

        [HttpPost("settlements")]
        public async Task<IActionResult> CreateSettlement([FromBody] ConsignmentSettlementRequest model)
        {
            if (model.SupplierId <= 0) return BadRequest(new { Message = "Supplier is required." });

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                var items = model.Items?.Where(x => x.ConsignItemId > 0 && x.Quantity > 0).ToList() ?? new();

                if (items.Count == 0)
                {
                    var autoItems = await conn.QueryAsync<ConsignmentSettlementItemRequest>(@"
SELECT
    ci.ConsignItemId,
    ci.ProductId,
    (ci.SoldQuantity - ci.SettledQuantity) AS Quantity,
    ci.UnitPrice AS UnitCost
FROM ConsignmentItems ci
JOIN Consignments c ON c.ConsignId = ci.ConsignId
WHERE c.SupplierId = @SupplierId
  AND (@BranchId IS NULL OR COALESCE(ci.BranchId, c.BranchId) = @BranchId)
  AND (ci.SoldQuantity - ci.SettledQuantity) > 0",
                        new { model.SupplierId, model.BranchId },
                        transaction);

                    items = autoItems.ToList();
                }

                if (items.Count == 0) return BadRequest(new { Message = "No unsettled consignment items found." });

                decimal total = items.Sum(x => x.Quantity * x.UnitCost);
                var settlementId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO ConsignmentSettlements (SupplierId, BranchId, TotalAmount, PaymentMethod, ReferenceNo, Note)
VALUES (@SupplierId, @BranchId, @TotalAmount, @PaymentMethod, @ReferenceNo, @Note);
SELECT LAST_INSERT_ID();",
                    new
                    {
                        model.SupplierId,
                        model.BranchId,
                        TotalAmount = total,
                        PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod.Trim(),
                        ReferenceNo = model.ReferenceNo,
                        model.Note
                    },
                    transaction);

                foreach (var item in items)
                {
                    var row = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT ci.ProductId, ci.UnitPrice, (ci.SoldQuantity - ci.SettledQuantity) AS UnsettledQuantity
FROM ConsignmentItems ci
JOIN Consignments c ON c.ConsignId = ci.ConsignId
WHERE ci.ConsignItemId = @ConsignItemId
  AND c.SupplierId = @SupplierId",
                        new { item.ConsignItemId, model.SupplierId },
                        transaction);

                    if (row == null) throw new Exception($"Consignment item {item.ConsignItemId} not found.");

                    var unsettled = (decimal)row.UnsettledQuantity;
                    if (item.Quantity > unsettled)
                        throw new Exception($"Settlement quantity exceeds unsettled quantity for item {item.ConsignItemId}.");

                    var unitCost = item.UnitCost > 0 ? item.UnitCost : (decimal)row.UnitPrice;
                    var amount = item.Quantity * unitCost;

                    await conn.ExecuteAsync(@"
INSERT INTO ConsignmentSettlementItems (SettlementId, ConsignItemId, ProductId, Quantity, UnitCost, Amount)
VALUES (@SettlementId, @ConsignItemId, @ProductId, @Quantity, @UnitCost, @Amount)",
                        new
                        {
                            SettlementId = settlementId,
                            item.ConsignItemId,
                            ProductId = (int)row.ProductId,
                            item.Quantity,
                            UnitCost = unitCost,
                            Amount = amount
                        },
                        transaction);

                    await conn.ExecuteAsync(@"
UPDATE ConsignmentItems
SET SettledQuantity = SettledQuantity + @Quantity
WHERE ConsignItemId = @ConsignItemId",
                        new { item.Quantity, item.ConsignItemId },
                        transaction);
                }

                await conn.ExecuteAsync(@"
UPDATE Consignments c
SET Status = 'Settled'
WHERE c.SupplierId = @SupplierId
  AND NOT EXISTS (
      SELECT 1
      FROM ConsignmentItems ci
      WHERE ci.ConsignId = c.ConsignId
        AND (ci.SoldQuantity - ci.SettledQuantity) > 0
  )
  AND EXISTS (
      SELECT 1
      FROM ConsignmentItems ci
      WHERE ci.ConsignId = c.ConsignId
        AND ci.SoldQuantity > 0
  )",
                    new { model.SupplierId },
                    transaction);

                await transaction.CommitAsync();
                return Ok(new { Message = "Consignment payout completed.", SettlementId = settlementId, TotalAmount = total });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("returns")]
        public async Task<IActionResult> ReturnConsignmentItem([FromBody] ConsignmentReturnRequest model)
        {
            if (model.ConsignItemId <= 0 || model.Quantity <= 0)
                return BadRequest(new { Message = "Consignment item and return quantity are required." });

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                var row = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT
    ci.ConsignItemId,
    ci.ProductId,
    COALESCE(ci.BranchId, c.BranchId) AS BranchId,
    (ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity) AS AvailableQuantity
FROM ConsignmentItems ci
JOIN Consignments c ON c.ConsignId = ci.ConsignId
WHERE ci.ConsignItemId = @ConsignItemId",
                    new { model.ConsignItemId },
                    transaction);

                if (row == null) return NotFound(new { Message = "Consignment item not found." });

                var available = Convert.ToDecimal(row.AvailableQuantity);
                if (available <= 0)
                    return BadRequest(new { Message = "No unsold consignment quantity is available to return." });

                if (model.Quantity > available)
                    return BadRequest(new { Message = "Return quantity exceeds unsold consignment quantity." });

                var productId = Convert.ToInt32(row.ProductId);
                var branchId = Convert.ToInt32(row.BranchId);

                var currentStock = await conn.ExecuteScalarAsync<decimal?>(@"
SELECT Quantity
FROM ProductStocks
WHERE BranchId = @BranchId AND ProductId = @ProductId",
                    new { BranchId = branchId, ProductId = productId },
                    transaction) ?? 0m;

                if (model.Quantity > currentStock)
                    return BadRequest(new { Message = "Return quantity exceeds current stock." });

                var updatedConsignmentRows = await conn.ExecuteAsync(@"
UPDATE ConsignmentItems
SET ReturnedQuantity = ReturnedQuantity + @Quantity
WHERE ConsignItemId = @ConsignItemId
  AND (Quantity - SoldQuantity - ReturnedQuantity) >= @Quantity",
                    new { model.Quantity, model.ConsignItemId },
                    transaction);
                if (updatedConsignmentRows == 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Return quantity exceeds unsold consignment quantity." });
                }

                await conn.ExecuteAsync(@"
UPDATE ProductStocks
SET Quantity = Quantity - @Quantity
WHERE BranchId = @BranchId AND ProductId = @ProductId",
                    new { model.Quantity, BranchId = branchId, ProductId = productId },
                    transaction);

                await transaction.CommitAsync();
                return Ok(new { Message = "Consignment return completed." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        private static async Task<decimal> ResolveMultiplierAsync(MySqlConnection conn, MySqlTransaction transaction, int productId, int unitId)
        {
            var multiplier = await conn.QueryFirstOrDefaultAsync<decimal?>(
                "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                new { ProductId = productId, UnitId = unitId },
                transaction);

            var value = multiplier.GetValueOrDefault(1m);
            return value <= 0 ? 1m : value;
        }

        public class ConsignmentReturnRequest
        {
            public int ConsignItemId { get; set; }
            public decimal Quantity { get; set; }
            public string? Reason { get; set; }
        }
    }
}
