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
public class CustomersController : BaseShopController
{
    private readonly DatabaseService _dbService;
    public CustomersController(DatabaseService dbService) => _dbService = dbService;

    private static async Task EnsureSalesOrderStatusColumnAsync(MySqlConnection conn)
    {
        var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'Sales'
  AND COLUMN_NAME = 'OrderStatus'");

        if (exists == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE Sales ADD COLUMN OrderStatus VARCHAR(20) NOT NULL DEFAULT 'Completed'");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = @"
            SELECT
                c.CustomerId,
                c.Name,
                c.Phone,
                c.Address,
                GREATEST(IFNULL(cb.CurrentBalance, 0), 0) AS CurrentBalance,
                c.CreatedAt
            FROM Customers c
            LEFT JOIN (
                SELECT
                    s.CustomerId,
                    SUM(
                        GREATEST(
                            0,
                            IFNULL(sit.ItemSubtotal, 0)
                            - IFNULL(s.DiscountAmount, 0)
                            + IFNULL(s.DeliveryFee, 0)
                            - IFNULL(s.PaidAmount, 0)
                        )
                    ) AS CurrentBalance
                FROM Sales s
                LEFT JOIN (
                    SELECT
                        SaleId,
                        SUM(
                            CASE
                                WHEN IFNULL(IsFOC, 0) = 1 THEN 0
                                WHEN IFNULL(SubTotal, 0) > ((IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                                    THEN GREATEST(0, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                                ELSE GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)))
                            END
                        ) AS ItemSubtotal
                    FROM SaleItems
                    GROUP BY SaleId
                ) sit ON sit.SaleId = s.SaleId
                WHERE s.CustomerId IS NOT NULL
                GROUP BY s.CustomerId
            ) cb ON cb.CustomerId = c.CustomerId
            ORDER BY c.CustomerId";
        var list = await conn.QueryAsync<Customer>(sql);
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Customer model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // SQL ထဲက Column အမည်များကို Model ထဲရှိ Name, Phone, Address တို့နှင့် အတိအကျညှိပေးရပါမည်
        // CurrentBalance က အသစ်ဆောက်ချိန်မှာ ပုံမှန်အားဖြင့် 0 ဖြစ်ပါလိမ့်မည်
        string sql = @"INSERT INTO Customers (Name, Phone, Address, CurrentBalance, CreatedAt) 
                   VALUES (@Name, @Phone, @Address, @CurrentBalance, @CreatedAt); 
                   SELECT LAST_INSERT_ID();";

        // Dapper က model.Name ကို @Name နေရာမှာ၊ model.Phone ကို @Phone နေရာမှာ အလိုအလျောက် အစားထိုးပေးပါလိမ့်မည်
        model.CustomerId = await conn.ExecuteScalarAsync<int>(sql, model);

        return Ok(model);
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        var usageCount = await conn.ExecuteScalarAsync<int>(@"
SELECT SUM(RefCount)
FROM (
    SELECT COUNT(*) AS RefCount FROM Sales WHERE CustomerId = @id
    UNION ALL SELECT COUNT(*) FROM CustomerPayments WHERE CustomerId = @id
) refs", new { id });

        if (usageCount > 0)
        {
            return BadRequest(new { Message = "This customer has sale or payment history. It cannot be deleted." });
        }

        // ID အလိုက် ဖျက်ရန် SQL
        string sql = "DELETE FROM Customers WHERE CustomerId = @id";

        try
        {
            // ExecuteAsync ကိုသုံးပြီး row ဘယ်နှစ်ကြောင်း ထိခိုက်သွားလဲ စစ်ပါမည်
            int rowsAffected = await conn.ExecuteAsync(sql, new { id });

            if (rowsAffected > 0)
            {
                return Ok(new { Message = "Customer ကို အောင်မြင်စွာ ဖျက်ပြီးပါပြီ။" });
            }
            return BadRequest(new { Message = "ဖျက်ရန် Customer ID ရှာမတွေ့ပါဗျာ။" });
        }
        catch (Exception ex)
        {
            // အကယ်၍ တခြား Table တွေမှာ ဒီ Customer နဲ့ ပတ်သက်တဲ့ Transaction တွေရှိနေရင် error တက်ပါလိမ့်မယ်
            return BadRequest(new { Message = "ဤ Customer အား ဖျက်၍မရပါ။ ၎င်းနှင့်ပတ်သက်သော အရောင်းမှတ်တမ်းများ ရှိနေနိုင်ပါသည်။" });
        }
    }

    [HttpPut("{id}")] // URL မှာ ID လက်ခံရန် ထည့်လိုက်ပါ
    public async Task<IActionResult> Update(int id, [FromBody] Customer model)
    {
        // URL က ID ကို Model ထဲ ပြန်ထည့်ပေးပါ
        model.CustomerId = id;

        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = @"UPDATE Customers 
                   SET Name = @Name, Phone = @Phone, Address = @Address 
                   WHERE CustomerId = @CustomerId";

        await conn.ExecuteAsync(sql, model);
        return Ok(model);
    }

    [HttpPost("pay-debt")]
    public async Task<IActionResult> PayDebt([FromBody] CustomerPaymentRequest model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();

        try
        {
            if (model.Amount <= 0)
                return BadRequest(new { Message = "Payment amount must be greater than zero." });

            var lockedCustomerId = await conn.ExecuteScalarAsync<int?>(
                "SELECT CustomerId FROM Customers WHERE CustomerId = @CustomerId FOR UPDATE",
                new { model.CustomerId }, trans);

            if (!lockedCustomerId.HasValue)
                return BadRequest(new { Message = "Customer not found." });

            decimal currentDebt = await conn.ExecuteScalarAsync<decimal>(@"
                SELECT IFNULL(SUM(
                    GREATEST(
                        0,
                        IFNULL(sit.ItemSubtotal, 0)
                        - IFNULL(s.DiscountAmount, 0)
                        + IFNULL(s.DeliveryFee, 0)
                        - IFNULL(s.PaidAmount, 0)
                    )
                ), 0)
                FROM Sales s
                LEFT JOIN (
                    SELECT
                        SaleId,
                        SUM(
                            CASE
                                WHEN IFNULL(IsFOC, 0) = 1 THEN 0
                                WHEN IFNULL(SubTotal, 0) > ((IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                                    THEN GREATEST(0, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                                ELSE GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)))
                            END
                        ) AS ItemSubtotal
                    FROM SaleItems
                    GROUP BY SaleId
                ) sit ON sit.SaleId = s.SaleId
                WHERE s.CustomerId = @CustomerId",
                new { model.CustomerId }, trans);

            if (currentDebt <= 0)
                return BadRequest(new { Message = "This customer has no outstanding debt." });

            if (model.Amount > currentDebt)
                return BadRequest(new { Message = "Payment amount cannot be greater than current customer debt." });

            decimal remainingPayment = model.Amount;
            DateTime paymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;

            var unpaidSales = await conn.QueryAsync<dynamic>(@"
                SELECT
                    s.SaleId,
                    GREATEST(
                        0,
                        IFNULL(sit.ItemSubtotal, 0)
                        - IFNULL(s.DiscountAmount, 0)
                        + IFNULL(s.DeliveryFee, 0)
                        - IFNULL(s.PaidAmount, 0)
                    ) AS DueAmount
                FROM Sales s
                LEFT JOIN (
                    SELECT
                        SaleId,
                        SUM(
                            CASE
                                WHEN IFNULL(IsFOC, 0) = 1 THEN 0
                                WHEN IFNULL(SubTotal, 0) > ((IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                                    THEN GREATEST(0, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                                ELSE GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)))
                            END
                        ) AS ItemSubtotal
                    FROM SaleItems
                    GROUP BY SaleId
                ) sit ON sit.SaleId = s.SaleId
                WHERE s.CustomerId = @CustomerId
                  AND GREATEST(
                        0,
                        IFNULL(sit.ItemSubtotal, 0)
                        - IFNULL(s.DiscountAmount, 0)
                        + IFNULL(s.DeliveryFee, 0)
                        - IFNULL(s.PaidAmount, 0)
                  ) > 0
                ORDER BY s.SaleDate ASC, s.SaleId ASC
                FOR UPDATE",
                new { model.CustomerId }, trans);

            foreach (var sale in unpaidSales)
            {
                if (remainingPayment <= 0) break;

                int saleId = Convert.ToInt32(sale.SaleId);
                decimal dueAmount = Convert.ToDecimal(sale.DueAmount);
                decimal appliedAmount = Math.Min(remainingPayment, dueAmount);

                await conn.ExecuteAsync(@"
                    UPDATE Sales
                    SET PaidAmount = PaidAmount + @AppliedAmount,
                        DueAmount = GREATEST(DueAmount - @AppliedAmount, 0)
                    WHERE SaleId = @SaleId",
                    new { AppliedAmount = appliedAmount, SaleId = saleId }, trans);

                await conn.ExecuteAsync(@"
                    INSERT INTO CustomerPayments
                        (CustomerId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate, SaleId)
                    VALUES
                        (@CustomerId, @Amount, @PaymentMethod, @ReferenceNo, @Note, @PaymentDate, @SaleId)",
                    new
                    {
                        model.CustomerId,
                        Amount = appliedAmount,
                        model.PaymentMethod,
                        model.ReferenceNo,
                        model.Note,
                        PaymentDate = paymentDate,
                        SaleId = saleId
                    }, trans);

                remainingPayment -= appliedAmount;
            }

            if (remainingPayment > 0)
                throw new InvalidOperationException("Payment could not be fully allocated to customer credit sales.");

            // ၂။ Customers Table ထဲမှ Balance ကို နှုတ်ခြင်း
            string updateSql = @"UPDATE Customers SET CurrentBalance = GREATEST(IFNULL(CurrentBalance, 0) - @Amount, 0) 
                           WHERE CustomerId = @CustomerId";

            await conn.ExecuteAsync(updateSql, new { model.Amount, model.CustomerId }, trans);

            await trans.CommitAsync();
            return Ok(new { Message = "Payment recorded and balance updated successfully." });
        }
        catch (Exception ex)
        {
            await trans.RollbackAsync();
            return BadRequest(new { Message = "Error: " + ex.Message });
        }
    }

    [HttpGet("{id}/payments")]
    public async Task<IActionResult> GetPaymentHistory(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        // PaymentDate အလိုက် အသစ်ဆုံးကို အပေါ်ဆုံးကပြပါမည်
        string sql = @"
            SELECT
                cp.*,
                s.SaleDate,
                s.SaleType,
                s.DueAmount,
                CASE
                    WHEN cp.SaleId IS NULL THEN NULL
                    ELSE CONCAT('SA', LPAD(cp.SaleId, 7, '0'))
                END AS SaleNo
            FROM CustomerPayments cp
            LEFT JOIN Sales s ON cp.SaleId = s.SaleId
            WHERE cp.CustomerId = @id
            ORDER BY cp.PaymentDate DESC, cp.PaymentId DESC";
        var list = await conn.QueryAsync<dynamic>(sql, new { id });
        return Ok(list);
    }

    [HttpGet("{id}/debt-sources")]
    public async Task<IActionResult> GetDebtSources(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // Customer အကြွေးတက်လာတဲ့ Credit Sale voucher list
        string sql = @"
            SELECT
                s.SaleId,
                CONCAT('SA', LPAD(s.SaleId, 7, '0')) AS SaleNo,
                s.SaleDate,
                GREATEST(
                    0,
                    IFNULL(sit.ItemSubtotal, 0)
                    - IFNULL(s.DiscountAmount, 0)
                    + IFNULL(s.DeliveryFee, 0)
                ) AS TotalAmount,
                s.PaidAmount,
                GREATEST(
                    0,
                    IFNULL(sit.ItemSubtotal, 0)
                    - IFNULL(s.DiscountAmount, 0)
                    + IFNULL(s.DeliveryFee, 0)
                    - IFNULL(s.PaidAmount, 0)
                ) AS DueAmount,
                s.SaleType,
                s.PaymentMethod
            FROM Sales s
            LEFT JOIN (
                SELECT
                    SaleId,
                    SUM(
                        CASE
                            WHEN IFNULL(IsFOC, 0) = 1 THEN 0
                            WHEN IFNULL(SubTotal, 0) > ((IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                                THEN GREATEST(0, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                            ELSE GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)))
                        END
                    ) AS ItemSubtotal
                FROM SaleItems
                GROUP BY SaleId
            ) sit ON sit.SaleId = s.SaleId
            WHERE s.CustomerId = @id
              AND GREATEST(
                    0,
                    IFNULL(sit.ItemSubtotal, 0)
                    - IFNULL(s.DiscountAmount, 0)
                    + IFNULL(s.DeliveryFee, 0)
                    - IFNULL(s.PaidAmount, 0)
              ) > 0
            ORDER BY s.SaleDate ASC, s.SaleId ASC";

        var list = await conn.QueryAsync<dynamic>(sql, new { id });
        return Ok(list);
    }

    [HttpGet("{id}/orders")]
    public async Task<IActionResult> GetOrders(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureSalesOrderStatusColumnAsync(conn);

        string sql = @"
            SELECT
                s.SaleId,
                CONCAT('SA', LPAD(s.SaleId, 7, '0')) AS SaleNo,
                s.SaleDate,
                GREATEST(
                    0,
                    IFNULL(sit.ItemSubtotal, 0)
                    - IFNULL(s.DiscountAmount, 0)
                    + IFNULL(s.DeliveryFee, 0)
                ) AS TotalAmount,
                s.PaidAmount,
                GREATEST(
                    0,
                    IFNULL(sit.ItemSubtotal, 0)
                    - IFNULL(s.DiscountAmount, 0)
                    + IFNULL(s.DeliveryFee, 0)
                    - IFNULL(s.PaidAmount, 0)
                ) AS DueAmount,
                s.SaleType,
                s.PaymentMethod,
                IFNULL(s.OrderStatus,'Completed') AS OrderStatus
            FROM Sales s
            LEFT JOIN (
                SELECT
                    SaleId,
                    SUM(
                        CASE
                            WHEN IFNULL(IsFOC, 0) = 1 THEN 0
                            WHEN IFNULL(SubTotal, 0) > ((IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                                THEN GREATEST(0, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0))
                            ELSE GREATEST(0, IFNULL(SubTotal, (IFNULL(Quantity, 0) * IFNULL(UnitPrice, 0)) - IFNULL(DiscountAmount, 0)))
                        END
                    ) AS ItemSubtotal
                FROM SaleItems
                GROUP BY SaleId
            ) sit ON sit.SaleId = s.SaleId
            WHERE s.CustomerId = @id
            ORDER BY s.SaleDate DESC, s.SaleId DESC";

        var list = await conn.QueryAsync<dynamic>(sql, new { id });
        return Ok(list);
    }
}
