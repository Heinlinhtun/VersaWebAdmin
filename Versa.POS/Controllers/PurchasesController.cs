using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PurchasesController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public PurchasesController(DatabaseService dbService)
        {
            _dbService = dbService;
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
                    "ALTER TABLE PurchaseOrderItems ADD COLUMN RemainingQuantity DECIMAL(18,5) NULL",
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

        [HttpGet("detail-by-invoice/{invoiceNo}")]
        public async Task<IActionResult> GetPurchaseDetail(string invoiceNo)
        {
            // Invoice နံပါတ် (PU0000001) မှ ID ကို ပြန်ထုတ်ခြင်း
            if (!invoiceNo.StartsWith("PU")) return BadRequest("Invalid Invoice Number");
            int poId = int.Parse(invoiceNo.Substring(2));

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // Purchase Items များနှင့် Product နာမည်၊ Unit နာမည်များကို Join ယူခြင်း
            string sql = @"
        SELECT poi.*, p.Name as ProductName, u.UnitName
        FROM PurchaseOrderItems poi
        JOIN Products p ON poi.ProductId = p.ProductId
        LEFT JOIN Units u ON poi.UnitId = u.UnitId
        WHERE poi.POId = @poId";

            var items = await conn.QueryAsync<dynamic>(sql, new { poId });
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePurchase([FromBody] PurchaseEntryModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);

                if (model.SupplierId <= 0 || model.Items == null || model.Items.Count == 0)
                    return BadRequest(new { Message = "Supplier and purchase items are required." });

                if (model.Items.Any(item => item.ProductId <= 0 || item.UnitId <= 0 || item.Quantity <= 0 || item.CostPrice < 0))
                    return BadRequest(new { Message = "Invalid purchase item. Product, unit, quantity and cost are required." });

                decimal total = model.TotalAmount;
                decimal paid = Math.Max(0m, model.PaidAmount.GetValueOrDefault(0m));
                decimal due = total - paid;

                // ၁။ Status သတ်မှတ်ခြင်း Logic
                string poStatus;
                if (paid <= 0) poStatus = "Unpaid";
                else if (paid < total) poStatus = "Partial";
                else poStatus = "Paid";

                var purchaseDate = model.PurchaseDate ?? DateTime.Now;

                // ၂။ Purchase Order (Voucher Master) သွင်းခြင်း
                string purchaseSql = @"INSERT INTO PurchaseOrders (SupplierId, BranchId, TotalAmount, PaidAmount, Status, Remarks, OrderDate) 
                             VALUES (@SupplierId, @BranchId, @TotalAmount, @PaidAmount, @Status, @Remarks, @OrderDate);
                             SELECT LAST_INSERT_ID();";

                int poId = await conn.ExecuteScalarAsync<int>(purchaseSql, new
                {
                    model.SupplierId,
                    model.BranchId,
                    model.TotalAmount,
                    PaidAmount = paid,
                    model.Remarks, // Remarks ပါ တစ်ခါတည်းသိမ်းသည်
                    OrderDate = purchaseDate,
                    Status = poStatus
                }, transaction);

                string invoiceNo = $"PU{poId:D7}";

                // ၃။ SupplierPayments ထဲသို့ အသေးစိတ် Note ဖြင့် သွင်းခြင်း
                if (paid > 0)
                {
                    string paymentNote = $"Paid for Purchase {invoiceNo}. (Total: {total:N0}, Paid: {paid:N0}, {due:N0} added to debt)";
                    string paymentSql = @"INSERT INTO SupplierPayments (SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate) 
                                VALUES (@SupplierId, @Amount, @PaymentMethod, @ReferenceNo, @Note, @PaymentDate)";

                    await conn.ExecuteAsync(paymentSql, new
                    {
                        SupplierId = model.SupplierId,
                        Amount = paid,
                        PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod.Trim(),
                        ReferenceNo = invoiceNo,
                        Note = paymentNote,
                        PaymentDate = purchaseDate
                    }, transaction);
                }

                // ၄။ Supplier Debt Update (ကြွေးကျန်ရှိမှသာ ပေါင်းထည့်မည်)
                if (due > 0)
                {
                    string updateSupplierDebtSql = "UPDATE Suppliers SET CurrentDebt = CurrentDebt + @due WHERE SupplierId = @SupplierId";
                    await conn.ExecuteAsync(updateSupplierDebtSql, new { due, model.SupplierId }, transaction);
                }

                var insertedItemCount = 0;

                foreach (var item in model.Items)
                {
                    // ၅။ Unit Multiplier ရှာဖွေခြင်း
                    var conversion = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                        new { item.ProductId, item.UnitId }, transaction);

                    decimal multiplier = conversion?.Multiplier ?? 1;
                    decimal baseUnitCost = multiplier > 0 ? (item.CostPrice / multiplier) : item.CostPrice;

                    // ၆။ လက်ရှိ Stock အခြေအနေနှင့် ရင်းဈေးအဟောင်းကို ရယူခြင်း (Moving Average အတွက်)
                    var currentProductData = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT 
                    p.CostPrice as OldCost, 
                    IFNULL(SUM(ps.Quantity), 0) as CurrentTotalStock 
                FROM Products p 
                LEFT JOIN ProductStocks ps ON p.ProductId = ps.ProductId 
                WHERE p.ProductId = @ProductId
                GROUP BY p.ProductId, p.CostPrice",
                        new { item.ProductId }, transaction);

                    decimal oldCost = currentProductData?.OldCost ?? 0m;
                    decimal currentStock = currentProductData?.CurrentTotalStock ?? 0m;

                    // ၇။ FOC နှင့် အရေအတွက် တွက်ချက်ခြင်း
                    decimal focQty = item.FOCQuantity ?? 0m;
                    decimal totalInwardQty = item.Quantity + focQty; // ဥပမာ - ၁၀ + ၁ = ၁၁
                    decimal totalInwardBaseQty = totalInwardQty * multiplier; // stock is stored in base-unit quantity

                    // ၈။ 🔥 Moving Weighted Average Cost တွက်ချက်ခြင်း
                    // ပုံသေနည်း- ((လက်ကျန် * ရင်းဈေးဟောင်း) + (ဝယ်ယူသည့်အရေအတွက် * ဝယ်ဈေးသစ်)) / (လက်ကျန် + ရရှိသည့်အရေအတွက်အားလုံး)
                    decimal calculatedNewCost;
                    if (currentStock + totalInwardBaseQty > 0)
                    {
                        // NOTE: currentStock is base-unit quantity; Costs are stored as base-unit cost.
                        // Purchased item.CostPrice is in the selected unit; convert to base-unit cost before averaging.
                        var purchasedBaseQty = item.Quantity * multiplier;
                        calculatedNewCost = ((currentStock * oldCost) + (purchasedBaseQty * baseUnitCost)) / (currentStock + totalInwardBaseQty);
                    }
                    else
                    {
                        calculatedNewCost = baseUnitCost;
                    }

                    // ၉။ PurchaseOrderItems ထဲသို့ Record သွင်းခြင်း
                    string itemSql = @"INSERT INTO PurchaseOrderItems (POId, ProductId, UnitId, Quantity, FOCQuantity, CostPrice, ExpiryDate, AlertDays, RemainingQuantity) 
                             VALUES (@POId, @ProductId, @UnitId, @Quantity, @FOCQuantity, @CostPrice, @ExpiryDate, @AlertDays, @RemainingQuantity)";

                    insertedItemCount += await conn.ExecuteAsync(itemSql, new
                    {
                        POId = poId,
                        item.ProductId,
                        item.UnitId,
                        item.Quantity,
                        FOCQuantity = focQty,
                        item.CostPrice, // မူရင်းဝယ်ဈေးအတိုင်း မှတ်တမ်းတင်သည်
                        item.ExpiryDate,
                        item.AlertDays,
                        RemainingQuantity = totalInwardBaseQty
                    }, transaction);

                    // ၁၀။ Stock Update (Base Unit သို့ပြောင်း၍ ပေါင်းထည့်ခြင်း)
                    decimal baseQuantity = totalInwardQty * multiplier;
                    string stockSql = @"INSERT INTO ProductStocks (BranchId, ProductId, Quantity) 
                              VALUES (@BranchId, @ProductId, @baseQuantity) 
                              ON DUPLICATE KEY UPDATE Quantity = Quantity + @baseQuantity";
                    await conn.ExecuteAsync(stockSql, new { model.BranchId, item.ProductId, baseQuantity }, transaction);

                    // ၁၁။ Product Master ရှိ ရင်းဈေးနှင့် ရောင်းဈေးကို Update လုပ်ခြင်း
                    // Always update moving-average cost as BASE-UNIT cost.
                    await conn.ExecuteAsync(
                        @"UPDATE Products SET CostPrice = @CalculatedNewCost WHERE ProductId = @ProductId",
                        new { CalculatedNewCost = calculatedNewCost, item.ProductId },
                        transaction);

                    // Only update sale price if user provided it (>0).
                    // - If purchasing in base unit: update Products.SalePrice (base unit).
                    // - If purchasing in scaling unit: update UnitConversions.BaseSalePrice for that unit.
                    if (item.NewSalePrice > 0)
                    {
                        if (multiplier == 1)
                        {
                            await conn.ExecuteAsync(
                                @"UPDATE Products SET SalePrice = @NewSalePrice WHERE ProductId = @ProductId",
                                new { item.NewSalePrice, item.ProductId },
                                transaction);
                        }
                        else
                        {
                            await conn.ExecuteAsync(
                                @"UPDATE UnitConversions SET BaseSalePrice = @NewSalePrice 
                                  WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                                new { item.NewSalePrice, item.ProductId, item.UnitId },
                                transaction);
                        }
                    }
                }

                if (insertedItemCount == 0)
                    throw new InvalidOperationException("Purchase order was not saved because no item rows were created.");

                await transaction.CommitAsync();
                return Ok(new { Message = $"Purchase Success. Status: {poStatus}. Average Cost updated." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Error: " + ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePurchase(int id, [FromBody] PurchaseEntryModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ၁။ ယခင် Item များ၏ Stock ကို Reverse ပြန်လုပ်ခြင်း
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);

                var oldItems = await conn.QueryAsync<dynamic>(
                    @"SELECT poi.*, uc.Multiplier FROM PurchaseOrderItems poi 
              LEFT JOIN UnitConversions uc ON poi.ProductId = uc.ProductId AND poi.UnitId = uc.FromUnitId
              WHERE poi.POId = @id", new { id }, transaction);

                foreach (var item in oldItems)
                {
                    decimal oldBaseQty = item.Quantity * (item.Multiplier ?? 1);
                    await conn.ExecuteAsync(
                        "UPDATE ProductStocks SET Quantity = Quantity - @oldBaseQty WHERE ProductId = @ProductId AND BranchId = @BranchId",
                        new { oldBaseQty, item.ProductId, model.BranchId }, transaction);
                }

                // ၁.၅။ ယခင် Master Data ကို ရယူခြင်း (Debt Update အတွက်)
                var oldMaster = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT TotalAmount, PaidAmount, SupplierId FROM PurchaseOrders WHERE POId = @id", new { id }, transaction);

                decimal oldDue = (oldMaster?.TotalAmount ?? 0m) - (oldMaster?.PaidAmount ?? 0m);

                // ၂။ PurchaseOrders Master Update
                decimal newTotal = model.TotalAmount;
                decimal newPaid = model.PaidAmount.GetValueOrDefault(0m);
                decimal newDue = newTotal - newPaid;

                string newStatus = newPaid <= 0 ? "Unpaid" : (newPaid < newTotal ? "Partial" : "Paid");

                await conn.ExecuteAsync(
                    @"UPDATE PurchaseOrders 
                      SET SupplierId=@SupplierId, TotalAmount=@TotalAmount, PaidAmount=@PaidAmount, Status=@Status 
                      WHERE POId=@id",
                    new { model.SupplierId, TotalAmount = newTotal, PaidAmount = newPaid, Status = newStatus, id }, transaction);

                // ၂.၅။ Supplier Debt Update
                decimal debtDifference = newDue - oldDue;
                if (debtDifference != 0)
                {
                    await conn.ExecuteAsync(
                        "UPDATE Suppliers SET CurrentDebt = CurrentDebt + @debtDifference WHERE SupplierId = @SupplierId",
                        new { debtDifference, SupplierId = model.SupplierId }, transaction);
                }

                // ၃။ အဟောင်းများဖျက်၍ အသစ်များပြန်သွင်းခြင်း
                await conn.ExecuteAsync("DELETE FROM PurchaseOrderItems WHERE POId = @id", new { id }, transaction);

                foreach (var item in model.Items)
                {
                    var conv = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        "SELECT Multiplier FROM UnitConversions WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                        new { item.ProductId, item.UnitId }, transaction);

                    decimal multiplier = conv?.Multiplier ?? 1;
                    decimal baseUnitCost = multiplier > 0 ? (item.CostPrice / multiplier) : item.CostPrice;
                    decimal baseQuantity = (item.Quantity + (item.FOCQuantity ?? 0m)) * multiplier;

                    // ၄။ PurchaseOrderItems ထဲသို့ Record သွင်းခြင်း
                    await conn.ExecuteAsync(
                        @"INSERT INTO PurchaseOrderItems (POId, ProductId, UnitId, Quantity, FOCQuantity, CostPrice, ExpiryDate, AlertDays, RemainingQuantity) 
                  VALUES (@POId, @ProductId, @UnitId, @Quantity, @FOCQuantity, @CostPrice, @ExpiryDate, @AlertDays, @RemainingQuantity)",
                        new
                        {
                            POId = id,
                            item.ProductId,
                            item.UnitId,
                            item.Quantity,
                            FOCQuantity = item.FOCQuantity ?? 0m,
                            CostPrice = item.CostPrice,
                            item.ExpiryDate,
                            item.AlertDays,
                            RemainingQuantity = baseQuantity
                        },
                        transaction);

                    // ၅။ Stock Update (Base Unit သို့ပြောင်း၍ ပေါင်းထည့်ခြင်း)
                    await conn.ExecuteAsync(
                        "UPDATE ProductStocks SET Quantity = Quantity + @baseQuantity WHERE BranchId = @BranchId AND ProductId = @ProductId",
                        new { model.BranchId, item.ProductId, baseQuantity }, transaction);

                    // ၆။ 🔥 Moving Weighted Average Cost တွက်ချက်ခြင်း & Product Update
                    // (UpdatePurchase မှာ အဟောင်းတွေကို နှုတ်ထားပြီးသားမို့ CreatePurchase အတိုင်း တွက်လို့ရပါတယ်)
                    var currentProductData = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                        SELECT p.CostPrice as OldCost, IFNULL(SUM(ps.Quantity), 0) as CurrentTotalStock 
                        FROM Products p 
                        LEFT JOIN ProductStocks ps ON p.ProductId = ps.ProductId 
                        WHERE p.ProductId = @ProductId
                        GROUP BY p.ProductId, p.CostPrice",
                        new { item.ProductId }, transaction);

                    decimal oldCost = currentProductData?.OldCost ?? 0m;
                    decimal currentStock = currentProductData?.CurrentTotalStock ?? 0m;
                    decimal totalInwardQty = item.Quantity + (item.FOCQuantity ?? 0m);
                    decimal totalInwardBaseQty = totalInwardQty * multiplier;

                    decimal calculatedNewCost;
                    if (currentStock + totalInwardBaseQty > 0)
                    {
                        var purchasedBaseQty = item.Quantity * multiplier;
                        calculatedNewCost = ((currentStock * oldCost) + (purchasedBaseQty * baseUnitCost)) / (currentStock + totalInwardBaseQty);
                    }
                    else
                    {
                        calculatedNewCost = baseUnitCost;
                    }

                    await conn.ExecuteAsync(
                        @"UPDATE Products SET CostPrice = @CalculatedNewCost WHERE ProductId = @ProductId",
                        new { CalculatedNewCost = calculatedNewCost, item.ProductId },
                        transaction);

                    if (item.NewSalePrice > 0)
                    {
                        if (multiplier == 1)
                        {
                            await conn.ExecuteAsync(
                                @"UPDATE Products SET SalePrice = @NewSalePrice WHERE ProductId = @ProductId",
                                new { item.NewSalePrice, item.ProductId },
                                transaction);
                        }
                        else
                        {
                            await conn.ExecuteAsync(
                                @"UPDATE UnitConversions SET BaseSalePrice = @NewSalePrice 
                                  WHERE ProductId = @ProductId AND FromUnitId = @UnitId",
                                new { item.NewSalePrice, item.ProductId, item.UnitId },
                                transaction);
                        }
                    }
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Purchase update successful." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        public class PurchaseHeaderUpdateModel
        {
            public int SupplierId { get; set; }
            public decimal PaidAmount { get; set; }
            public string? PaymentMethod { get; set; }
            public DateTime? PurchaseDate { get; set; }
        }

        [HttpPut("{id:int}/header")]
        public async Task<IActionResult> UpdatePurchaseHeader(int id, [FromBody] PurchaseHeaderUpdateModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                if (model.SupplierId <= 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new { Message = "Supplier is required." });
                }

                var oldMaster = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT POId, SupplierId, TotalAmount, PaidAmount, OrderDate FROM PurchaseOrders WHERE POId = @id",
                    new { id },
                    transaction);

                if (oldMaster == null)
                {
                    await transaction.RollbackAsync();
                    return NotFound(new { Message = "Purchase not found." });
                }

                decimal total = (decimal)oldMaster.TotalAmount;
                decimal oldPaid = (decimal)oldMaster.PaidAmount;
                decimal newPaid = Math.Max(0, Math.Min(model.PaidAmount, total));
                decimal oldDue = Math.Max(0, total - oldPaid);
                decimal newDue = Math.Max(0, total - newPaid);
                int oldSupplierId = (int)oldMaster.SupplierId;
                string status = newPaid <= 0 ? "Unpaid" : (newPaid < total ? "Partial" : "Paid");
                var orderDate = model.PurchaseDate ?? (DateTime)oldMaster.OrderDate;
                string invoiceNo = $"PU{id:D7}";
                string method = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod.Trim();

                await conn.ExecuteAsync(@"
UPDATE PurchaseOrders
SET SupplierId = @SupplierId,
    PaidAmount = @PaidAmount,
    Status = @Status,
    OrderDate = @OrderDate
WHERE POId = @id",
                    new { model.SupplierId, PaidAmount = newPaid, Status = status, OrderDate = orderDate, id },
                    transaction);

                if (oldSupplierId == model.SupplierId)
                {
                    decimal debtDifference = newDue - oldDue;
                    if (debtDifference != 0)
                    {
                        await conn.ExecuteAsync(
                            "UPDATE Suppliers SET CurrentDebt = CurrentDebt + @debtDifference WHERE SupplierId = @SupplierId",
                            new { debtDifference, SupplierId = model.SupplierId },
                            transaction);
                    }
                }
                else
                {
                    if (oldDue > 0)
                    {
                        await conn.ExecuteAsync(
                            "UPDATE Suppliers SET CurrentDebt = CurrentDebt - @oldDue WHERE SupplierId = @SupplierId",
                            new { oldDue, SupplierId = oldSupplierId },
                            transaction);
                    }
                    if (newDue > 0)
                    {
                        await conn.ExecuteAsync(
                            "UPDATE Suppliers SET CurrentDebt = CurrentDebt + @newDue WHERE SupplierId = @SupplierId",
                            new { newDue, SupplierId = model.SupplierId },
                            transaction);
                    }
                }

                await conn.ExecuteAsync("DELETE FROM SupplierPayments WHERE ReferenceNo = @ReferenceNo", new { ReferenceNo = invoiceNo }, transaction);
                if (newPaid > 0)
                {
                    string paymentNote = $"Paid for Purchase {invoiceNo}. (Total: {total:N0}, Paid: {newPaid:N0}, {newDue:N0} added to debt)";
                    await conn.ExecuteAsync(@"
INSERT INTO SupplierPayments (SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate)
VALUES (@SupplierId, @Amount, @PaymentMethod, @ReferenceNo, @Note, @PaymentDate)",
                        new
                        {
                            SupplierId = model.SupplierId,
                            Amount = newPaid,
                            PaymentMethod = method,
                            ReferenceNo = invoiceNo,
                            Note = paymentNote,
                            PaymentDate = orderDate
                        },
                        transaction);
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Purchase updated successfully." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPurchases()
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // PurchaseOrders နဲ့ Suppliers table ကို JOIN တွဲပြီး 
            // SupplierName ပါ တစ်ခါတည်း ဆွဲထုတ်သည့် SQL
            string sql = @"
        SELECT 
            po.POId, 
            po.SupplierId, 
            po.BranchId, 
            po.TotalAmount, 
            po.PaidAmount, 
            po.Status, 
            po.OrderDate,
            s.SupplierName
        FROM PurchaseOrders po
        LEFT JOIN Suppliers s ON po.SupplierId = s.SupplierId
        ORDER BY po.OrderDate DESC LIMIT 20";

            try
            {
                var purchases = await conn.QueryAsync<dynamic>(sql);
                return Ok(purchases);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "စာရင်းဆွဲထုတ်ရာတွင် အမှားအယွင်းရှိပါသည် - " + ex.Message });
            }
        }

        [HttpGet("details/{id:int}")]
        public async Task<IActionResult> GetPurchaseDetails(int id)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // ၁။ Master Data (ဘယ်သူ့ဆီက ဝယ်တာလဲ)
            string masterSql = @"SELECT
                            po.*,
                            s.SupplierName,
                            pm.PaymentMethod
                        FROM PurchaseOrders po
                        LEFT JOIN Suppliers s ON po.SupplierId = s.SupplierId
                        LEFT JOIN (
                            SELECT
                                ReferenceNo,
                                GROUP_CONCAT(DISTINCT COALESCE(NULLIF(PaymentMethod, ''), 'Unknown') ORDER BY PaymentMethod SEPARATOR ', ') AS PaymentMethod
                            FROM SupplierPayments
                            GROUP BY ReferenceNo
                        ) pm ON pm.ReferenceNo = CONCAT('PU', LPAD(po.POId, 7, '0'))
                        WHERE po.POId = @id";
            var master = await conn.QueryFirstOrDefaultAsync<dynamic>(masterSql, new { id });

            if (master == null) return NotFound();

            // ၂။ Item Details (ဘာတွေဝယ်ခဲ့တာလဲ)
            string itemsSql = @"SELECT poi.*, COALESCE(p.Name, CONCAT('Product #', poi.ProductId)) as ProductName, u.UnitName 
                        FROM PurchaseOrderItems poi
                        LEFT JOIN Products p ON poi.ProductId = p.ProductId
                        LEFT JOIN Units u ON poi.UnitId = u.UnitId
                        WHERE poi.POId = @id";
            var items = await conn.QueryAsync<dynamic>(itemsSql, new { id });

            return Ok(new { Master = master, Items = items });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePurchase(int id, [FromQuery] int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ၀။ Master Data ရယူခြင်း (Debt Update အတွက်)
                var master = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT TotalAmount, PaidAmount, SupplierId FROM PurchaseOrders WHERE POId = @id", new { id }, transaction);

                if (master != null)
                {
                    decimal due = (master.TotalAmount ?? 0m) - (master.PaidAmount ?? 0m);
                    if (due > 0)
                    {
                        await conn.ExecuteAsync(
                            "UPDATE Suppliers SET CurrentDebt = CurrentDebt - @due WHERE SupplierId = @SupplierId",
                            new { due, SupplierId = (int)master.SupplierId }, transaction);
                    }
                }

                // ၁။ Stock များကို Base Unit တွက်ချက်၍ ပြန်နှုတ်ခြင်း
                var items = await conn.QueryAsync<dynamic>(
                    @"SELECT poi.*, uc.Multiplier FROM PurchaseOrderItems poi 
              LEFT JOIN UnitConversions uc ON poi.ProductId = uc.ProductId 
              WHERE poi.POId = @id", new { id }, transaction);

                foreach (var item in items)
                {
                    decimal baseQty = item.Quantity * (item.Multiplier ?? 1);
                    await conn.ExecuteAsync(
                        "UPDATE ProductStocks SET Quantity = Quantity - @baseQty WHERE ProductId = @ProductId AND BranchId = @branchId",
                        new { baseQty, item.ProductId, branchId }, transaction);
                }

                // ၂။ Master နှင့် Detail Data များကို ဖျက်ခြင်း
                await conn.ExecuteAsync("DELETE FROM PurchaseOrderItems WHERE POId = @id", new { id }, transaction);
                await conn.ExecuteAsync("DELETE FROM PurchaseOrders WHERE POId = @id", new { id }, transaction);

                await transaction.CommitAsync();
                return Ok(new { Message = "Purchase deleted and stock adjusted correctly." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetPurchaseHistory([FromQuery] int branchId, [FromQuery] string role)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // Admin ဆိုရင် branchId filter ကို ကျော်ပြီး အကုန်ပြမယ်
            // Staff ဆိုရင်တော့ ပို့လိုက်တဲ့ branchId နဲ့ပဲ filter လုပ်မယ်
            string sql = @"
                SELECT 
                    po.*, 
                    s.SupplierName,
                    b.BranchName,
                    pm.PaymentMethod
                FROM PurchaseOrders po
                LEFT JOIN Suppliers s ON po.SupplierId = s.SupplierId
                LEFT JOIN Branches b ON po.BranchId = b.BranchId
                LEFT JOIN (
                    SELECT ReferenceNo, GROUP_CONCAT(DISTINCT COALESCE(NULLIF(PaymentMethod, ''), 'Unknown') ORDER BY PaymentMethod SEPARATOR ', ') AS PaymentMethod
                    FROM SupplierPayments
                    GROUP BY ReferenceNo
                ) pm ON pm.ReferenceNo = CONCAT('PU', LPAD(po.POId, 7, '0'))
                WHERE (@role = 'Admin' OR po.BranchId = @branchId)
                ORDER BY po.OrderDate DESC";

            try
            {
                var purchases = await conn.QueryAsync<dynamic>(sql, new { branchId, role });
                return Ok(purchases);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "History ဆွဲထုတ်ရာတွင် အမှားအယွင်းရှိပါသည် - " + ex.Message });
            }
        }
    }
}
