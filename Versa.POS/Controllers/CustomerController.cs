using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using Versa.Core.Models;
using Versa.Core.Services;
using Versa.POS.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CustomersController : BaseShopController
{
    private readonly DatabaseService _dbService;
    private readonly ILogger<CustomersController>? _logger;
    public CustomersController(DatabaseService dbService, ILogger<CustomersController>? logger = null)
    {
        _dbService = dbService;
        _logger = logger;
    }

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

    // Tenant databases previously migrated for Previous Credit; verify before reusing after a restore.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _openingBalanceReadyDbs = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.SemaphoreSlim> _openingBalanceMigrationLocks = new();

    // Previous Credit = customer credit that existed before the shop started using the system.
    // Must be called OUTSIDE a transaction because ALTER TABLE causes an implicit commit in MySQL.
    private static async Task EnsureCustomerOpeningBalanceColumnsAsync(MySqlConnection conn)
    {
        // Keep the advisory lock on one physical connection, and resolve the tenant
        // database before consulting the process-wide migration cache.
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
        var dbKey = conn.Database ?? string.Empty;
        if (_openingBalanceReadyDbs.ContainsKey(dbKey))
        {
            // A restored backup can replace a migrated table without restarting
            // this API. Never trust the cached flag over the actual tenant schema.
            var ready = await conn.ExecuteScalarAsync<int>(@"
SELECT
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Customers'
       AND COLUMN_NAME IN ('OpeningBalance', 'OpeningBalancePaid'))
    + (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
       WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'CustomerOpeningBalanceAdjustments')");
            if (ready == 3) return;
            _openingBalanceReadyDbs.TryRemove(dbKey, out _);
        }

        var migrationLock = _openingBalanceMigrationLocks.GetOrAdd(dbKey, _ => new System.Threading.SemaphoreSlim(1, 1));
        await migrationLock.WaitAsync();
        var databaseLockName = $"versa-opening-balance:{dbKey}";
        var holdsDatabaseLock = false;
        try
        {
            // The in-process lock handles normal traffic; the advisory lock also protects multi-instance deployments.
            holdsDatabaseLock = await conn.ExecuteScalarAsync<int>("SELECT GET_LOCK(@databaseLockName, 30)", new { databaseLockName }) == 1;
            if (!holdsDatabaseLock)
                throw new InvalidOperationException("Could not acquire the customer previous-credit migration lock.");

            // Always inspect after taking the lock, including after a restore.

            await CustomerSchema.EnsureOpeningBalanceAsync(conn);

            _openingBalanceReadyDbs[dbKey] = true;
        }
        finally
        {
            try
            {
                if (holdsDatabaseLock)
                    await conn.ExecuteAsync("SELECT RELEASE_LOCK(@databaseLockName)", new { databaseLockName });
            }
            finally
            {
                migrationLock.Release();
            }
        }
    }

    private int? AuthenticatedUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
    private bool CanManageOpeningBalance => User.IsInRole("Admin") || User.IsInRole("Manager");

    private const string PreviousCreditNote = "Previous credit payment";

    private sealed class OpeningBalanceRow
    {
        public decimal OpeningBalance { get; set; }
        public decimal OpeningBalancePaid { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureCustomerOpeningBalanceColumnsAsync(conn);
        string sql = @"
            SELECT
                c.CustomerId,
                c.Name,
                c.Phone,
                c.Address,
                IFNULL(c.OpeningBalance, 0) AS OpeningBalance,
                IFNULL(c.OpeningBalancePaid, 0) AS OpeningBalancePaid,
                GREATEST(IFNULL(c.OpeningBalance, 0) - IFNULL(c.OpeningBalancePaid, 0), 0) AS OpeningRemaining,
                GREATEST(IFNULL(cb.CurrentBalance, 0), 0) AS SalesBalance,
                GREATEST(IFNULL(cb.CurrentBalance, 0), 0)
                    + GREATEST(IFNULL(c.OpeningBalance, 0) - IFNULL(c.OpeningBalancePaid, 0), 0) AS CurrentBalance,
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
        try
        {
            return await CreateCustomerAsync(model);
        }
        catch (MySqlException ex)
        {
            _logger?.LogError(ex, "Customer creation failed for tenant {TenantDb}; database error {ErrorNumber}", TenantDbName, ex.Number);
            var message = ex.Number switch
            {
                1044 or 1045 or 1142 or 1143 => "The server database account does not have permission to prepare or save customer records. Please ask your administrator to check customer-table permissions.",
                1054 => "The customer database is missing a required field. Please ask your administrator to update the customer database schema.",
                1406 => "Customer details are too long. Use at most 100 characters for the name and 20 for the phone number.",
                _ => $"Could not save the customer because of a server database error (code {ex.Number}). Please contact your administrator."
            };
            return StatusCode(ex.Number == 1406 ? 400 : 503, new { Message = message });
        }
        catch (InvalidOperationException ex)
        {
            _logger?.LogError(ex, "Customer setup failed for tenant {TenantDb}", TenantDbName);
            return StatusCode(503, new { Message = "Customer database setup could not complete. Please try again; if the error continues, ask your administrator to check the server logs." });
        }
    }

    private async Task<IActionResult> CreateCustomerAsync(Customer model)
    {
        var openingBalance = model.OpeningBalance ?? 0m;
        if (openingBalance < 0)
            return BadRequest(new { Message = "Previous credit cannot be negative." });
        if (openingBalance > 0 && !CanManageOpeningBalance)
            return Forbid();

        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureCustomerOpeningBalanceColumnsAsync(conn);

        // SQL ထဲက Column အမည်များကို Model ထဲရှိ Name, Phone, Address တို့နှင့် အတိအကျညှိပေးရပါမည်
        // CurrentBalance က အသစ်ဆောက်ချိန်မှာ ပုံမှန်အားဖြင့် 0 ဖြစ်ပါလိမ့်မည်
        // Previous Credit ကို stored CurrentBalance ထဲလည်း ပေါင်းထည့်ထားမှ voucher ပေါ်က balance မှန်ပါမည်
        string sql = @"INSERT INTO Customers (Name, Phone, Address, CurrentBalance, OpeningBalance, OpeningBalancePaid, CreatedAt)
                   VALUES (@Name, @Phone, @Address, @StoredBalance, @OpeningBalance, 0, @CreatedAt);
                   SELECT LAST_INSERT_ID();";

        var storedBalance = Math.Max(0, model.CurrentBalance) + openingBalance;

        // Dapper က model.Name ကို @Name နေရာမှာ၊ model.Phone ကို @Phone နေရာမှာ အလိုအလျောက် အစားထိုးပေးပါလိမ့်မည်
        model.CustomerId = await conn.ExecuteScalarAsync<int>(sql, new
        {
            model.Name,
            model.Phone,
            model.Address,
            StoredBalance = storedBalance,
            OpeningBalance = openingBalance,
            model.CreatedAt
        });

        if (openingBalance > 0)
        {
            await conn.ExecuteAsync(@"
INSERT INTO CustomerOpeningBalanceAdjustments
    (CustomerId, PreviousOpeningBalance, NewOpeningBalance, AmountChange, Reason, ChangedByUserId, ChangedAt)
VALUES
    (@CustomerId, 0, @OpeningBalance, @OpeningBalance, 'Initial previous credit', @ChangedByUserId, NOW(6))",
                new { model.CustomerId, OpeningBalance = openingBalance, ChangedByUserId = AuthenticatedUserId });
        }

        model.OpeningBalance = openingBalance;
        model.OpeningRemaining = openingBalance;
        model.CurrentBalance = storedBalance;
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

        await EnsureCustomerOpeningBalanceColumnsAsync(conn);
        var openingRemaining = await conn.ExecuteScalarAsync<decimal>(
            "SELECT IFNULL(GREATEST(IFNULL(OpeningBalance, 0) - IFNULL(OpeningBalancePaid, 0), 0), 0) FROM Customers WHERE CustomerId = @id",
            new { id });

        if (openingRemaining > 0)
        {
            return BadRequest(new { Message = "This customer still has unpaid previous credit. It cannot be deleted." });
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

        // Older clients (Customers.vue etc.) don't send OpeningBalance -> keep the old behaviour.
        if (!model.OpeningBalance.HasValue)
        {
            await conn.ExecuteAsync(sql, model);
            return Ok(model);
        }

        var newOpening = model.OpeningBalance.Value;
        if (newOpening < 0)
            return BadRequest(new { Message = "Previous credit cannot be negative." });

        await EnsureCustomerOpeningBalanceColumnsAsync(conn);
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();
        try
        {
            var current = await conn.QuerySingleOrDefaultAsync<OpeningBalanceRow>(@"
                SELECT IFNULL(OpeningBalance, 0) AS OpeningBalance, IFNULL(OpeningBalancePaid, 0) AS OpeningBalancePaid
                FROM Customers WHERE CustomerId = @CustomerId FOR UPDATE",
                new { model.CustomerId }, trans);

            if (current == null)
            {
                await trans.RollbackAsync();
                return NotFound(new { Message = "Customer not found." });
            }

            var oldOpening = current.OpeningBalance;
            var openingPaid = current.OpeningBalancePaid;
            if (newOpening < openingPaid)
            {
                await trans.RollbackAsync();
                return BadRequest(new { Message = $"Previous credit cannot be less than the amount already paid ({openingPaid:N0})." });
            }

            await conn.ExecuteAsync(sql, model, trans);

            var delta = newOpening - oldOpening;
            if (delta != 0 && !CanManageOpeningBalance)
            {
                await trans.RollbackAsync();
                return Forbid();
            }
            if (delta != 0 && string.IsNullOrWhiteSpace(model.OpeningBalanceAdjustmentReason))
            {
                await trans.RollbackAsync();
                return BadRequest(new { Message = "A reason is required when changing previous credit." });
            }

            await conn.ExecuteAsync(@"
                UPDATE Customers
                SET OpeningBalance = @NewOpening,
                    CurrentBalance = GREATEST(IFNULL(CurrentBalance, 0) + @Delta, 0)
                WHERE CustomerId = @CustomerId",
                new { NewOpening = newOpening, Delta = delta, model.CustomerId }, trans);

            if (delta != 0)
            {
                await conn.ExecuteAsync(@"
INSERT INTO CustomerOpeningBalanceAdjustments
    (CustomerId, PreviousOpeningBalance, NewOpeningBalance, AmountChange, Reason, ChangedByUserId, ChangedAt)
VALUES
    (@CustomerId, @PreviousOpeningBalance, @NewOpeningBalance, @AmountChange, @Reason, @ChangedByUserId, NOW(6))",
                    new
                    {
                        model.CustomerId,
                        PreviousOpeningBalance = oldOpening,
                        NewOpeningBalance = newOpening,
                        AmountChange = delta,
                        Reason = model.OpeningBalanceAdjustmentReason!.Trim(),
                        ChangedByUserId = AuthenticatedUserId
                    }, trans);
            }

            await trans.CommitAsync();

            model.OpeningBalancePaid = openingPaid;
            model.OpeningRemaining = Math.Max(0, newOpening - openingPaid);
            return Ok(model);
        }
        catch (Exception ex)
        {
            await trans.RollbackAsync();
            return BadRequest(new { Message = "Error: " + ex.Message });
        }
    }

    [HttpPost("pay-debt")]
    public async Task<IActionResult> PayDebt([FromBody] CustomerPaymentRequest model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureCustomerOpeningBalanceColumnsAsync(conn); // ALTER TABLE must run before the transaction
        await conn.OpenAsync();
        using var trans = await conn.BeginTransactionAsync();

        try
        {
            if (model.Amount <= 0)
                return BadRequest(new { Message = "Payment amount must be greater than zero." });

            var lockedCustomer = await conn.QuerySingleOrDefaultAsync<OpeningBalanceRow>(@"
                SELECT IFNULL(OpeningBalance, 0) AS OpeningBalance, IFNULL(OpeningBalancePaid, 0) AS OpeningBalancePaid
                FROM Customers WHERE CustomerId = @CustomerId FOR UPDATE",
                new { model.CustomerId }, trans);

            if (lockedCustomer == null)
                return BadRequest(new { Message = "Customer not found." });

            decimal openingRemaining = Math.Max(0, lockedCustomer.OpeningBalance - lockedCustomer.OpeningBalancePaid);

            decimal salesDebt = await conn.ExecuteScalarAsync<decimal>(@"
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

            decimal currentDebt = salesDebt + openingRemaining;

            if (currentDebt <= 0)
                return BadRequest(new { Message = "This customer has no outstanding debt." });

            if (model.Amount > currentDebt)
                return BadRequest(new { Message = "Payment amount cannot be greater than current customer debt." });

            decimal remainingPayment = model.Amount;
            DateTime paymentDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;

            // ၁။ Previous Credit (system မသုံးခင်က အကြွေး) ကို အရင်ဆုံး ချေပါမည် — အဟောင်းဆုံး အကြွေးဖြစ်သောကြောင့်
            if (openingRemaining > 0)
            {
                decimal openingApplied = Math.Min(remainingPayment, openingRemaining);

                await conn.ExecuteAsync(@"
                    UPDATE Customers
                    SET OpeningBalancePaid = IFNULL(OpeningBalancePaid, 0) + @AppliedAmount
                    WHERE CustomerId = @CustomerId",
                    new { AppliedAmount = openingApplied, model.CustomerId }, trans);

                await conn.ExecuteAsync(@"
                    INSERT INTO CustomerPayments
                        (CustomerId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate, SaleId)
                    VALUES
                        (@CustomerId, @Amount, @PaymentMethod, @ReferenceNo, @Note, @PaymentDate, NULL)",
                    new
                    {
                        model.CustomerId,
                        Amount = openingApplied,
                        model.PaymentMethod,
                        model.ReferenceNo,
                        Note = string.IsNullOrWhiteSpace(model.Note) ? PreviousCreditNote : $"{PreviousCreditNote} - {model.Note.Trim()}",
                        PaymentDate = paymentDate
                    }, trans);

                remainingPayment -= openingApplied;
            }

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
                        Note = string.IsNullOrWhiteSpace(model.Note) ? "Debt payment" : model.Note.Trim(),
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
                    WHEN cp.SaleId IS NULL AND cp.Note LIKE 'Previous credit payment%' THEN 'Previous Credit Payment'
                    WHEN cp.SaleId IS NULL THEN 'Debt Payment'
                    WHEN cp.PaymentDate > COALESCE(s.SaleDate, cp.PaymentDate) THEN 'Debt Payment'
                    WHEN COALESCE(cp.Note, '') <> '' THEN 'Debt Payment'
                    ELSE 'Sale Payment'
                END AS PaymentType,
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

    [HttpGet("{id}/opening-balance-adjustments")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetOpeningBalanceAdjustments(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureCustomerOpeningBalanceColumnsAsync(conn);
        var list = await conn.QueryAsync(@"
SELECT a.AdjustmentId, a.PreviousOpeningBalance, a.NewOpeningBalance, a.AmountChange,
       a.Reason, a.ChangedAt, u.Username AS ChangedBy
FROM CustomerOpeningBalanceAdjustments a
LEFT JOIN ShopUsers u ON u.UserId = a.ChangedByUserId
WHERE a.CustomerId = @id
ORDER BY a.ChangedAt DESC, a.AdjustmentId DESC", new { id });
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
