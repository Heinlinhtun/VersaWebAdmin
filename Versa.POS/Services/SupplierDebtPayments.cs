using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using MySqlConnector;
using Versa.Core.Models;

namespace Versa.POS.Services;

// A tenant-local journal. Legacy payments deliberately have no reversible journal entry.
public static class SupplierDebtPayments
{
    public const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS SupplierDebtPaymentOperations (
            RequestId CHAR(36) NOT NULL PRIMARY KEY,
            SupplierId INT NOT NULL,
            PaymentId INT NOT NULL,
            PayloadHash CHAR(64) NOT NULL,
            Amount DECIMAL(18,2) NOT NULL,
            DebtAfter DECIMAL(18,2) NOT NULL,
            StateAfter CHAR(64) NOT NULL,
            Allocations LONGTEXT NOT NULL,
            CreatedBy VARCHAR(100) NOT NULL,
            CreatedAt DATETIME(6) NOT NULL,
            ReversalPaymentId INT NULL,
            ReversedBy VARCHAR(100) NULL,
            ReversedAt DATETIME(6) NULL,
            ReversalReason VARCHAR(500) NULL,
            UNIQUE KEY UQ_SupplierDebtPayment (PaymentId),
            UNIQUE KEY UQ_SupplierDebtReversal (ReversalPaymentId),
            KEY IX_SupplierDebtOperations (SupplierId)
        ) ENGINE=InnoDB
        """;

    // DDL must run before BeginTransaction: MySQL DDL implicitly commits transactions.
    public static async Task EnsureSchemaAsync(MySqlConnection conn)
    {
        var exists = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SupplierDebtPaymentOperations'
            """);
        if (exists == 0) await conn.ExecuteAsync(SchemaSql);
    }

    public sealed class OrderState
    {
        public int POId { get; set; }
        public decimal? TotalAmount { get; set; }
        public decimal? PaidAmount { get; set; }
        public string? Status { get; set; }
        public DateTime OrderDate { get; set; }
    }
    public sealed class Allocation
    {
        public int POId { get; set; }
        public decimal BeforePaid { get; set; }
        public string? BeforeStatus { get; set; }
        public decimal Applied { get; set; }
    }
    public sealed class Operation
    {
        public Guid RequestId { get; set; }
        public int SupplierId { get; set; }
        public int PaymentId { get; set; }
        public string PayloadHash { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal DebtAfter { get; set; }
        public string StateAfter { get; set; } = "";
        public string Allocations { get; set; } = "";
        public int? ReversalPaymentId { get; set; }
    }
    public sealed record Result(int PaymentId, int? ReversalPaymentId, bool Replayed);
    public sealed class ReversalRequest { public string Reason { get; set; } = ""; }

    private static string Hash(object value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));

    public static string PayloadHash(SupplierPaymentRequest model) => Hash(new
    {
        model.SupplierId, Amount = model.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
        PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod.Trim(),
        ReferenceNo = model.ReferenceNo?.Trim() ?? "",
        Note = string.IsNullOrWhiteSpace(model.Note) ? "Debt payment" : model.Note.Trim(),
        PaymentDate = model.PaymentDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
    });

    public static void Validate(SupplierPaymentRequest model)
    {
        if (!Guid.TryParse(model.RequestId, out var id) || id == Guid.Empty)
            throw new ArgumentException("A payment request ID is required. Refresh the application before paying.");
        if (model.SupplierId <= 0 || model.Amount <= 0 || model.Amount > 9999999999999999.99m
            || decimal.Round(model.Amount, 2) != model.Amount)
            throw new ArgumentException("Enter a positive payment amount with at most two decimal places.");
        if (model.PaymentDate == default || (model.PaymentMethod?.Trim().Length ?? 0) > 50
            || (model.ReferenceNo?.Trim().Length ?? 0) > 100 || (model.Note?.Length ?? 0) > 2000)
            throw new ArgumentException("Payment date or payment details are invalid.");
        // PU references are owned by purchase editing, which deletes/recreates its payment rows.
        if ((model.ReferenceNo?.Trim() ?? "").StartsWith("PU", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("References starting with PU are reserved for purchase payments.");
    }

    private static async Task<decimal> LockSupplier(MySqlConnection conn, MySqlTransaction tx, int supplierId)
    {
        var debt = await conn.QuerySingleOrDefaultAsync<decimal?>(
            "SELECT CurrentDebt FROM Suppliers WHERE SupplierId = @supplierId FOR UPDATE", new { supplierId }, tx);
        return debt ?? throw new InvalidOperationException("Supplier not found or its balance is invalid.");
    }

    private static async Task<List<OrderState>> LockOrders(MySqlConnection conn, MySqlTransaction tx, int supplierId) =>
        (await conn.QueryAsync<OrderState>("""
            SELECT POId, TotalAmount, PaidAmount, Status, OrderDate FROM PurchaseOrders
            WHERE SupplierId = @supplierId ORDER BY OrderDate, POId FOR UPDATE
            """, new { supplierId }, tx)).ToList();

    private static async Task<string> StateHash(MySqlConnection conn, MySqlTransaction tx,
        int supplierId, List<OrderState> orders)
    {
        // Returns change allocation meaning even when the headline balance happens to match.
        var returns = (await conn.QueryAsync("""
            SELECT r.ReturnId, r.POId, r.Quantity, r.RefundAmount FROM PurchaseReturns r
            JOIN PurchaseOrders p ON p.POId = r.POId
            WHERE p.SupplierId = @supplierId ORDER BY r.ReturnId FOR UPDATE
            """, new { supplierId }, tx)).ToList();
        return Hash(new { Orders = orders, Returns = returns });
    }

    public static async Task<Result> PayAsync(MySqlConnection conn, SupplierPaymentRequest model, string actor)
    {
        Validate(model);
        await EnsureSchemaAsync(conn);
        await using var tx = await conn.BeginTransactionAsync();
        var debt = await LockSupplier(conn, tx, model.SupplierId);
        var requestId = Guid.Parse(model.RequestId!).ToString();
        var hash = PayloadHash(model);
        // Check replay BEFORE debt validation: a successful original may have settled the debt.
        var previous = await conn.QuerySingleOrDefaultAsync<Operation>(
            "SELECT * FROM SupplierDebtPaymentOperations WHERE RequestId = @requestId FOR UPDATE", new { requestId }, tx);
        if (previous != null)
        {
            if (previous.SupplierId != model.SupplierId || previous.PayloadHash != hash)
                throw new InvalidOperationException("This request ID was already used with different payment details.");
            await tx.CommitAsync();
            return new(previous.PaymentId, previous.ReversalPaymentId, true);
        }
        if (model.Amount > debt)
            throw new InvalidOperationException("Payment exceeds the current supplier debt. Refresh the balance.");

        var orders = await LockOrders(conn, tx, model.SupplierId);
        var allocations = Allocate(orders, model.Amount);
        var paymentId = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO SupplierPayments (SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate)
            VALUES (@SupplierId, @Amount, @PaymentMethod, @ReferenceNo, @Note, @PaymentDate);
            SELECT LAST_INSERT_ID();
            """, new
        {
            model.SupplierId, model.Amount,
            PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod.Trim(),
            ReferenceNo = model.ReferenceNo?.Trim() ?? "",
            Note = string.IsNullOrWhiteSpace(model.Note) ? "Debt payment" : model.Note.Trim(),
            PaymentDate = model.PaymentDate.Date
        }, tx);
        foreach (var a in allocations)
        {
            var order = orders.Single(o => o.POId == a.POId);
            await conn.ExecuteAsync("UPDATE PurchaseOrders SET PaidAmount = @PaidAmount, Status = @Status WHERE POId = @POId",
                order, tx);
        }
        await conn.ExecuteAsync("UPDATE Suppliers SET CurrentDebt = CurrentDebt - @Amount WHERE SupplierId = @SupplierId",
            new { model.Amount, model.SupplierId }, tx);
        await conn.ExecuteAsync("""
            INSERT INTO SupplierDebtPaymentOperations
            (RequestId, SupplierId, PaymentId, PayloadHash, Amount, DebtAfter, StateAfter, Allocations, CreatedBy, CreatedAt)
            VALUES (@requestId, @SupplierId, @paymentId, @hash, @Amount, @debtAfter, @stateAfter, @allocationsJson, @actor, UTC_TIMESTAMP(6))
            """, new
        {
            requestId, model.SupplierId, paymentId, hash, model.Amount, debtAfter = debt - model.Amount,
            stateAfter = await StateHash(conn, tx, model.SupplierId, orders),
            allocationsJson = JsonSerializer.Serialize(allocations), actor
        }, tx);
        await tx.CommitAsync();
        return new(paymentId, null, false);
    }

    public static List<Allocation> Allocate(List<OrderState> orders, decimal amount)
    {
        var remaining = amount;
        var allocations = new List<Allocation>();
        foreach (var order in orders)
        {
            if (order.TotalAmount == null || order.PaidAmount == null || order.PaidAmount < 0)
                throw new InvalidOperationException("Purchase balances need reconciliation before payment.");
            if (remaining <= 0 || order.PaidAmount >= order.TotalAmount) continue;
            var applied = Math.Min(remaining, order.TotalAmount.Value - order.PaidAmount.Value);
            allocations.Add(new() { POId = order.POId, BeforePaid = order.PaidAmount.Value,
                BeforeStatus = order.Status, Applied = applied });
            order.PaidAmount += applied;
            order.Status = order.PaidAmount >= order.TotalAmount ? "Paid" : "Partial";
            remaining -= applied;
        }
        // Unallocated remainder can settle an opening balance; supplier balance validation still applies.
        return allocations;
    }

    public static async Task<Result> ReverseAsync(MySqlConnection conn, int supplierId, int paymentId, string reason, string actor)
    {
        reason = reason.Trim();
        if (reason.Length < 5 || reason.Length > 500)
            throw new ArgumentException("Enter a reversal reason between 5 and 500 characters.");
        await EnsureSchemaAsync(conn);
        await using var tx = await conn.BeginTransactionAsync();
        var debt = await LockSupplier(conn, tx, supplierId);
        var op = await conn.QuerySingleOrDefaultAsync<Operation>("""
            SELECT * FROM SupplierDebtPaymentOperations
            WHERE SupplierId = @supplierId AND PaymentId = @paymentId FOR UPDATE
            """, new { supplierId, paymentId }, tx);
        if (op == null)
            throw new InvalidOperationException("This payment has no allocation audit. Automatic reversal is blocked; use a reviewed reconciliation.");
        if (op.ReversalPaymentId != null)
        {
            await tx.CommitAsync();
            return new(paymentId, op.ReversalPaymentId, true);
        }
        var payment = await conn.QuerySingleOrDefaultAsync<SupplierPaymentRequest>("""
            SELECT SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate FROM SupplierPayments
            WHERE PaymentId = @paymentId AND SupplierId = @supplierId FOR UPDATE
            """, new { paymentId, supplierId }, tx);
        if (payment == null || PayloadHash(payment) != op.PayloadHash)
            throw new InvalidOperationException("The original payment has changed. Reconciliation is required.");
        var orders = await LockOrders(conn, tx, supplierId);
        var laterPayment = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM SupplierPayments WHERE SupplierId = @supplierId AND PaymentId > @paymentId
            """, new { supplierId, paymentId }, tx);
        if (laterPayment != 0 || debt != op.DebtAfter || await StateHash(conn, tx, supplierId, orders) != op.StateAfter)
            throw new InvalidOperationException("Later payments, purchases, returns or balance changes prevent a safe automatic reversal. Reconciliation is required.");
        var allocations = JsonSerializer.Deserialize<List<Allocation>>(op.Allocations)
            ?? throw new InvalidOperationException("Allocation audit is missing.");
        foreach (var a in allocations)
            await conn.ExecuteAsync("UPDATE PurchaseOrders SET PaidAmount = @BeforePaid, Status = @BeforeStatus WHERE POId = @POId AND SupplierId = @supplierId",
                new { a.BeforePaid, a.BeforeStatus, a.POId, supplierId }, tx);
        // Signed compensating entry keeps existing SUM(Amount) consumers correct and preserves the original.
        var reversalId = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO SupplierPayments (SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate)
            VALUES (@supplierId, @amount, @PaymentMethod, @reference, @note, CURRENT_TIMESTAMP);
            SELECT LAST_INSERT_ID();
            """, new { supplierId, amount = -op.Amount, payment.PaymentMethod,
                reference = $"REV-{paymentId}", note = $"Reversal of payment #{paymentId}: {reason}" }, tx);
        await conn.ExecuteAsync("UPDATE Suppliers SET CurrentDebt = CurrentDebt + @Amount WHERE SupplierId = @supplierId",
            new { op.Amount, supplierId }, tx);
        await conn.ExecuteAsync("""
            UPDATE SupplierDebtPaymentOperations SET ReversalPaymentId = @reversalId,
                ReversedBy = @actor, ReversedAt = UTC_TIMESTAMP(6), ReversalReason = @reason
            WHERE PaymentId = @paymentId
            """, new { reversalId, actor, reason, paymentId }, tx);
        await tx.CommitAsync();
        return new(paymentId, reversalId, false);
    }
}
