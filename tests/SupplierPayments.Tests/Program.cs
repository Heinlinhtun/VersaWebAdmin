using System.Reflection;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;
using Versa.POS.Services;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}
static SupplierPaymentRequest Request(int supplier, decimal amount, string? id = null) => new()
{
    SupplierId = supplier, Amount = amount, RequestId = id ?? Guid.NewGuid().ToString(),
    PaymentMethod = "Cash", PaymentDate = new DateTime(2026, 9, 28), ReferenceNo = "", Note = "Debt payment"
};
static async Task Reject(Func<Task> action, string description)
{
    try { await action(); }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
    { Console.WriteLine($"PASS: {description}"); return; }
    throw new Exception($"Expected rejection: {description}");
}

var roles = typeof(SuppliersController).GetMethod("ReversePayment")!
    .GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles);
Check(roles.Contains("Admin"), "Reversal endpoint requires Admin authorization");
var a = Request(1, 100m);
var b = Request(1, 100.00m, a.RequestId);
b.PaymentDate = DateTime.SpecifyKind(b.PaymentDate, DateTimeKind.Utc);
Check(SupplierDebtPayments.PayloadHash(a) == SupplierDebtPayments.PayloadHash(b),
    "Idempotency hash is stable across database decimal scale and timestamp kind");
foreach (var amount in new[] { 0m, -1m, 1.001m })
    await Reject(() => { SupplierDebtPayments.Validate(Request(1, amount)); return Task.CompletedTask; }, $"Invalid amount {amount} rejected");
await Reject(() => { var m = Request(1, 10); m.RequestId = null; SupplierDebtPayments.Validate(m); return Task.CompletedTask; }, "Old clients without a request ID fail closed");
await Reject(() => { var m = Request(1, 10); m.ReferenceNo = "PU00001"; SupplierDebtPayments.Validate(m); return Task.CompletedTask; }, "Purchase-owned reference prefix cannot be used for debt payments");

var connectionString = Environment.GetEnvironmentVariable("VERSA_PAYMENT_TEST_CONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("SKIP: database tests. Set VERSA_PAYMENT_TEST_CONNECTION to an isolated localhost server.");
    return;
}
var builder = new MySqlConnectionStringBuilder(connectionString);
if (builder.Server is not ("127.0.0.1" or "localhost") || !string.IsNullOrEmpty(builder.Database))
    throw new Exception("Tests require a localhost server and no existing Database in the connection string.");
