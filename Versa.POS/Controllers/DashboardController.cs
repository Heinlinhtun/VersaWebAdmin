using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public DashboardController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        private static async Task EnsurePurchaseBatchRemainingColumnAsync(MySqlConnection conn)
        {
            var hasColumn = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'PurchaseOrderItems'
  AND COLUMN_NAME = 'RemainingQuantity'");

            if (hasColumn == 0)
            {
                await conn.ExecuteAsync("ALTER TABLE PurchaseOrderItems ADD COLUMN RemainingQuantity DECIMAL(18,5) NULL");
            }

            await conn.ExecuteAsync(@"
UPDATE PurchaseOrderItems poi
LEFT JOIN UnitConversions uc
  ON uc.ProductId = poi.ProductId
 AND uc.FromUnitId = poi.UnitId
SET poi.RemainingQuantity = (IFNULL(poi.Quantity, 0) + IFNULL(poi.FOCQuantity, 0)) * IFNULL(uc.Multiplier, 1)
WHERE poi.RemainingQuantity IS NULL");
        }

        [HttpGet("summary/{branchId}")]
        public async Task<IActionResult> GetDailySummary(int branchId, [FromQuery] int days = 7, [FromQuery] string? date = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await EnsurePurchaseBatchRemainingColumnAsync(conn);
            var selectedDate = ResolveDashboardDate(date);

            // ၁။ ဒီနေ့အရောင်းနှင့် အကြွေးစာရင်းချုပ်
            string salesSql = @"
                SELECT 
                    IFNULL(SUM(TotalAmount), 0) as todaySales,
                    IFNULL(SUM(PaidAmount), 0) as cashInHand,
                    IFNULL(SUM(DueAmount), 0) as todayCredit,
                    COUNT(SaleId) as invoiceCount
                FROM Sales 
                WHERE BranchId = @branchId AND DATE(SaleDate) = @selectedDate";

            var salesSummary = await conn.QueryFirstOrDefaultAsync<dynamic>(salesSql, new { branchId, selectedDate });

            // ၂။ ဒီနေ့အမြတ် (Gross Profit)
            string profitSql = @"
                SELECT IFNULL(SUM((si.UnitPrice - si.CostPrice) * si.Quantity), 0)
                FROM SaleItems si
                JOIN Sales s ON si.SaleId = s.SaleId
                WHERE s.BranchId = @branchId AND DATE(s.SaleDate) = @selectedDate";

            decimal grossProfit = await conn.ExecuteScalarAsync<decimal>(profitSql, new { branchId, selectedDate });

            // ၃။ ပစ္စည်းလက်ကျန်နည်းနေသည့် List
            string stockAlertSql = @"
                SELECT p.Name as name, s.Quantity as quantity, p.AlertQuantity as alertQuantity
                FROM ProductStocks s
                JOIN Products p ON s.ProductId = p.ProductId
                WHERE s.BranchId = @branchId AND s.Quantity <= p.AlertQuantity
                LIMIT 10";

            var stockAlerts = await conn.QueryAsync<dynamic>(stockAlertSql, new { branchId });

            // ၄။ စုစုပေါင်း ပစ္စည်းအမျိုးအစားအရေအတွက် (Total Inventory Product Count)
            string totalInventorySql = @"
                SELECT COUNT(DISTINCT ps.ProductId)
                FROM ProductStocks ps
                INNER JOIN Products p ON ps.ProductId = p.ProductId
                WHERE ps.BranchId = @branchId
                  AND ps.Quantity > 0";
            int totalInventory = await conn.ExecuteScalarAsync<int>(totalInventorySql, new { branchId });

            // ၅။ စုစုပေါင်း ဝယ်သူအရေအတွက် (Active Customers)
            string activeCustomersSql = "SELECT COUNT(*) FROM Customers";
            int activeCustomers = await conn.ExecuteScalarAsync<int>(activeCustomersSql);

            // ၆။ ဒီနေ့အသုံးစရိတ် (Today's Expenses)
            string expensesSql = "SELECT IFNULL(SUM(Amount), 0) FROM Expenses WHERE BranchId = @branchId AND DATE(ExpenseDate) = @selectedDate";
            decimal todayExpenses = await conn.ExecuteScalarAsync<decimal>(expensesSql, new { branchId, selectedDate });
 
            // ၇။ သက်တမ်းကုန်ခါနီး ပစ္စည်းများ (Expiring Soon - based on product-specific AlertDays)
            string expirySoonSql = @"
                SELECT 
                    COUNT(*) as ProductCount,
                    IFNULL(SUM(RemainingQuantity), 0) as TotalQty
                FROM (
                    SELECT poi.POItemId, poi.RemainingQuantity
                    FROM PurchaseOrderItems poi
                    JOIN PurchaseOrders po ON poi.POId = po.POId
                    WHERE po.BranchId = @branchId 
                    AND poi.ExpiryDate IS NOT NULL 
                    AND poi.ExpiryDate BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL IFNULL(poi.AlertDays, 30) DAY)
                    AND IFNULL(poi.RemainingQuantity, 0) > 0
                ) p";
            var expirySoonRes = await conn.QueryFirstOrDefaultAsync<dynamic>(expirySoonSql, new { branchId });
            int expiredSoonCount = Convert.ToInt32(expirySoonRes.ProductCount);
            decimal expiredSoonQty = Convert.ToDecimal(expirySoonRes.TotalQty);

            // ၇။ ရက်သတ္တပတ်အရောင်း Analytics (Last 7 Days)
            string chartSql = @"
                SELECT DATE(SaleDate) as `date`, SUM(TotalAmount) as total 
                FROM Sales 
                WHERE BranchId = @branchId AND SaleDate >= DATE_SUB(CURDATE(), INTERVAL @days DAY) 
                GROUP BY DATE(SaleDate) 
                ORDER BY `date` ASC";
            var salesAnalyticsList = (await conn.QueryAsync<dynamic>(chartSql, new { branchId, days })).ToList();

            // ၈။ လတ်တလော ဆောင်ရွက်ချက်များ (Recent Activity)
            string activitySql = @"
                SELECT 'Sale' as type, CONCAT('SA', LPAD(SaleId, 7, '0')) as title, SaleId as id, SaleDate as date, TotalAmount as amount
                FROM Sales 
                WHERE BranchId = @branchId
                ORDER BY SaleDate DESC LIMIT 5";
            var recentActivities = await conn.QueryAsync<dynamic>(activitySql, new { branchId });

            // Diagnostic Info
            var diagnostics = new
            {
                DbName = TenantDbName,
                TotalSalesCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Sales"),
                TotalCustomersCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Customers"),
                TotalProductsCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Products"),
                DbCurDate = await conn.ExecuteScalarAsync<string>("SELECT CURDATE()"),
                DbNow = await conn.ExecuteScalarAsync<string>("SELECT NOW()"),
                BranchIdPassed = branchId,
                SelectedDate = selectedDate,
                DaysRequested = days,
                ChartDataCount = salesAnalyticsList.Count
            };

            return Ok(new
            {
                sales = salesSummary,
                profit = grossProfit,
                stockAlerts = stockAlerts,
                totalInventory = totalInventory,
                activeCustomers = activeCustomers,
                todayExpenses = todayExpenses,
                expiredSoon = expiredSoonCount,
                expiredSoonQty = expiredSoonQty,
                salesAnalytics = salesAnalyticsList,
                recentActivities = recentActivities,
                serverDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                diagnostics = diagnostics
            });
        }

        [HttpGet("top-items/{branchId}")]
        public async Task<IActionResult> GetTopSoldItems(int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            string sql = @"
                SELECT p.Name as name, SUM(si.Quantity) as totalQuantity, SUM(si.SubTotal) as totalAmount
                FROM SaleItems si
                JOIN Sales s ON si.SaleId = s.SaleId
                JOIN Products p ON si.ProductId = p.ProductId
                WHERE s.BranchId = @branchId
                GROUP BY p.ProductId, p.Name
                ORDER BY totalQuantity DESC
                LIMIT 10";

            var result = await conn.QueryAsync<dynamic>(sql, new { branchId });
            return Ok(result);
        }

        [HttpGet("top-customers/{branchId}")]
        public async Task<IActionResult> GetTopCustomers(int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            string sql = @"
                SELECT c.Name as name, COUNT(s.SaleId) as visitCount, SUM(s.TotalAmount) as totalSpent
                FROM Sales s
                JOIN Customers c ON s.CustomerId = c.CustomerId
                WHERE s.BranchId = @branchId
                GROUP BY c.CustomerId, c.Name
                ORDER BY totalSpent DESC
                LIMIT 10";

            var result = await conn.QueryAsync<dynamic>(sql, new { branchId });
            return Ok(result);
        }

        [HttpGet("today-sales-items/{branchId}")]
        public async Task<IActionResult> GetTodaySalesItems(int branchId, [FromQuery] string? date = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            var selectedDate = ResolveDashboardDate(date);
            string sql = @"
                SELECT p.Name as name, u.UnitName as UnitName, SUM(si.Quantity) as totalQuantity, SUM(si.SubTotal) as totalAmount
                FROM SaleItems si
                JOIN Sales s ON si.SaleId = s.SaleId
                JOIN Products p ON si.ProductId = p.ProductId
                LEFT JOIN Units u ON si.UnitId = u.UnitId
                WHERE s.BranchId = @branchId AND DATE(s.SaleDate) = @selectedDate
                GROUP BY p.ProductId, p.Name, u.UnitName
                ORDER BY totalAmount DESC";

            var result = await conn.QueryAsync<dynamic>(sql, new { branchId, selectedDate });
            return Ok(result);
        }

        [HttpGet("top-expenses/{branchId}")]
        public async Task<IActionResult> GetTopExpenses(int branchId, [FromQuery] string? date = null)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            var selectedDate = ResolveDashboardDate(date);
            string sql = @"
                SELECT Title as name, SUM(Amount) as totalAmount
                FROM Expenses
                WHERE BranchId = @branchId AND DATE(ExpenseDate) = @selectedDate
                GROUP BY Title
                ORDER BY totalAmount DESC
                LIMIT 10";

            var result = await conn.QueryAsync<dynamic>(sql, new { branchId, selectedDate });
            return Ok(result);
        }

        [HttpGet("expired-soon/{branchId}")]
        public async Task<IActionResult> GetExpiredSoonItems(int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await EnsurePurchaseBatchRemainingColumnAsync(conn);
            string sql = @"
                SELECT 
                    p.Name as name, 
                    poi.ExpiryDate as expiryDate, 
                    (IFNULL(poi.RemainingQuantity, 0) / IFNULL(uc.Multiplier, 1)) as batchQuantity,
                    poi.FOCQuantity as focQuantity,
                    ps.Quantity as totalStock,
                    IFNULL(poi.AlertDays, 30) as alertDays,
                    DATEDIFF(poi.ExpiryDate, CURDATE()) as daysRemaining
                FROM PurchaseOrderItems poi
                JOIN PurchaseOrders po ON poi.POId = po.POId
                JOIN Products p ON poi.ProductId = p.ProductId
                JOIN ProductStocks ps ON poi.ProductId = ps.ProductId AND po.BranchId = ps.BranchId
                LEFT JOIN UnitConversions uc ON uc.ProductId = poi.ProductId AND uc.FromUnitId = poi.UnitId
                WHERE po.BranchId = @branchId 
                AND poi.ExpiryDate IS NOT NULL 
                AND poi.ExpiryDate BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL IFNULL(poi.AlertDays, 30) DAY)
                AND IFNULL(poi.RemainingQuantity, 0) > 0
                ORDER BY poi.ExpiryDate ASC";

            var result = await conn.QueryAsync<dynamic>(sql, new { branchId });
            return Ok(result);
        }

        private static string ResolveDashboardDate(string? date)
        {
            return DateOnly.TryParse(date, out var parsed)
                ? parsed.ToString("yyyy-MM-dd")
                : DateTime.Today.ToString("yyyy-MM-dd");
        }
    }
}
