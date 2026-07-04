using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReturnsController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public ReturnsController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // ၁။ Sales Return (Customer ဆီမှ ပစ္စည်းပြန်ဝင်လာခြင်း)
        [HttpPost("sales-return")]
        public async Task<IActionResult> ProcessSalesReturn([FromBody] ReturnEntryModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // SaleItems တွင် အရေအတွက် လျှော့ချခြင်း
                string updateSaleItem = "UPDATE SaleItems SET Quantity = Quantity - @ReturnQty WHERE SaleId = @SaleId AND ProductId = @ProductId";
                await conn.ExecuteAsync(updateSaleItem, new { model.ReturnQty, model.SaleId, model.ProductId }, transaction);

                // Multiplier ရှာဖွေပြီး စတော့ပြန်ပေါင်းခြင်း
                decimal multiplier = await GetMultiplier(conn, model.ProductId, model.UnitId, transaction);
                decimal baseQtyToReturn = model.ReturnQty * multiplier;

                string stockUpdate = "UPDATE ProductStocks SET Quantity = Quantity + @baseQtyToReturn WHERE BranchId = @BranchId AND ProductId = @ProductId";
                await conn.ExecuteAsync(stockUpdate, new { baseQtyToReturn, model.BranchId, model.ProductId }, transaction);

                // Customer အကြွေးမှ ပြန်နှုတ်ပေးခြင်း
                if (model.CustomerId > 0)
                {
                    decimal refundAmount = model.ReturnQty * model.UnitPrice;
                    string custUpdate = "UPDATE Customers SET CurrentBalance = CurrentBalance - @refundAmount WHERE CustomerId = @CustomerId";
                    await conn.ExecuteAsync(custUpdate, new { refundAmount, model.CustomerId }, transaction);
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Sales return processed successfully." });
            }
            catch (Exception ex) { await transaction.RollbackAsync(); return BadRequest(new { Message = ex.Message }); }
        }

        // ၂။ Purchase Return (Supplier ဆီသို့ ပစ္စည်းပြန်ပို့ခြင်း)
        [HttpPost("purchase-return")]
        public async Task<IActionResult> ProcessPurchaseReturn([FromBody] ReturnEntryModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // Multiplier ရှာဖွေပြီး စတော့မှ နှုတ်ပယ်ခြင်း
                decimal multiplier = await GetMultiplier(conn, model.ProductId, model.UnitId, transaction);
                decimal baseQtyToDeduct = model.ReturnQty * multiplier;

                string stockUpdate = "UPDATE ProductStocks SET Quantity = Quantity - @baseQtyToDeduct WHERE BranchId = @BranchId AND ProductId = @ProductId";
                await conn.ExecuteAsync(stockUpdate, new { baseQtyToDeduct, model.BranchId, model.ProductId }, transaction);

                // Supplier ဆီမှ အကြွေးပြန်နှုတ်ခြင်း
                if (model.SupplierId > 0)
                {
                    decimal creditNoteAmount = model.ReturnQty * model.UnitCost;
                    string supplierUpdate = "UPDATE Suppliers SET CurrentDebt = CurrentDebt - @creditNoteAmount WHERE SupplierId = @SupplierId";
                    await conn.ExecuteAsync(supplierUpdate, new { creditNoteAmount, model.SupplierId }, transaction);
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Purchase return processed successfully." });
            }
            catch (Exception ex) { await transaction.RollbackAsync(); return BadRequest(new { Message = ex.Message }); }
        }

        // Private helper to get Multiplier
        private async Task<decimal> GetMultiplier(MySqlConnection conn, int productId, int unitId, MySqlTransaction trans)
        {
            var res = await conn.QueryFirstOrDefaultAsync<decimal?>(
                "SELECT Multiplier FROM UnitConversions WHERE ProductId = @productId AND FromUnitId = @unitId",
                new { productId, unitId }, trans);
            return res ?? 1;
        }
    }
}