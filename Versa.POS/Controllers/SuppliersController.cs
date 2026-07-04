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
public class SuppliersController : BaseShopController
{
    private readonly DatabaseService _dbService;
    public SuppliersController(DatabaseService dbService) => _dbService = dbService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var list = await conn.QueryAsync<Supplier>("SELECT * FROM Suppliers");
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Supplier model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = @"INSERT INTO Suppliers (SupplierName, ContactPerson, Phone, Address) 
                       VALUES (@SupplierName, @ContactPerson, @Phone, @Address); 
                       SELECT LAST_INSERT_ID();";
        model.SupplierId = await conn.ExecuteScalarAsync<int>(sql, model);
        return Ok(model);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        string sql = "DELETE FROM Suppliers WHERE SupplierId = @id";

        try
        {
            int rowsAffected = await conn.ExecuteAsync(sql, new { id });

            if (rowsAffected > 0)
            {
                return Ok(new { Message = "Supplier ကို အောင်မြင်စွာ ဖျက်ပြီးပါပြီ။" });
            }
            return BadRequest(new { Message = "ဖျက်ရန် Supplier ID ရှာမတွေ့ပါဗျာ။" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = "ဤ Supplier အား ဖျက်၍မရပါ။ ၎င်းနှင့်ပတ်သက်သော အဝယ်မှတ်တမ်းများ ရှိနေနိုင်ပါသည်။" });
        }
    }

    [HttpPut("{id}")] // URL ကနေ ID ကို လက်ခံရန်
    public async Task<IActionResult> Update(int id, [FromBody] Supplier model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // ၁။ URL ကလာတဲ့ 'id' ကို Model ရဲ့ 'SupplierId' ထဲသို့ အတင်းထည့်ပေးပါ
        model.SupplierId = id;

        // ၂။ Payload ထဲမှာ ပါလာတဲ့ Column တွေကိုပဲ Update လုပ်ပါမယ်
        // Database Table ရှိ Column အမည်များမှာ SupplierName, ContactPerson, Phone ဖြစ်ရပါမည်
        string sql = @"UPDATE Suppliers 
                   SET SupplierName = @SupplierName, 
                       ContactPerson = @ContactPerson, 
                       Phone = @Phone 
                   WHERE SupplierId = @SupplierId";

        try
        {
            int rowsAffected = await conn.ExecuteAsync(sql, model);

            if (rowsAffected > 0)
            {
                return Ok(model);
            }
            return BadRequest(new { Message = "ပြင်ဆင်ရန် ID ရှာမတွေ့ပါ သို့မဟုတ် ဒေတာအပြောင်းအလဲမရှိပါဗျာ။" });
        }
        catch (Exception ex)
        {
            // Error ဖြစ်ပါက အကြောင်းရင်းကို သိနိုင်ရန် Message ပြန်ပို့ပေးပါမည်
            return BadRequest(new { Message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("pay-debt")]
    public async Task<IActionResult> PayDebt([FromBody] SupplierPaymentRequest model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        using var transaction = await conn.BeginTransactionAsync();

        try
        {
            // ၁။ ငွေပေးချေမှုမှတ်တမ်းကို အရင်သွင်းပါ
            string insertPaymentSql = @"INSERT INTO SupplierPayments (SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate) 
                                   VALUES (@SupplierId, @Amount, @PaymentMethod, @ReferenceNo, @Note, @PaymentDate)";
            await conn.ExecuteAsync(insertPaymentSql, model, transaction);

            // ၂။ Supplier ထံမှ အကြွေးမကျေသေးသော ဘောက်ချာများကို အဟောင်းဆုံးကနေ စယူပါ
            // (PaidAmount < TotalAmount) ဖြစ်နေသော ဘောက်ချာများကို စစ်ထုတ်ပါသည်
            string selectUnpaidSql = @"SELECT POId, TotalAmount, PaidAmount 
                                   FROM PurchaseOrders 
                                   WHERE SupplierId = @SupplierId AND PaidAmount < TotalAmount 
                                   ORDER BY OrderDate ASC";
            var unpaidOrders = (await conn.QueryAsync(selectUnpaidSql, new { model.SupplierId }, transaction)).ToList();

            decimal remainingPayAmount = model.Amount;

            // ၃။ FIFO Logic ဖြင့် ဘောက်ချာများသို့ ငွေခွဲဝေခြင်း
            foreach (var po in unpaidOrders)
            {
                if (remainingPayAmount <= 0) break;

                decimal poTotal = po.TotalAmount;
                decimal poCurrentPaid = po.PaidAmount;
                decimal stillOwed = poTotal - poCurrentPaid; // ဒီဘောက်ချာအတွက် ကျန်သေးသောပမာဏ

                decimal amountToApply = Math.Min(remainingPayAmount, stillOwed);

                decimal newPaidAmount = poCurrentPaid + amountToApply;
                remainingPayAmount -= amountToApply;

                // Status သတ်မှတ်ခြင်း (အကုန်ကျေရင် Paid၊ တစ်ဝက်ကျေရင် Partial)
                string newStatus = newPaidAmount >= poTotal ? "Paid" : "Partial";

                // PurchaseOrders Table ကို Update လုပ်ခြင်း
                string updatePOSql = "UPDATE PurchaseOrders SET PaidAmount = @PaidAmount, Status = @Status WHERE POId = @POId";
                await conn.ExecuteAsync(updatePOSql, new { PaidAmount = newPaidAmount, Status = newStatus, POId = po.POId }, transaction);
            }

            // ၄။ Suppliers Table ရှိ စုစုပေါင်းအကြွေး (CurrentDebt) ကို လျှော့ချခြင်း
            string updateDebtSql = "UPDATE Suppliers SET CurrentDebt = CurrentDebt - @Amount WHERE SupplierId = @SupplierId";
            await conn.ExecuteAsync(updateDebtSql, new { model.Amount, model.SupplierId }, transaction);

            await transaction.CommitAsync();
            return Ok(new { message = "Debt payment recorded and allocated to Purchase Orders successfully!" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new { message = "Error: " + ex.Message });
        }
    }

    [HttpGet("{id}/payments")]
    public async Task<IActionResult> GetSupplierPayments(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // ရက်စွဲအလိုက် နောက်ဆုံးပေးထားတာတွေကို အပေါ်မှာပြရန်
        string sql = @"SELECT * FROM SupplierPayments 
                   WHERE SupplierId = @id 
                   ORDER BY PaymentDate DESC";

        var payments = await conn.QueryAsync<dynamic>(sql, new { id });
        return Ok(payments);
    }
}