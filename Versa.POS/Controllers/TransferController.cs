using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TransferController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public TransferController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTransfer([FromBody] StockTransferModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // ၁။ StockTransfers (Master) ကို အရင်သိမ်းသည်
                string transferSql = @"INSERT INTO StockTransfers (FromBranchId, ToBranchId, UserId, TransferDate, Status, Note) 
                                 VALUES (@FromBranchId, @ToBranchId, @UserId, NOW(), 'Completed', @Note);
                                 SELECT LAST_INSERT_ID();";
                int transferId = await conn.ExecuteScalarAsync<int>(transferSql, model, transaction);

                foreach (var item in model.Items)
                {
                    // ၂။ Unit Multiplier ရှာဖွေခြင်း
                    var res = await conn.QueryFirstOrDefaultAsync<decimal?>(
                        "SELECT Multiplier FROM UnitConversions WHERE ProductId = @productId AND FromUnitId = @unitId",
                        new { item.ProductId, item.UnitId }, transaction);
                    decimal multiplier = res ?? 1;
                    decimal baseQty = item.Quantity * multiplier;

                    // ၃။ StockTransferItems (Details) သိမ်းခြင်း
                    string itemSql = "INSERT INTO StockTransferItems (TransferId, ProductId, Quantity) VALUES (@transferId, @ProductId, @baseQty)";
                    await conn.ExecuteAsync(itemSql, new { transferId, item.ProductId, baseQty }, transaction);

                    // ၄။ FromBranch မှ စတော့နှုတ်ခြင်း
                    string deductSql = "UPDATE ProductStocks SET Quantity = Quantity - @baseQty WHERE BranchId = @FromBranchId AND ProductId = @ProductId";
                    await conn.ExecuteAsync(deductSql, new { baseQty, model.FromBranchId, item.ProductId }, transaction);

                    // ၅။ ToBranch ထဲသို့ စတော့ပေါင်းထည့်ခြင်း (UPSERT Logic)
                    string addSql = @"INSERT INTO ProductStocks (BranchId, ProductId, Quantity) 
                                VALUES (@ToBranchId, @ProductId, @baseQty) 
                                ON DUPLICATE KEY UPDATE Quantity = Quantity + @baseQty";
                    await conn.ExecuteAsync(addSql, new { model.ToBranchId, item.ProductId, baseQty }, transaction);
                }

                await transaction.CommitAsync();
                return Ok(new { Message = "Stock transfer completed successfully." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetTransferHistory()
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // Branches Table နှင့် Join ၍ ဆိုင်ခွဲအမည်များ ဆွဲထုတ်ခြင်း
            string sql = @"
        SELECT 
            t.TransferId, 
            b1.BranchName as FromBranchName, 
            b2.BranchName as ToBranchName, 
            t.TransferDate, t.Status, t.Note
        FROM StockTransfers t
        JOIN Branches b1 ON t.FromBranchId = b1.BranchId
        JOIN Branches b2 ON t.ToBranchId = b2.BranchId
        ORDER BY t.TransferDate DESC";

            var history = await conn.QueryAsync<TransferHistoryViewModel>(sql);
            return Ok(history);
        }
    }
}