var db = "versa_payment_test_" + Guid.NewGuid().ToString("N");
await using var admin = new MySqlConnection(builder.ConnectionString);
await admin.OpenAsync();
await admin.ExecuteAsync($"CREATE DATABASE `{db}`");
builder.Database = db;
async Task<MySqlConnection> Open()
{
    var c = new MySqlConnection(builder.ConnectionString);
    await c.OpenAsync();
    return c;
}
async Task<SupplierDebtPayments.Result> Pay(SupplierPaymentRequest m)
{
    await using var c = await Open();
    return await SupplierDebtPayments.PayAsync(c, m, "admin-test");
}
async Task<SupplierDebtPayments.Result> Reverse(int supplier, int payment)
{
    await using var c = await Open();
    return await SupplierDebtPayments.ReverseAsync(c, supplier, payment, "Duplicate entry confirmed", "admin-test");
}
try
{
    await using var conn = await Open();
    await conn.ExecuteAsync("""
        CREATE TABLE Suppliers (SupplierId INT PRIMARY KEY, CurrentDebt DECIMAL(18,2)) ENGINE=InnoDB;
        CREATE TABLE PurchaseOrders (POId INT PRIMARY KEY, SupplierId INT, TotalAmount DECIMAL(18,2),
          PaidAmount DECIMAL(18,2), Status VARCHAR(20), OrderDate DATETIME, KEY(SupplierId)) ENGINE=InnoDB;
        CREATE TABLE SupplierPayments (PaymentId INT AUTO_INCREMENT PRIMARY KEY, SupplierId INT,
          Amount DECIMAL(18,2), PaymentMethod VARCHAR(50), ReferenceNo VARCHAR(100), Note TEXT,
          PaymentDate TIMESTAMP, KEY(SupplierId)) ENGINE=InnoDB;
        CREATE TABLE PurchaseReturns (ReturnId INT AUTO_INCREMENT PRIMARY KEY, POId INT,
          Quantity DECIMAL(18,2), RefundAmount DECIMAL(18,2), KEY(POId)) ENGINE=InnoDB;
        INSERT INTO Suppliers VALUES (1,1000),(2,1000),(3,1000),(4,1000),(5,1000),(6,1000),(7,1000),(8,1000);
        INSERT INTO PurchaseOrders VALUES (1,1,600,100,'Partial','2026-09-01'), (2,1,500,0,'Unpaid','2026-09-02');
        """);
    await SupplierDebtPayments.EnsureSchemaAsync(conn);
    var request = Request(1, 700);
    var posted = await Pay(request);
    var replay = await Pay(Request(1, 700.00m, request.RequestId));
    Check(posted.PaymentId == replay.PaymentId && replay.Replayed, "Lost-response retry returns the original payment");
    Check(await conn.ExecuteScalarAsync<decimal>("SELECT CurrentDebt FROM Suppliers WHERE SupplierId=1") == 300,
        "Replay does not subtract debt twice");
    Check(await conn.ExecuteScalarAsync<decimal>("SELECT PaidAmount FROM PurchaseOrders WHERE POId=2") == 200,
        "FIFO splits payment across purchase orders");
    await Reject(() => Pay(Request(1, 701, request.RequestId)), "Reused request ID with different amount rejected");
    await Reject(() => Pay(Request(1, 301)), "Overpayment rejected against locked current debt");
    await Reject(() => Reverse(2, posted.PaymentId), "Cross-supplier reversal rejected");
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:MasterDbConnection"] = builder.ConnectionString }).Build();
    var controller = new SuppliersController(new DatabaseService(config))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
        { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("TenantDb", db) }, "test")) } }
    };
    var history = (OkObjectResult)await controller.GetSupplierPayments(1);
    var historyRows = ((IEnumerable<dynamic>)history.Value!).ToList();
    Check(historyRows.Count == 1 && Convert.ToInt32(historyRows[0].CanRequestReversal) == 1,
        "History endpoint exposes tracked latest payment for reversal");
    var reversed = await Reverse(1, posted.PaymentId);
    var repeated = await Reverse(1, posted.PaymentId);
    Check(reversed.ReversalPaymentId == repeated.ReversalPaymentId && repeated.Replayed, "Repeated reversal does not restore debt twice");
    Check(await conn.ExecuteScalarAsync<decimal>("SELECT CurrentDebt FROM Suppliers WHERE SupplierId=1") == 1000,
        "Reversal restores supplier debt");
    Check(await conn.ExecuteScalarAsync<decimal>("SELECT SUM(PaidAmount) FROM PurchaseOrders WHERE SupplierId=1") == 100,
        "Reversal restores exact original allocations");
    Check(await conn.ExecuteScalarAsync<string>("SELECT Status FROM PurchaseOrders WHERE POId=2") == "Unpaid",
        "Reversal restores original invoice status");
    Check(await conn.ExecuteScalarAsync<decimal>("SELECT SUM(Amount) FROM SupplierPayments WHERE SupplierId=1") == 0,
        "Original and signed reversal net to zero in payment totals");
    Check(await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SupplierPayments WHERE SupplierId=1") == 2,
        "Original payment is preserved alongside its reversal");
    Check((await Pay(request)).ReversalPaymentId == reversed.ReversalPaymentId,
        "Retrying a reversed payment never creates another payment");
    history = (OkObjectResult)await controller.GetSupplierPayments(1);
    historyRows = ((IEnumerable<dynamic>)history.Value!).ToList();
    Check(historyRows.Count == 2 && historyRows.All(p => Convert.ToInt32(p.CanRequestReversal) == 0)
        && historyRows.Any(p => (string)p.PaymentType == "Reversed Payment")
        && historyRows.Any(p => (string)p.PaymentType == "Payment Reversal"),
        "History labels both original reversal audit and compensating entry");

    var concurrent = Request(2, 600);
    var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Pay(concurrent)));
    Check(results.Select(x => x.PaymentId).Distinct().Count() == 1, "Eight simultaneous retries create one payment");
    var contenders = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
    {
        try { await Pay(Request(3, 700)); return true; }
        catch (InvalidOperationException) { return false; }
    }));
    Check(contenders.Count(x => x) == 1, "Different concurrent requests cannot overpay the same debt");
    var concurrentReversals = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Reverse(2, results[0].PaymentId)));
    Check(concurrentReversals.Select(x => x.ReversalPaymentId).Distinct().Count() == 1,
        "Concurrent reversal retries create one compensating entry");

    await conn.ExecuteAsync("INSERT INTO SupplierPayments (SupplierId,Amount,PaymentMethod,PaymentDate) VALUES (4,10,'Cash',NOW())");
    var legacy = await conn.ExecuteScalarAsync<int>("SELECT MAX(PaymentId) FROM SupplierPayments WHERE SupplierId=4");
    await Reject(() => Reverse(4, legacy), "Legacy payment without allocations cannot be automatically reversed");
    var earlier = await Pay(Request(5, 100));
    await Pay(Request(5, 100));
    await Reject(() => Reverse(5, earlier.PaymentId), "Later payment blocks reversal");
    var changed = await Pay(Request(6, 100));
    await conn.ExecuteAsync("INSERT INTO PurchaseOrders VALUES (6,6,100,100,'Paid','2026-09-28')");
    await Reject(() => Reverse(6, changed.PaymentId), "Later purchase blocks reversal even with unchanged supplier debt");
    await conn.ExecuteAsync("INSERT INTO PurchaseOrders VALUES (7,7,1000,0,'Unpaid','2026-09-28')");
    var returned = await Pay(Request(7, 100));
    await conn.ExecuteAsync("INSERT INTO PurchaseReturns (POId,Quantity,RefundAmount) VALUES (7,1,20)");
    await Reject(() => Reverse(7, returned.PaymentId), "Later return blocks reversal");

    // Force failure after payment/allocation writes; the entire transaction must roll back.
    await conn.ExecuteAsync("""
        CREATE TRIGGER reject_operation BEFORE INSERT ON SupplierDebtPaymentOperations
        FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Injected journal failure'
        """);
    try { await Pay(Request(8, 100)); throw new Exception("Expected injected failure"); }
    catch (MySqlException) { }
    Check(await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SupplierPayments WHERE SupplierId=8") == 0
        && await conn.ExecuteScalarAsync<decimal>("SELECT CurrentDebt FROM Suppliers WHERE SupplierId=8") == 1000,
        "Failure while journaling rolls back both payment and supplier balance");
    await conn.ExecuteAsync("DROP TRIGGER reject_operation");
    var rollbackReversal = await Pay(Request(8, 100));
    await conn.ExecuteAsync("""
        CREATE TRIGGER reject_reversal BEFORE UPDATE ON SupplierDebtPaymentOperations
        FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Injected reversal failure'
        """);
    try { await Reverse(8, rollbackReversal.PaymentId); throw new Exception("Expected injected failure"); }
    catch (MySqlException) { }
    Check(await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM SupplierPayments WHERE SupplierId=8") == 1
        && await conn.ExecuteScalarAsync<decimal>("SELECT CurrentDebt FROM Suppliers WHERE SupplierId=8") == 900,
        "Reversal failure rolls back compensating payment and debt restoration");
    await conn.ExecuteAsync("DROP TRIGGER reject_reversal");
    Console.WriteLine("Database integration checks completed.");
}
finally
{
    MySqlConnection.ClearAllPools();
    await admin.ExecuteAsync($"DROP DATABASE `{db}`");
}
