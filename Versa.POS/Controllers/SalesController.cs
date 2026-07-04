using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using System.Text.Json;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : BaseShopController
    {
        private readonly DatabaseService _dbService;
        public SalesController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public class HeldSaleDraftRequest
        {
            public int? HeldSaleId { get; set; }
            public int BranchId { get; set; }
            public int? CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public int UserId { get; set; }
            public decimal TotalAmount { get; set; }
            public int ItemCount { get; set; }
            public string? Preview { get; set; }
            public string PayloadJson { get; set; } = "";
        }

        private static async Task EnsureHeldSalesTableAsync(MySqlConnection conn)
        {
            await conn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS HeldSales (
  HeldSaleId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  BranchId INT NOT NULL,
  CustomerId INT NULL,
  CustomerName VARCHAR(255) NULL,
  UserId INT NOT NULL,
  TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
  ItemCount INT NOT NULL DEFAULT 0,
  Preview TEXT NULL,
  PayloadJson LONGTEXT NOT NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  ExpiresAt DATETIME NOT NULL,
  INDEX IX_HeldSales_Branch_Expires (BranchId, ExpiresAt),
  INDEX IX_HeldSales_User (UserId)
);");
        }

        private static async Task CleanupExpiredHeldSalesAsync(MySqlConnection conn)
        {
            await conn.ExecuteAsync("DELETE FROM HeldSales WHERE ExpiresAt <= NOW()");
        }

        private static async Task EnsurePurchaseBatchRemainingColumnAsync(MySqlConnection conn, MySqlTransaction transaction)
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

        private static async Task DeductPurchaseBatchesFefoAsync(
            MySqlConnection conn,
            MySqlTransaction transaction,
            int branchId,
            int productId,
            decimal baseQuantity)
        {
            if (baseQuantity <= 0) return;

            var batches = (await conn.QueryAsync<dynamic>(@"
SELECT poi.POItemId, IFNULL(poi.RemainingQuantity, 0) AS RemainingQuantity
FROM PurchaseOrderItems poi
JOIN PurchaseOrders po ON po.POId = poi.POId
WHERE po.BranchId = @BranchId
  AND poi.ProductId = @ProductId
  AND IFNULL(poi.RemainingQuantity, 0) > 0
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

                var available = Convert.ToDecimal(batch.RemainingQuantity);
                var deduct = Math.Min(available, remaining);
                if (deduct <= 0) continue;

                await conn.ExecuteAsync(@"
UPDATE PurchaseOrderItems
SET RemainingQuantity = GREATEST(IFNULL(RemainingQuantity, 0) - @Deduct, 0)
WHERE POItemId = @POItemId",
                    new { Deduct = deduct, POItemId = (int)batch.POItemId },
                    transaction);

                remaining -= deduct;
            }
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

        private static async Task EnsureSalesOrderStatusColumnAsync(MySqlConnection conn, MySqlTransaction transaction)
        {
            var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'Sales'
  AND COLUMN_NAME = 'OrderStatus'",
                transaction: transaction);

            if (exists == 0)
            {
                await conn.ExecuteAsync(
                    "ALTER TABLE Sales ADD COLUMN OrderStatus VARCHAR(20) NOT NULL DEFAULT 'Completed'",
                    transaction: transaction);
            }
        }

        private static async Task EnsureSalesClientSaleIdColumnAsync(MySqlConnection conn, MySqlTransaction transaction)
        {
            var hasColumn = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'Sales'
  AND COLUMN_NAME = 'ClientSaleId'",
                transaction: transaction);

            if (hasColumn == 0)
            {
                await conn.ExecuteAsync(
                    "ALTER TABLE Sales ADD COLUMN ClientSaleId VARCHAR(80) NULL",
                    transaction: transaction);
            }

            var hasIndex = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'Sales'
  AND INDEX_NAME = 'UX_Sales_ClientSaleId'",
                transaction: transaction);

            if (hasIndex == 0)
            {
                await conn.ExecuteAsync(
                    "ALTER TABLE Sales ADD UNIQUE INDEX UX_Sales_ClientSaleId (ClientSaleId)",
                    transaction: transaction);
            }
        }

        private static async Task EnsureSalesSaleTypeColumnAsync(MySqlConnection conn, MySqlTransaction transaction)
        {
            var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'Sales'
  AND COLUMN_NAME = 'SaleType'",
                transaction: transaction);

            if (exists == 0)
            {
                await conn.ExecuteAsync(
                    "ALTER TABLE Sales ADD COLUMN SaleType VARCHAR(20) NOT NULL DEFAULT 'Normal'",
                    transaction: transaction);
                return;
            }

            await conn.ExecuteAsync(
                "ALTER TABLE Sales MODIFY COLUMN SaleType VARCHAR(20) NOT NULL DEFAULT 'Normal'",
                transaction: transaction);
        }

        private static async Task EnsureBundleItemsSupportsUnitsAsync(MySqlConnection conn, MySqlTransaction transaction)
        {
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
        }

        private static async Task<bool> HasColumnAsync(MySqlConnection conn, MySqlTransaction transaction, string tableName, string columnName)
        {
            var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @TableName
  AND COLUMN_NAME = @ColumnName",
                new { TableName = tableName, ColumnName = columnName },
                transaction: transaction);

            return exists > 0;
        }

        private static async Task<(bool IsConsignment, int? ConsignItemId, decimal? UnitCost)> AllocateConsignmentSaleAsync(
            MySqlConnection conn,
            MySqlTransaction transaction,
            int branchId,
            int productId,
            decimal baseQty)
        {
            if (baseQty <= 0) return (false, null, null);

            var lots = (await conn.QueryAsync<dynamic>(@"
SELECT ci.ConsignItemId,
       ci.UnitPrice,
       (ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity) AS AvailableQuantity
FROM ConsignmentItems ci
JOIN Consignments c ON c.ConsignId = ci.ConsignId
WHERE ci.ProductId = @ProductId
  AND COALESCE(ci.BranchId, c.BranchId) = @BranchId
  AND c.Status = 'Active'
  AND (ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity) > 0
ORDER BY c.OrderDate, ci.ConsignItemId",
                new { ProductId = productId, BranchId = branchId },
                transaction)).ToList();

            decimal remaining = baseQty;
            int? firstConsignItemId = null;
            decimal? firstUnitCost = null;

            foreach (var lot in lots)
            {
                if (remaining <= 0) break;

                decimal available = Convert.ToDecimal(lot.AvailableQuantity);
                if (available <= 0) continue;

                decimal deduct = Math.Min(available, remaining);
                int consignItemId = Convert.ToInt32(lot.ConsignItemId);

                var affectedRows = await conn.ExecuteAsync(@"
UPDATE ConsignmentItems
SET SoldQuantity = SoldQuantity + @Deduct
WHERE ConsignItemId = @ConsignItemId
  AND (Quantity - SoldQuantity - ReturnedQuantity) >= @Deduct",
                    new { Deduct = deduct, ConsignItemId = consignItemId },
                    transaction);
                if (affectedRows == 0) continue;

                firstConsignItemId ??= consignItemId;
                firstUnitCost ??= Convert.ToDecimal(lot.UnitPrice);
                remaining -= deduct;
            }

            return (firstConsignItemId.HasValue, firstConsignItemId, firstUnitCost);
        }

        [HttpGet("held-sales")]
        public async Task<IActionResult> GetHeldSales([FromQuery] int? branchId = null)
        {
            var tenantDb = User.FindFirst("TenantDb")?.Value;
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(tenantDb));
            await conn.OpenAsync();
            await conn.ExecuteAsync("SET time_zone = '+06:30'");
            await EnsureHeldSalesTableAsync(conn);
            await CleanupExpiredHeldSalesAsync(conn);

            var rows = await conn.QueryAsync(@"
SELECT HeldSaleId,
       BranchId,
       CustomerId,
       CustomerName,
       UserId,
       TotalAmount,
       ItemCount,
       Preview,
       PayloadJson,
       CreatedAt,
       UpdatedAt,
       ExpiresAt
FROM HeldSales
WHERE ExpiresAt > NOW()
  AND (@BranchId IS NULL OR BranchId = @BranchId)
ORDER BY UpdatedAt DESC, HeldSaleId DESC",
                new { BranchId = branchId });

            return Ok(rows);
        }

        [HttpPost("hold-sale")]
        public async Task<IActionResult> HoldSale([FromBody] HeldSaleDraftRequest model)
        {
            if (model == null) return BadRequest(new { Message = "Draft payload is required." });
            if (model.BranchId <= 0) return BadRequest(new { Message = "BranchId is required." });
            if (string.IsNullOrWhiteSpace(model.PayloadJson)) return BadRequest(new { Message = "PayloadJson is required." });

            try
            {
                using var _ = JsonDocument.Parse(model.PayloadJson);
            }
            catch
            {
                return BadRequest(new { Message = "PayloadJson must be valid JSON." });
            }

            var tenantDb = User.FindFirst("TenantDb")?.Value;
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(tenantDb));
            await conn.OpenAsync();
            await conn.ExecuteAsync("SET time_zone = '+06:30'");
            await EnsureHeldSalesTableAsync(conn);
            await CleanupExpiredHeldSalesAsync(conn);

            int claimUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : 0;
            int effectiveUserId = claimUserId > 0 ? claimUserId : model.UserId;
            string cleanCustomerName = string.IsNullOrWhiteSpace(model.CustomerName) ? "Walk-in Customer" : model.CustomerName.Trim();

            if (model.HeldSaleId.HasValue && model.HeldSaleId.Value > 0)
            {
                var affected = await conn.ExecuteAsync(@"
UPDATE HeldSales
SET BranchId = @BranchId,
    CustomerId = @CustomerId,
    CustomerName = @CustomerName,
    UserId = @UserId,
    TotalAmount = @TotalAmount,
    ItemCount = @ItemCount,
    Preview = @Preview,
    PayloadJson = @PayloadJson,
    UpdatedAt = NOW(),
    ExpiresAt = DATE_ADD(NOW(), INTERVAL 10 DAY)
WHERE HeldSaleId = @HeldSaleId
  AND ExpiresAt > NOW()",
                    new
                    {
                        HeldSaleId = model.HeldSaleId.Value,
                        model.BranchId,
                        model.CustomerId,
                        CustomerName = cleanCustomerName,
                        UserId = effectiveUserId,
                        model.TotalAmount,
                        model.ItemCount,
                        Preview = model.Preview ?? "",
                        model.PayloadJson
                    });

                if (affected > 0)
                {
                    var updated = await conn.QuerySingleAsync(@"
SELECT HeldSaleId, BranchId, CustomerId, CustomerName, UserId, TotalAmount, ItemCount, Preview, PayloadJson, CreatedAt, UpdatedAt, ExpiresAt
FROM HeldSales
WHERE HeldSaleId = @HeldSaleId",
                        new { HeldSaleId = model.HeldSaleId.Value });
                    return Ok(updated);
                }
            }

            var inserted = await conn.QuerySingleAsync(@"
INSERT INTO HeldSales (BranchId, CustomerId, CustomerName, UserId, TotalAmount, ItemCount, Preview, PayloadJson, CreatedAt, UpdatedAt, ExpiresAt)
VALUES (@BranchId, @CustomerId, @CustomerName, @UserId, @TotalAmount, @ItemCount, @Preview, @PayloadJson, NOW(), NOW(), DATE_ADD(NOW(), INTERVAL 10 DAY));
SELECT HeldSaleId, BranchId, CustomerId, CustomerName, UserId, TotalAmount, ItemCount, Preview, PayloadJson, CreatedAt, UpdatedAt, ExpiresAt
FROM HeldSales
WHERE HeldSaleId = LAST_INSERT_ID();",
                new
                {
                    model.BranchId,
                    model.CustomerId,
                    CustomerName = cleanCustomerName,
                    UserId = effectiveUserId,
                    model.TotalAmount,
                    model.ItemCount,
                    Preview = model.Preview ?? "",
                    model.PayloadJson
                });

            return Ok(inserted);
        }

        [HttpDelete("held-sales/{heldSaleId:int}")]
        public async Task<IActionResult> DeleteHeldSale(int heldSaleId)
        {
            var tenantDb = User.FindFirst("TenantDb")?.Value;
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(tenantDb));
            await conn.OpenAsync();
            await EnsureHeldSalesTableAsync(conn);

            await conn.ExecuteAsync("DELETE FROM HeldSales WHERE HeldSaleId = @HeldSaleId", new { HeldSaleId = heldSaleId });
            return Ok(new { HeldSaleId = heldSaleId });
        }

        [HttpPost("create-sale")]
        public async Task<IActionResult> CreateSale([FromBody] SaleEntryModel model)
        {
            var tenantDb = User.FindFirst("TenantDb")?.Value;
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(tenantDb));
            await conn.OpenAsync();
            // Set session timezone to Asia/Yangon (UTC+6:30) so TIMESTAMP stores/returns local time
            await conn.ExecuteAsync("SET time_zone = '+06:30'");
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsureSalesOrderStatusColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsureSalesClientSaleIdColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsureSalesSaleTypeColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsureBundleItemsSupportsUnitsAsync(conn, (MySqlTransaction)transaction);

                string? cleanClientSaleId = string.IsNullOrWhiteSpace(model.ClientSaleId) ? null : model.ClientSaleId.Trim();
                if (!string.IsNullOrWhiteSpace(cleanClientSaleId))
                {
                    var existingSaleId = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT SaleId FROM Sales WHERE ClientSaleId = @ClientSaleId LIMIT 1",
                        new { ClientSaleId = cleanClientSaleId },
                        transaction);

                    if (existingSaleId.HasValue)
                    {
                        await transaction.CommitAsync();
                        return Ok(new { SaleId = existingSaleId.Value, SaleNo = "SA" + existingSaleId.Value.ToString("D7"), Duplicate = true });
                    }
                }

                // 0. Resolve Customer
                int? resolvedCustomerId = model.CustomerId > 0 ? model.CustomerId : null;
                string cleanCustomerName = model.CustomerName?.Trim();

                if (!resolvedCustomerId.HasValue && !string.IsNullOrWhiteSpace(cleanCustomerName) && 
                    !cleanCustomerName.Equals("Walk-in", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if customer exists (case-insensitive)
                    resolvedCustomerId = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT CustomerId FROM Customers WHERE LOWER(Name) = LOWER(@Name) LIMIT 1", 
                        new { Name = cleanCustomerName }, transaction);

                    if (!resolvedCustomerId.HasValue)
                    {
                        // Create new customer
                        string insertCustomerSql = "INSERT INTO Customers (Name, Phone, Address, CurrentBalance, CreatedAt) VALUES (@Name, '', '', 0, NOW()); SELECT LAST_INSERT_ID();";
                        resolvedCustomerId = await conn.ExecuteScalarAsync<int>(insertCustomerSql, new { Name = cleanCustomerName }, transaction);
                    }
                }

                // ၁။ စုစုပေါင်း ပမာဏ တွက်ချက်ခြင်း
                decimal actualItemsTotal = model.Items.Where(i => !i.IsFOC).Sum(i => (i.Quantity * i.UnitPrice) - i.DiscountAmount);
                decimal finalTotalAmount = actualItemsTotal - model.DiscountAmount + model.DeliveryFee;
                decimal dueAmount = Math.Max(0, finalTotalAmount - model.PaidAmount);
                string finalSaleType = dueAmount > 0 ? (model.PaidAmount > 0 ? "Partial" : "Credit") : "Normal";

                // Always trust authenticated user claim over client payload for audit fields
                int claimUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : 0;
                int effectiveUserId = claimUserId > 0 ? claimUserId : model.UserId;

                // ၂။ Sales Master သိမ်းဆည်းခြင်း
                DateTime saleDate = model.SaleDate ?? DateTime.Now;
                string saleSql = @"INSERT INTO Sales (BranchId, CustomerId, UserId, TotalAmount, DiscountAmount, DeliveryFee, PaidAmount, DueAmount, PaymentMethod, VoucherNote, SaleDate, SaleType, OrderStatus, ClientSaleId)  
                          VALUES (@BranchId, @CustomerId, @UserId, @actualItemsTotal, @DiscountAmount, @DeliveryFee, @PaidAmount, @dueAmount, @PaymentMethod, @VoucherNote, @SaleDate, @finalSaleType, @OrderStatus, @ClientSaleId);
                          SELECT LAST_INSERT_ID();";

                int saleId = await conn.ExecuteScalarAsync<int>(saleSql, new
                {
                    model.BranchId,
                    CustomerId = resolvedCustomerId,
                    UserId = effectiveUserId,
                    actualItemsTotal,
                    model.DiscountAmount,
                    model.DeliveryFee,
                    model.PaidAmount,
                    dueAmount,
                    model.PaymentMethod,
                    VoucherNote = string.IsNullOrWhiteSpace(model.VoucherNote) ? (model.Note ?? "") : model.VoucherNote,
                    SaleDate = saleDate,
                    finalSaleType,
                    OrderStatus = "Completed",
                    ClientSaleId = cleanClientSaleId
                }, transaction);

                // ၃။ Items Loop & Stock Update Logic
                foreach (var item in model.Items)
                {
                    // SaleItems ထဲသို့ သွင်းခြင်း (ItemType ပါဝင်သည်)
                    string insertItemSql = @"INSERT INTO SaleItems (SaleId, ItemType, ProductId, ServiceId, BundleId, UnitId, Quantity, UnitPrice, DiscountAmount, IsFOC, CostPrice, SubTotal, Note, IsConsignment, ConsignItemId) 
                                     VALUES (@SaleId, @ItemType, @ProductId, @ServiceId, @BundleId, @UnitId, @Quantity, @UnitPrice, @DiscountAmount, @IsFOC, @CostPrice, @SubTotal, @Note, @IsConsignment, @ConsignItemId);
                                     SELECT LAST_INSERT_ID();";

                    int saleItemId = await conn.ExecuteScalarAsync<int>(insertItemSql, new
                    {
                        SaleId = saleId,
                        item.ItemType,
                        item.ProductId,
                        item.ServiceId,
                        item.BundleId,
                        item.UnitId,
                        item.Quantity,
                        UnitPrice = item.IsFOC ? 0 : item.UnitPrice,
                        DiscountAmount = item.IsFOC ? 0 : item.DiscountAmount,
                        item.IsFOC,
                        item.CostPrice, // Frontend မှ အရင်းဈေး ပို့ပေးရန် လိုအပ်ပါသည်
                        SubTotal = item.IsFOC ? 0 : (item.Quantity * item.UnitPrice) - item.DiscountAmount,
                        item.Note,
                        item.IsConsignment,
                        item.ConsignItemId
                    }, transaction);

                    // အမျိုးအစားအလိုက် Stock နှုတ်ခြင်း logic
                    if (item.ItemType == "Product")
                    {
                        decimal multiplier = item.Multiplier > 0 ? item.Multiplier : 1;
                        decimal baseQty = item.Quantity * multiplier;
                        if (item.ProductId.HasValue)
                        {
                            await DeductPurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, model.BranchId, item.ProductId.Value, baseQty);
                        }
                        await conn.ExecuteAsync("UPDATE ProductStocks SET Quantity = Quantity - @baseQty WHERE BranchId = @BranchId AND ProductId = @ProductId",
                            new { baseQty, model.BranchId, item.ProductId }, transaction);

                        if (!item.IsFOC && item.ProductId.HasValue)
                        {
                            var allocation = await AllocateConsignmentSaleAsync(
                                conn,
                                (MySqlTransaction)transaction,
                                model.BranchId,
                                item.ProductId.Value,
                                baseQty);

                            if (allocation.IsConsignment)
                            {
                                await conn.ExecuteAsync(@"
UPDATE SaleItems
SET IsConsignment = 1,
    ConsignItemId = @ConsignItemId,
    CostPrice = COALESCE(@UnitCost, CostPrice)
WHERE SaleItemId = @SaleItemId",
                                    new
                                    {
                                        allocation.ConsignItemId,
                                        allocation.UnitCost,
                                        SaleItemId = saleItemId
                                    },
                                    transaction);
                            }
                        }
                    }
                    else if (item.ItemType == "Bundle")
                    {
                        // Bundle ထဲရှိ Component ပစ္စည်းများကို တစ်ခုချင်းစီ Stock နှုတ်ပေးရမည်
                        var components = await conn.QueryAsync<dynamic>(
                            @"SELECT bi.ComponentProductId, bi.Quantity, IFNULL(uc.Multiplier, 1) as UnitMultiplier
                              FROM BundleItems bi
                              LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                              WHERE bi.BundleId = @BundleId AND bi.ComponentProductId IS NOT NULL AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'",
                            new { item.BundleId }, transaction);

                        foreach (var comp in components)
                        {
                            decimal totalDeduct = Convert.ToDecimal(comp.Quantity) * Convert.ToDecimal(comp.UnitMultiplier) * item.Quantity;
                            await DeductPurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, model.BranchId, Convert.ToInt32(comp.ComponentProductId), totalDeduct);
                            await conn.ExecuteAsync("UPDATE ProductStocks SET Quantity = Quantity - @totalDeduct WHERE BranchId = @BranchId AND ProductId = @ComponentProductId",
                                new { totalDeduct, model.BranchId, comp.ComponentProductId }, transaction);
                        }
                    }
                    // Service ဖြစ်လျှင် Stock နှုတ်ရန် မလိုပါ
                }

                // ၄။ Customer Payments & Balance
                if (model.PaidAmount > 0)
                {
                    var rawPayments = model.Payments ?? new List<PaymentDetailModel>();

                    // Keep only valid rows and trim method names.
                    var validPayments = rawPayments
                        .Where(p => p != null && !string.IsNullOrWhiteSpace(p.PaymentMethod) && p.Amount > 0)
                        .Select(p => new PaymentDetailModel
                        {
                            PaymentMethod = p.PaymentMethod.Trim(),
                            Amount = p.Amount
                        })
                        .ToList();

                    // Backward compatibility: old clients may not send Payments.
                    if (!validPayments.Any())
                    {
                        validPayments.Add(new PaymentDetailModel
                        {
                            PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Unknown" : model.PaymentMethod.Trim(),
                            Amount = model.PaidAmount
                        });
                    }

                    // Normalize amount mismatch by scaling to PaidAmount to avoid drift/rounding issues.
                    decimal paymentTotal = validPayments.Sum(x => x.Amount);
                    if (paymentTotal <= 0)
                    {
                        validPayments.Clear();
                        validPayments.Add(new PaymentDetailModel
                        {
                            PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Unknown" : model.PaymentMethod.Trim(),
                            Amount = model.PaidAmount
                        });
                        paymentTotal = model.PaidAmount;
                    }

                    decimal ratio = model.PaidAmount / paymentTotal;
                    decimal accumulated = 0;
                    bool canLinkPaymentToSale = await HasColumnAsync(conn, (MySqlTransaction)transaction, "CustomerPayments", "SaleId");

                    for (int i = 0; i < validPayments.Count; i++)
                    {
                        var p = validPayments[i];
                        decimal adjustedAmount;

                        if (i == validPayments.Count - 1)
                        {
                            adjustedAmount = Math.Max(0, model.PaidAmount - accumulated);
                        }
                        else
                        {
                            adjustedAmount = Math.Round(p.Amount * ratio, 2, MidpointRounding.AwayFromZero);
                            accumulated += adjustedAmount;
                        }

                        if (adjustedAmount <= 0) continue;

                        if (canLinkPaymentToSale)
                        {
                            await conn.ExecuteAsync(@"
                            INSERT INTO CustomerPayments (CustomerId, Amount, PaymentMethod, PaymentDate, SaleId) 
                            VALUES (@CustomerId, @Amount, @PaymentMethod, NOW(), @SaleId)",
                                new
                                {
                                    CustomerId = resolvedCustomerId,
                                    Amount = adjustedAmount,
                                    PaymentMethod = p.PaymentMethod,
                                    SaleId = saleId
                                }, transaction);
                        }
                        else
                        {
                            await conn.ExecuteAsync(@"
                            INSERT INTO CustomerPayments (CustomerId, Amount, PaymentMethod, PaymentDate) 
                            VALUES (@CustomerId, @Amount, @PaymentMethod, NOW())",
                                new
                                {
                                    CustomerId = resolvedCustomerId,
                                    Amount = adjustedAmount,
                                    PaymentMethod = p.PaymentMethod
                                }, transaction);
                        }
                    }
                }

                if (dueAmount > 0 && resolvedCustomerId.HasValue)
                {
                    await conn.ExecuteAsync("UPDATE Customers SET CurrentBalance = CurrentBalance + @dueAmount WHERE CustomerId = @CustomerId",
                        new { dueAmount, CustomerId = resolvedCustomerId }, transaction);
                }

                await transaction.CommitAsync();
                return Ok(new { SaleId = saleId, SaleNo = "SA" + saleId.ToString("D7") });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                if (ex is MySqlException { Number: 1062 } && !string.IsNullOrWhiteSpace(model.ClientSaleId))
                {
                    var existingSaleId = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT SaleId FROM Sales WHERE ClientSaleId = @ClientSaleId LIMIT 1",
                        new { ClientSaleId = model.ClientSaleId.Trim() });

                    if (existingSaleId.HasValue)
                    {
                        return Ok(new { SaleId = existingSaleId.Value, SaleNo = "SA" + existingSaleId.Value.ToString("D7"), Duplicate = true });
                    }
                }

                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpPost("create-preorder")]
        public async Task<IActionResult> CreatePreorder([FromBody] SaleEntryModel model)
        {
            var tenantDb = User.FindFirst("TenantDb")?.Value;
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(tenantDb));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsureSalesOrderStatusColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsureSalesSaleTypeColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsureBundleItemsSupportsUnitsAsync(conn, (MySqlTransaction)transaction);

                // 0. Resolve Customer
                int? resolvedCustomerId = model.CustomerId > 0 ? model.CustomerId : null;
                string cleanCustomerName = model.CustomerName?.Trim();

                if (!resolvedCustomerId.HasValue && !string.IsNullOrWhiteSpace(cleanCustomerName) &&
                    !cleanCustomerName.Equals("Walk-in", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedCustomerId = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT CustomerId FROM Customers WHERE LOWER(Name) = LOWER(@Name) LIMIT 1",
                        new { Name = cleanCustomerName }, transaction);

                    if (!resolvedCustomerId.HasValue)
                    {
                        string insertCustomerSql = "INSERT INTO Customers (Name, Phone, Address, CurrentBalance, CreatedAt) VALUES (@Name, '', '', 0, NOW()); SELECT LAST_INSERT_ID();";
                        resolvedCustomerId = await conn.ExecuteScalarAsync<int>(insertCustomerSql, new { Name = cleanCustomerName }, transaction);
                    }
                }

                decimal actualItemsTotal = model.Items.Where(i => !i.IsFOC).Sum(i => (i.Quantity * i.UnitPrice) - i.DiscountAmount);
                decimal finalTotalAmount = actualItemsTotal - model.DiscountAmount + model.DeliveryFee;
                decimal dueAmount = Math.Max(0, finalTotalAmount - model.PaidAmount);
                string finalSaleType = dueAmount > 0 ? (model.PaidAmount > 0 ? "Partial" : "Credit") : "Normal";

                int claimUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId) ? parsedUserId : 0;
                int effectiveUserId = claimUserId > 0 ? claimUserId : model.UserId;

                DateTime saleDate = model.SaleDate?.Date ?? DateTime.Now.Date;
                string saleSql = @"INSERT INTO Sales (BranchId, CustomerId, UserId, TotalAmount, DiscountAmount, DeliveryFee, PaidAmount, DueAmount, PaymentMethod, VoucherNote, SaleDate, SaleType, OrderStatus)  
                          VALUES (@BranchId, @CustomerId, @UserId, @actualItemsTotal, @DiscountAmount, @DeliveryFee, @PaidAmount, @dueAmount, @PaymentMethod, @VoucherNote, @SaleDate, @finalSaleType, @OrderStatus);
                          SELECT LAST_INSERT_ID();";

                int saleId = await conn.ExecuteScalarAsync<int>(saleSql, new
                {
                    model.BranchId,
                    CustomerId = resolvedCustomerId,
                    UserId = effectiveUserId,
                    actualItemsTotal,
                    model.DiscountAmount,
                    model.DeliveryFee,
                    model.PaidAmount,
                    dueAmount,
                    model.PaymentMethod,
                    VoucherNote = string.IsNullOrWhiteSpace(model.VoucherNote) ? (model.Note ?? "") : model.VoucherNote,
                    SaleDate = saleDate,
                    finalSaleType,
                    OrderStatus = "Preordered"
                }, transaction);

                foreach (var item in model.Items)
                {
                    string insertItemSql = @"INSERT INTO SaleItems (SaleId, ItemType, ProductId, ServiceId, BundleId, UnitId, Quantity, UnitPrice, DiscountAmount, IsFOC, CostPrice, SubTotal, Note, IsConsignment, ConsignItemId) 
                                     VALUES (@SaleId, @ItemType, @ProductId, @ServiceId, @BundleId, @UnitId, @Quantity, @UnitPrice, @DiscountAmount, @IsFOC, @CostPrice, @SubTotal, @Note, @IsConsignment, @ConsignItemId);
                                     SELECT LAST_INSERT_ID();";

                    int saleItemId = await conn.ExecuteScalarAsync<int>(insertItemSql, new
                    {
                        SaleId = saleId,
                        item.ItemType,
                        item.ProductId,
                        item.ServiceId,
                        item.BundleId,
                        item.UnitId,
                        item.Quantity,
                        UnitPrice = item.IsFOC ? 0 : item.UnitPrice,
                        DiscountAmount = item.IsFOC ? 0 : item.DiscountAmount,
                        item.IsFOC,
                        item.CostPrice,
                        SubTotal = item.IsFOC ? 0 : (item.Quantity * item.UnitPrice) - item.DiscountAmount,
                        item.Note,
                        item.IsConsignment,
                        item.ConsignItemId
                    }, transaction);

                    // Reserve stock immediately (unsellable temporarily)
                    if (item.ItemType == "Product")
                    {
                        decimal multiplier = item.Multiplier > 0 ? item.Multiplier : 1;
                        decimal baseQty = item.Quantity * multiplier;
                        if (item.ProductId.HasValue)
                        {
                            await DeductPurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, model.BranchId, item.ProductId.Value, baseQty);
                        }
                        await conn.ExecuteAsync("UPDATE ProductStocks SET Quantity = Quantity - @baseQty WHERE BranchId = @BranchId AND ProductId = @ProductId",
                            new { baseQty, model.BranchId, item.ProductId }, transaction);

                        if (!item.IsFOC && item.ProductId.HasValue)
                        {
                            var allocation = await AllocateConsignmentSaleAsync(
                                conn,
                                (MySqlTransaction)transaction,
                                model.BranchId,
                                item.ProductId.Value,
                                baseQty);

                            if (allocation.IsConsignment)
                            {
                                await conn.ExecuteAsync(@"
UPDATE SaleItems
SET IsConsignment = 1,
    ConsignItemId = @ConsignItemId,
    CostPrice = COALESCE(@UnitCost, CostPrice)
WHERE SaleItemId = @SaleItemId",
                                    new
                                    {
                                        allocation.ConsignItemId,
                                        allocation.UnitCost,
                                        SaleItemId = saleItemId
                                    },
                                    transaction);
                            }
                        }
                    }
                    else if (item.ItemType == "Bundle")
                    {
                        var components = await conn.QueryAsync<dynamic>(
                            @"SELECT bi.ComponentProductId, bi.Quantity, IFNULL(uc.Multiplier, 1) as UnitMultiplier
                              FROM BundleItems bi
                              LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                              WHERE bi.BundleId = @BundleId AND bi.ComponentProductId IS NOT NULL AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'",
                            new { item.BundleId }, transaction);

                        foreach (var comp in components)
                        {
                            decimal totalDeduct = Convert.ToDecimal(comp.Quantity) * Convert.ToDecimal(comp.UnitMultiplier) * item.Quantity;
                            await DeductPurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, model.BranchId, Convert.ToInt32(comp.ComponentProductId), totalDeduct);
                            await conn.ExecuteAsync("UPDATE ProductStocks SET Quantity = Quantity - @totalDeduct WHERE BranchId = @BranchId AND ProductId = @ComponentProductId",
                                new { totalDeduct, model.BranchId, comp.ComponentProductId }, transaction);
                        }
                    }
                }

                if (model.PaidAmount > 0)
                {
                    var rawPayments = model.Payments ?? new List<PaymentDetailModel>();

                    var validPayments = rawPayments
                        .Where(p => p != null && !string.IsNullOrWhiteSpace(p.PaymentMethod) && p.Amount > 0)
                        .Select(p => new PaymentDetailModel
                        {
                            PaymentMethod = p.PaymentMethod.Trim(),
                            Amount = p.Amount
                        })
                        .ToList();

                    if (!validPayments.Any())
                    {
                        validPayments.Add(new PaymentDetailModel
                        {
                            PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Unknown" : model.PaymentMethod.Trim(),
                            Amount = model.PaidAmount
                        });
                    }

                    decimal paymentTotal = validPayments.Sum(x => x.Amount);
                    if (paymentTotal <= 0)
                    {
                        validPayments.Clear();
                        validPayments.Add(new PaymentDetailModel
                        {
                            PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Unknown" : model.PaymentMethod.Trim(),
                            Amount = model.PaidAmount
                        });
                        paymentTotal = model.PaidAmount;
                    }

                    decimal ratio = model.PaidAmount / paymentTotal;
                    decimal accumulated = 0;
                    bool canLinkPaymentToSale = await HasColumnAsync(conn, (MySqlTransaction)transaction, "CustomerPayments", "SaleId");

                    for (int i = 0; i < validPayments.Count; i++)
                    {
                        var p = validPayments[i];
                        decimal adjustedAmount;

                        if (i == validPayments.Count - 1)
                        {
                            adjustedAmount = Math.Max(0, model.PaidAmount - accumulated);
                        }
                        else
                        {
                            adjustedAmount = Math.Round(p.Amount * ratio, 2, MidpointRounding.AwayFromZero);
                            accumulated += adjustedAmount;
                        }

                        if (adjustedAmount <= 0) continue;

                        if (canLinkPaymentToSale)
                        {
                            await conn.ExecuteAsync(@"
                            INSERT INTO CustomerPayments (CustomerId, Amount, PaymentMethod, PaymentDate, SaleId) 
                            VALUES (@CustomerId, @Amount, @PaymentMethod, NOW(), @SaleId)",
                                new
                                {
                                    CustomerId = resolvedCustomerId,
                                    Amount = adjustedAmount,
                                    PaymentMethod = p.PaymentMethod,
                                    SaleId = saleId
                                }, transaction);
                        }
                        else
                        {
                            await conn.ExecuteAsync(@"
                            INSERT INTO CustomerPayments (CustomerId, Amount, PaymentMethod, PaymentDate) 
                            VALUES (@CustomerId, @Amount, @PaymentMethod, NOW())",
                                new
                                {
                                    CustomerId = resolvedCustomerId,
                                    Amount = adjustedAmount,
                                    PaymentMethod = p.PaymentMethod
                                }, transaction);
                        }
                    }
                }

                if (dueAmount > 0 && resolvedCustomerId.HasValue)
                {
                    await conn.ExecuteAsync("UPDATE Customers SET CurrentBalance = CurrentBalance + @dueAmount WHERE CustomerId = @CustomerId",
                        new { dueAmount, CustomerId = resolvedCustomerId }, transaction);
                }

                await transaction.CommitAsync();
                return Ok(new { SaleId = saleId, SaleNo = "SA" + saleId.ToString("D7") });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        public class UpdateOrderStatusRequest
        {
            public string OrderStatus { get; set; } = "";
        }

        public class CollectOrderPaymentRequest
        {
            public decimal Amount { get; set; }
            public string PaymentMethod { get; set; } = "Cash";
            public string? ReferenceNo { get; set; }
            public string? Note { get; set; }
            public DateTime? PaymentDate { get; set; }
        }

        [HttpPost("{saleId}/collect-payment")]
        public async Task<IActionResult> CollectOrderPayment(int saleId, [FromBody] CollectOrderPaymentRequest req)
        {
            if (saleId <= 0) return BadRequest(new { Message = "Invalid saleId." });
            if (req == null) return BadRequest(new { Message = "Invalid request." });
            if (req.Amount <= 0) return BadRequest(new { Message = "Payment amount must be greater than zero." });

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // Lock sale row
                var sale = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT
    s.SaleId,
    s.CustomerId,
    GREATEST(IFNULL(s.DueAmount, 0) - IFNULL(sr.ReturnedAmount, 0), 0) AS DueAmount
FROM Sales s
LEFT JOIN (
    SELECT SaleId, SUM(RefundAmount) AS ReturnedAmount
    FROM SaleReturns
    GROUP BY SaleId
) sr ON sr.SaleId = s.SaleId
WHERE s.SaleId = @saleId
FOR UPDATE",
                    new { saleId }, transaction);

                if (sale == null) return NotFound(new { Message = "Sale not found." });

                int? customerId = sale.CustomerId as int?;
                decimal dueAmount = (decimal)(sale.DueAmount ?? 0m);
                if (dueAmount <= 0) return BadRequest(new { Message = "This order has no remaining balance." });
                if (req.Amount > dueAmount) return BadRequest(new { Message = "Payment amount cannot exceed remaining balance for this order." });
                if (!customerId.HasValue) return BadRequest(new { Message = "This order is not linked to a customer." });

                var payDate = req.PaymentDate ?? DateTime.Now;
                var method = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "Cash" : req.PaymentMethod.Trim();

                await conn.ExecuteAsync(@"
UPDATE Sales
SET PaidAmount = PaidAmount + @Amount,
    DueAmount = GREATEST(DueAmount - @Amount, 0)
WHERE SaleId = @SaleId",
                    new { Amount = req.Amount, SaleId = saleId }, transaction);

                await conn.ExecuteAsync(@"
INSERT INTO CustomerPayments
    (CustomerId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate, SaleId)
VALUES
    (@CustomerId, @Amount, @PaymentMethod, @ReferenceNo, @Note, @PaymentDate, @SaleId)",
                    new
                    {
                        CustomerId = customerId.Value,
                        Amount = req.Amount,
                        PaymentMethod = method,
                        ReferenceNo = req.ReferenceNo ?? "",
                        Note = req.Note ?? "",
                        PaymentDate = payDate,
                        SaleId = saleId
                    }, transaction);

                // Keep customer balance in sync (but payment is applied to this sale only)
                await conn.ExecuteAsync(@"
UPDATE Customers
SET CurrentBalance = GREATEST(CurrentBalance - @Amount, 0)
WHERE CustomerId = @CustomerId",
                    new { Amount = req.Amount, CustomerId = customerId.Value }, transaction);

                await transaction.CommitAsync();
                return Ok(new { Message = "Payment recorded.", SaleId = saleId, Amount = req.Amount });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Error: " + ex.Message });
            }
        }

        [HttpPut("orders/{saleId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int saleId, [FromBody] UpdateOrderStatusRequest req)
        {
            if (saleId <= 0) return BadRequest(new { Message = "Invalid saleId." });
            var nextStatus = (req?.OrderStatus ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nextStatus)) return BadRequest(new { Message = "OrderStatus is required." });

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsureSalesOrderStatusColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);
                await EnsureBundleItemsSupportsUnitsAsync(conn, (MySqlTransaction)transaction);

                var saleRow = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT
    s.SaleId,
    s.BranchId,
    IFNULL(s.OrderStatus,'Completed') AS OrderStatus,
    GREATEST(IFNULL(s.DueAmount, 0) - IFNULL(sr.ReturnedAmount, 0), 0) AS DueAmount
FROM Sales s
LEFT JOIN (
    SELECT SaleId, SUM(RefundAmount) AS ReturnedAmount
    FROM SaleReturns
    GROUP BY SaleId
) sr ON sr.SaleId = s.SaleId
WHERE s.SaleId = @saleId
FOR UPDATE",
                    new { saleId }, transaction);

                if (saleRow == null) return NotFound(new { Message = "Sale not found." });

                string currentStatus = (string)(saleRow.OrderStatus ?? "Completed");
                int branchId = (int)saleRow.BranchId;
                decimal dueAmount = (decimal)(saleRow.DueAmount ?? 0m);

                // Enforce full payment before completing/delivering
                if ((string.Equals(nextStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(nextStatus, "Delivered", StringComparison.OrdinalIgnoreCase)) &&
                    dueAmount > 0)
                {
                    return BadRequest(new { Message = "Cannot complete this order while remaining balance exists. Please collect remaining payment first." });
                }

                // If canceling, restore the deducted stock (so items become sellable again)
                if (!string.Equals(currentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(nextStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                {
                    var items = (await conn.QueryAsync<dynamic>(@"
SELECT ItemType, ProductId, BundleId, Quantity, UnitId
FROM SaleItems
WHERE SaleId = @saleId",
                        new { saleId }, transaction)).ToList();

                    foreach (var item in items)
                    {
                        string itemType = (string)(item.ItemType ?? "");
                        if (string.Equals(itemType, "Product", StringComparison.OrdinalIgnoreCase))
                        {
                            int? productId = item.ProductId as int?;
                            if (!productId.HasValue) continue;

                            decimal qty = Convert.ToDecimal(item.Quantity);
                            int? unitId = item.UnitId as int?;

                            decimal multiplier = 1;
                            if (unitId.HasValue)
                            {
                                multiplier = await conn.QueryFirstOrDefaultAsync<decimal?>(@"
SELECT Multiplier
FROM UnitConversions
WHERE ProductId = @ProductId AND FromUnitId = @UnitId
LIMIT 1",
                                    new { ProductId = productId.Value, UnitId = unitId.Value }, transaction) ?? 1;

                                if (multiplier <= 0) multiplier = 1;
                            }

                            decimal baseQty = qty * multiplier;
                            await RestorePurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, branchId, productId.Value, baseQty);
                            await conn.ExecuteAsync(
                                "UPDATE ProductStocks SET Quantity = Quantity + @baseQty WHERE BranchId = @branchId AND ProductId = @productId",
                                new { baseQty, branchId, productId = productId.Value }, transaction);
                        }
                        else if (string.Equals(itemType, "Bundle", StringComparison.OrdinalIgnoreCase))
                        {
                            int? bundleId = item.BundleId as int?;
                            if (!bundleId.HasValue) continue;

                            decimal bundleQty = Convert.ToDecimal(item.Quantity);
                            var components = await conn.QueryAsync<dynamic>(
                                @"SELECT bi.ComponentProductId, bi.Quantity, IFNULL(uc.Multiplier, 1) as UnitMultiplier
                                  FROM BundleItems bi
                                  LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                                  WHERE bi.BundleId = @BundleId AND bi.ComponentProductId IS NOT NULL AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'",
                                new { BundleId = bundleId.Value }, transaction);

                            foreach (var comp in components)
                            {
                                decimal compQty = Convert.ToDecimal(comp.Quantity) * Convert.ToDecimal(comp.UnitMultiplier) * bundleQty;
                                await RestorePurchaseBatchesFefoAsync(conn, (MySqlTransaction)transaction, branchId, (int)comp.ComponentProductId, compQty);
                                await conn.ExecuteAsync(
                                    "UPDATE ProductStocks SET Quantity = Quantity + @qty WHERE BranchId = @branchId AND ProductId = @productId",
                                    new { qty = compQty, branchId, productId = (int)comp.ComponentProductId }, transaction);
                            }
                        }
                    }
                }

                await conn.ExecuteAsync(
                    "UPDATE Sales SET OrderStatus = @nextStatus WHERE SaleId = @saleId",
                    new { nextStatus, saleId }, transaction);

                await transaction.CommitAsync();
                return Ok(new { SaleId = saleId, OrderStatus = nextStatus });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("find-voucher/{saleId}")]
        public async Task<IActionResult> GetVoucherItems(int saleId, [FromQuery] int? branchId = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // Voucher ထဲက ပစ္စည်းစာရင်းနဲ့ လက်ရှိရောင်းထားတဲ့ ဈေးနှုန်းကို ယူခြင်း
            string sql = @"
            SELECT 
                si.*,
                CASE 
                    WHEN si.ItemType = 'Product' THEN p.Name
                    WHEN si.ItemType = 'Service' THEN sv.ServiceName
                    WHEN si.ItemType = 'Bundle' THEN b.BundleName
                    ELSE COALESCE(p.Name, sv.ServiceName, b.BundleName, 'Unknown')
                END as ProductName,
                u.UnitName,
                si.Note
            FROM SaleItems si
            JOIN Sales s ON si.SaleId = s.SaleId
            LEFT JOIN Products p ON si.ProductId = p.ProductId
            LEFT JOIN Services sv ON si.ServiceId = sv.ServiceId
            LEFT JOIN Bundles b ON si.BundleId = b.BundleId
            LEFT JOIN Units u ON si.UnitId = u.UnitId
            WHERE si.SaleId = @saleId 
            AND (@branchId IS NULL OR s.BranchId = @branchId)";

            var items = await conn.QueryAsync<dynamic>(sql, new { saleId, branchId });

            if (items == null || !items.Any())
                return NotFound(new { Message = "Voucher ရှာမတွေ့ပါ။ (သို့မဟုတ် အခြားဆိုင်မှ ဘောက်ချာဖြစ်နေသည်)" });

            return Ok(items);
        }

        [HttpGet("for-sale/{branchId}")]
        public async Task<IActionResult> GetProductsForSale(int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // ၁။ Products နှင့် လက်ရှိ Stock ကို ဆွဲထုတ်ခြင်း
            string sql = @"
        SELECT p.ProductId, p.Name, p.Sku, p.SalePrice, p.CostPrice, p.CategoryId,
               u.UnitId, u.UnitName,
               IFNULL(ps.Quantity, 0) as StockQuantity,
               IFNULL(cons.ConsignmentQuantity, 0) as ConsignmentQuantity,
               GREATEST(IFNULL(ps.Quantity, 0) - IFNULL(cons.ConsignmentQuantity, 0), 0) as OwnedQuantity
        FROM Products p
        JOIN Units u ON p.UnitId = u.UnitId
        LEFT JOIN ProductStocks ps ON p.ProductId = ps.ProductId AND ps.BranchId = @branchId
        LEFT JOIN (
            SELECT ci.ProductId,
                   SUM(ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity) AS ConsignmentQuantity
            FROM ConsignmentItems ci
            JOIN Consignments c ON c.ConsignId = ci.ConsignId
            WHERE COALESCE(ci.BranchId, c.BranchId) = @branchId
              AND c.Status = 'Active'
            GROUP BY ci.ProductId
        ) cons ON cons.ProductId = p.ProductId
        WHERE p.IsActive = 1";

            var products = (await conn.QueryAsync<dynamic>(sql, new { branchId })).ToList();

            // ၂။ ပစ္စည်းတစ်ခုချင်းစီအတွက် Multiple Price Levels များကို လိုက်ထည့်ပေးခြင်း
            foreach (var p in products)
            {
                string priceSql = "SELECT PriceId, PriceName, PriceValue, MinQuantity, UnitId FROM ProductPrices WHERE ProductId = @ProductId";
                p.AdditionalPrices = await conn.QueryAsync<dynamic>(priceSql, new { ProductId = p.ProductId });
            }

            return Ok(products);
        }

        [HttpPost("customer-payment")]
        public async Task<IActionResult> RecordCustomerPayment([FromBody] CustomerPaymentModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ၁။ ငွေပေးချေမှုမှတ်တမ်းသွင်းခြင်း
                string paySql = @"INSERT INTO CustomerPayments (CustomerId, Amount, PaymentMethod, PaymentDate) 
                          VALUES (@CustomerId, @Amount, @PaymentMethod, NOW())";
                await conn.ExecuteAsync(paySql, model, transaction);

                // ၂။ Customer ရဲ့ လက်ကျန်အကြွေးစာရင်းကို လျှော့ချခြင်း
                string updateCustSql = "UPDATE Customers SET CurrentBalance = CurrentBalance - @Amount WHERE CustomerId = @CustomerId";
                await conn.ExecuteAsync(updateCustSql, new { model.Amount, model.CustomerId }, transaction);

                await transaction.CommitAsync();
                return Ok(new { Message = "Payment recorded and customer balance updated." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("dashboard-summary/{branchId}")]
        public async Task<IActionResult> GetDashboardSummary(int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // ဒီနေ့ရက်စွဲအတွက် အရောင်းအချက်အလက်များကို တွက်ချက်ခြင်း
            string sql = @"
        SELECT 
            COUNT(SaleId) as TotalInvoices,
            SUM(TotalAmount) as TotalSales,
            SUM(PaidAmount) as TotalCollected,
            SUM(DueAmount) as TotalCredit,
            SUM(DiscountAmount) as TotalDiscount
        FROM Sales 
        WHERE BranchId = @branchId AND DATE(SaleDate) = CURDATE()";

            var summary = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { branchId });

            // Profit တွက်ချက်ခြင်း (SaleItems ရှိ CostPrice နှင့် UnitPrice နှိုင်းယှဉ်ချက်အရ) - Fallback to current cost if 0
            string profitSql = @"
        SELECT IFNULL(SUM((si.UnitPrice - COALESCE(NULLIF(si.CostPrice, 0), p.CostPrice, 0)) * si.Quantity), 0) 
        FROM SaleItems si
        JOIN Sales s ON si.SaleId = s.SaleId
        LEFT JOIN Products p ON si.ProductId = p.ProductId
        WHERE s.BranchId = @branchId AND DATE(s.SaleDate) = CURDATE()";

            decimal grossProfit = await conn.ExecuteScalarAsync<decimal>(profitSql, new { branchId });

            return Ok(new
            {
                TodaySummary = summary,
                GrossProfit = grossProfit
            });
        }

        [HttpGet("history/{branchId}")]
        public async Task<IActionResult> GetSalesHistory(int branchId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // SQL Dump ရှိ Table များအတိုင်း Join ထားပါသည်
            string sql = @"
        SELECT 
            s.SaleId, 
            COALESCE(c.Name, 'Walk-in') as CustomerName, 
            u.Username as CashierName, 
            s.TotalAmount, s.PaidAmount, s.DueAmount, s.SaleType, s.SaleDate
        FROM Sales s
        LEFT JOIN Customers c ON s.CustomerId = c.CustomerId
        JOIN ShopUsers u ON s.UserId = u.UserId
        WHERE s.BranchId = @branchId";

            // Date Filter ပါဝင်လျှင် ထည့်သွင်းစစ်ဆေးရန်
            if (fromDate.HasValue && toDate.HasValue)
            {
                sql += " AND s.SaleDate BETWEEN @fromDate AND @toDate";
            }

            sql += " ORDER BY s.SaleDate DESC";

            var history = await conn.QueryAsync<SaleHistoryViewModel>(sql, new { branchId, fromDate, toDate });
            return Ok(history);
        }

        [HttpGet("details/{saleId:int}")]
        public async Task<IActionResult> GetSaleDetails(int saleId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // ၁။ အရောင်း Master Data ကိုအရင်ယူသည်
            string masterSql = @"
        SELECT s.*, COALESCE(c.Name, 'Walk-in') as CustomerName, c.Phone as CustomerPhone, c.Address as CustomerAddress, COALESCE(u.Username, 'System') as CashierName
        FROM Sales s 
        LEFT JOIN Customers c ON s.CustomerId = c.CustomerId 
        LEFT JOIN ShopUsers u ON s.UserId = u.UserId
        WHERE s.SaleId = @saleId";

            var master = await conn.QueryFirstOrDefaultAsync<dynamic>(masterSql, new { saleId });

            if (master == null) return NotFound();

            // ၂။ အရောင်းထဲက ပစ္စည်းများစာရင်း (Product, Service, Bundle အားလုံးပါဝင်စေရန်)
            string itemsSql = @"
        SELECT 
            si.*,
            CASE 
                WHEN si.ItemType = 'Product' THEN p.Name
                WHEN si.ItemType = 'Service' THEN sv.ServiceName
                WHEN si.ItemType = 'Bundle' THEN b.BundleName
                ELSE 'Unknown Item'
            END AS ItemName,
            u.UnitName
        FROM SaleItems si
        LEFT JOIN Products p ON si.ProductId = p.ProductId
        LEFT JOIN Services sv ON si.ServiceId = sv.ServiceId
        LEFT JOIN Bundles b ON si.BundleId = b.BundleId
        LEFT JOIN Units u ON si.UnitId = u.UnitId
        WHERE si.SaleId = @saleId";

            var items = await conn.QueryAsync<dynamic>(itemsSql, new { saleId });

            // Frontend HistoryCenter.vue နှင့် ကိုက်ညီစေရန် Object Key ကို 'sale' နှင့် 'items' ဟု ပေးပါမည်
            return Ok(new { sale = master, items = items });
        }

        [HttpPost("quick-sale")]
        public async Task<IActionResult> QuickSale([FromBody] QuickSaleRequest model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);

                // 0. Handle Category Auto-creation
                int? resolvedCategoryId = model.CategoryId > 0 ? model.CategoryId : null;
                string cleanCatName = model.CategoryName?.Trim();
                if (!resolvedCategoryId.HasValue && !string.IsNullOrEmpty(cleanCatName))
                {
                    string checkCat = "SELECT CategoryId FROM Categories WHERE LOWER(CategoryName) = LOWER(@CategoryName) LIMIT 1";
                    resolvedCategoryId = await conn.QueryFirstOrDefaultAsync<int?>(checkCat, new { CategoryName = cleanCatName }, transaction);
                    if (!resolvedCategoryId.HasValue)
                    {
                        string insCat = "INSERT INTO Categories (CategoryName) VALUES (@CategoryName); SELECT LAST_INSERT_ID();";
                        resolvedCategoryId = await conn.ExecuteScalarAsync<int>(insCat, new { CategoryName = cleanCatName }, transaction);
                    }
                }

                // 0. Handle Unit Auto-creation
                int? resolvedUnitId = model.UnitId > 0 ? model.UnitId : null;
                string cleanUnitName = model.UnitName?.Trim();
                if (!resolvedUnitId.HasValue && !string.IsNullOrEmpty(cleanUnitName))
                {
                    string checkUnit = "SELECT UnitId FROM Units WHERE LOWER(UnitName) = LOWER(@UnitName) LIMIT 1";
                    resolvedUnitId = await conn.QueryFirstOrDefaultAsync<int?>(checkUnit, new { UnitName = cleanUnitName }, transaction);
                    if (!resolvedUnitId.HasValue)
                    {
                        string insUnit = "INSERT INTO Units (UnitName, ShortName) VALUES (@UnitName, @ShortName); SELECT LAST_INSERT_ID();";
                        resolvedUnitId = await conn.ExecuteScalarAsync<int>(insUnit, new { UnitName = cleanUnitName, ShortName = cleanUnitName }, transaction);
                    }
                }

                // Ensure UnitId is always present (Products.UnitId is required)
                if (!resolvedUnitId.HasValue)
                {
                    const string fallbackUnitName = "PCS";
                    resolvedUnitId = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT UnitId FROM Units WHERE LOWER(UnitName) = LOWER(@UnitName) LIMIT 1",
                        new { UnitName = fallbackUnitName },
                        transaction);

                    if (!resolvedUnitId.HasValue)
                    {
                        resolvedUnitId = await conn.ExecuteScalarAsync<int>(
                            "INSERT INTO Units (UnitName, ShortName) VALUES (@UnitName, @ShortName); SELECT LAST_INSERT_ID();",
                            new { UnitName = fallbackUnitName, ShortName = fallbackUnitName },
                            transaction);
                    }
                }

                // 0. Resolve Supplier
                int? resolvedSupplierId = model.SupplierId > 0 ? model.SupplierId : null;
                if (!resolvedSupplierId.HasValue && !string.IsNullOrWhiteSpace(model.SupplierName))
                {
                    resolvedSupplierId = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT SupplierId FROM Suppliers WHERE SupplierName = @SupplierName", 
                        new { model.SupplierName }, transaction);

                    if (!resolvedSupplierId.HasValue)
                    {
                        string insertSupplierSql = "INSERT INTO Suppliers (SupplierName, ContactPerson, Phone, Address) VALUES (@SupplierName, '', '', ''); SELECT LAST_INSERT_ID();";
                        resolvedSupplierId = await conn.ExecuteScalarAsync<int>(insertSupplierSql, new { model.SupplierName }, transaction);
                    }
                }

                // Ensure SupplierId is always present (PurchaseOrders.SupplierId is required)
                if (!resolvedSupplierId.HasValue)
                {
                    const string fallbackSupplierName = "Quick Sale Supplier";
                    resolvedSupplierId = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT SupplierId FROM Suppliers WHERE SupplierName = @SupplierName LIMIT 1",
                        new { SupplierName = fallbackSupplierName },
                        transaction);

                    if (!resolvedSupplierId.HasValue)
                    {
                        resolvedSupplierId = await conn.ExecuteScalarAsync<int>(
                            "INSERT INTO Suppliers (SupplierName, ContactPerson, Phone, Address) VALUES (@SupplierName, '', '', ''); SELECT LAST_INSERT_ID();",
                            new { SupplierName = fallbackSupplierName },
                            transaction);
                    }
                }

                // 1. Create Product
                string productSql = @"INSERT INTO Products (Name, Sku, CategoryId, UnitId, CostPrice, SalePrice, AlertQuantity) 
                                     VALUES (@Name, @Sku, @CategoryId, @UnitId, @CostPrice, @SalePrice, 0); 
                                     SELECT LAST_INSERT_ID();";
                string resolvedSku = string.IsNullOrWhiteSpace(model.Sku)
                    ? $"QS{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 1000)}"
                    : model.Sku.Trim();
                int productId = await conn.ExecuteScalarAsync<int>(productSql, new {
                    model.Name,
                    Sku = resolvedSku,
                    CategoryId = resolvedCategoryId,
                    UnitId = resolvedUnitId,
                    model.CostPrice,
                    model.SalePrice
                }, transaction);

                // 2. Create Purchase Order
                string purchaseSql = @"INSERT INTO PurchaseOrders (SupplierId, BranchId, TotalAmount, PaidAmount, Status, Remarks, OrderDate) 
                                     VALUES (@SupplierId, @BranchId, @TotalAmount, @PaidAmount, 'Paid', 'Quick Sale Registration', NOW());
                                     SELECT LAST_INSERT_ID();";
                decimal totalCost = model.CostPrice * model.Quantity;
                int poId = await conn.ExecuteScalarAsync<int>(purchaseSql, new {
                    SupplierId = resolvedSupplierId,
                    model.BranchId,
                    TotalAmount = totalCost,
                    PaidAmount = totalCost
                }, transaction);

                // 3. Create Purchase Order Item
                string poiSql = @"INSERT INTO PurchaseOrderItems (POId, ProductId, UnitId, Quantity, FOCQuantity, CostPrice, ExpiryDate, AlertDays, RemainingQuantity) 
                                 VALUES (@POId, @ProductId, @UnitId, @Quantity, 0, @CostPrice, NULL, 30, @RemainingQuantity)";
                await conn.ExecuteAsync(poiSql, new {
                    POId = poId,
                    ProductId = productId,
                    UnitId = resolvedUnitId,
                    model.Quantity,
                    model.CostPrice,
                    RemainingQuantity = model.Quantity
                }, transaction);

                // 4. Update Stock
                string stockSql = @"INSERT INTO ProductStocks (BranchId, ProductId, Quantity) 
                                   VALUES (@BranchId, @ProductId, @Quantity) 
                                   ON DUPLICATE KEY UPDATE Quantity = Quantity + @Quantity";
                await conn.ExecuteAsync(stockSql, new {
                    model.BranchId,
                    ProductId = productId,
                    model.Quantity
                }, transaction);

                await transaction.CommitAsync();

                return Ok(new { 
                    ProductId = productId, 
                    Name = model.Name, 
                    SalePrice = model.SalePrice, 
                    CostPrice = model.CostPrice,
                    UnitId = resolvedUnitId,
                    StockQuantity = model.Quantity,
                    ItemType = "Product"
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

    }
}
