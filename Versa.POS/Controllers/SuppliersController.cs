using Dapper;
using System.Security.Claims;
using Versa.POS.Services;
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

        var usageCount = await conn.ExecuteScalarAsync<int>(@"
SELECT SUM(RefCount)
FROM (
    SELECT COUNT(*) AS RefCount FROM PurchaseOrders WHERE SupplierId = @id
    UNION ALL SELECT COUNT(*) FROM SupplierPayments WHERE SupplierId = @id
    UNION ALL SELECT COUNT(*) FROM Consignments WHERE SupplierId = @id
) refs", new { id });

        if (usageCount > 0)
        {
            return BadRequest(new { Message = "This supplier has purchase, payment, or consignment history. It cannot be deleted." });
        }

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

    [Authorize(Roles = "Admin,Manager")]
    [HttpPost("pay-debt")]
    public async Task<IActionResult> PayDebt([FromBody] SupplierPaymentRequest model)
    {
        try
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            var result = await SupplierDebtPayments.PayAsync(conn, model,
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown");
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("{id}/payments/{paymentId}/reverse")]
    public async Task<IActionResult> ReversePayment(int id, int paymentId,
        [FromBody] SupplierDebtPayments.ReversalRequest model)
    {
        try
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            return Ok(await SupplierDebtPayments.ReverseAsync(conn, id, paymentId, model.Reason ?? "",
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown"));
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("{id}/payments")]
    public async Task<IActionResult> GetSupplierPayments(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        await conn.OpenAsync();
        await SupplierDebtPayments.EnsureSchemaAsync(conn);

        // ရက်စွဲအလိုက် နောက်ဆုံးပေးထားတာတွေကို အပေါ်မှာပြရန်
        string sql = @"
            SELECT
                sp.*,
                op.ReversalPaymentId, op.ReversedBy, op.ReversedAt, op.ReversalReason,
                reverseOp.PaymentId AS ReversesPaymentId,
                CASE WHEN op.PaymentId IS NOT NULL AND op.ReversalPaymentId IS NULL
                     AND NOT EXISTS (SELECT 1 FROM SupplierPayments newer
                                     WHERE newer.SupplierId = sp.SupplierId AND newer.PaymentId > sp.PaymentId)
                     THEN 1 ELSE 0 END AS CanRequestReversal,
                CASE
                    WHEN reverseOp.PaymentId IS NOT NULL THEN 'Payment Reversal'
                    WHEN op.ReversalPaymentId IS NOT NULL THEN 'Reversed Payment'
                    WHEN COALESCE(sp.Note, '') = 'Debt payment' THEN 'Debt Payment'
                    WHEN sp.ReferenceNo LIKE 'PU%' THEN 'Purchase Payment'
                    ELSE 'Debt Payment'
                END AS PaymentType
            FROM SupplierPayments sp
            LEFT JOIN SupplierDebtPaymentOperations op ON op.PaymentId = sp.PaymentId
            LEFT JOIN SupplierDebtPaymentOperations reverseOp ON reverseOp.ReversalPaymentId = sp.PaymentId
            WHERE sp.SupplierId = @id
            ORDER BY sp.PaymentDate DESC, sp.PaymentId DESC";

        var payments = await conn.QueryAsync<dynamic>(sql, new { id });
        return Ok(payments);
    }
}
