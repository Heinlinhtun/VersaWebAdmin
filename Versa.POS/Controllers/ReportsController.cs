using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Dynamic;
using Versa.Core.Services;
using Versa.POS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : BaseShopController
{
    private readonly DatabaseService _dbService;

    public ReportsController(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    private int? ResolveBranchScope(int? requestedBranchId)
    {
        if (User.IsInRole("Admin"))
        {
            return requestedBranchId;
        }

        var branchClaim = User.FindFirst("BranchId")?.Value;
        return int.TryParse(branchClaim, out var branchId) ? branchId : requestedBranchId;
    }

    private static async Task EnsureSaleReturnsPaymentMethodColumnAsync(MySqlConnection conn)
    {
        var hasColumn = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'SaleReturns'
  AND COLUMN_NAME = 'ReturnPaymentMethod'");

        if (hasColumn == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE SaleReturns ADD COLUMN ReturnPaymentMethod VARCHAR(50) NULL");
        }
    }

    private sealed class PaymentChannelRow
    {
        public int SaleId { get; set; }
        public string Method { get; set; } = "Unknown";
        public decimal Amount { get; set; }
    }

    private sealed class ReturnChannelRow
    {
        public int SaleId { get; set; }
        public decimal RefundAmount { get; set; }
        public string ReturnPaymentMethod { get; set; } = "";
    }

    private static string CleanPaymentMethod(string? method)
    {
        return string.IsNullOrWhiteSpace(method) ? "Unknown" : method.Trim();
    }

    private static void DeductReturnFromChannels(
        Dictionary<string, decimal> channels,
        decimal refundAmount,
        string? preferredMethod)
    {
        var remainingDeduct = Math.Max(0m, refundAmount);
        if (remainingDeduct <= 0) return;

        if (!string.IsNullOrWhiteSpace(preferredMethod))
        {
            var preferred = preferredMethod.Trim();
            if (channels.TryGetValue(preferred, out var preferredAmount) && preferredAmount > 0)
            {
                var directDeduct = Math.Min(preferredAmount, remainingDeduct);
                channels[preferred] = preferredAmount - directDeduct;
                remainingDeduct -= directDeduct;
            }
            else if (!channels.Any(x => x.Value > 0))
            {
                channels[preferred] = channels.GetValueOrDefault(preferred) - remainingDeduct;
                return;
            }
        }

        if (remainingDeduct <= 0) return;

        var positiveChannels = channels
            .Where(x => x.Value > 0)
            .Select(x => new
            {
                x.Key,
                x.Value,
                ExactDeduction = remainingDeduct * x.Value / channels.Where(c => c.Value > 0).Sum(c => c.Value)
            })
            .ToList();

        if (positiveChannels.Count == 0)
        {
            var fallback = CleanPaymentMethod(preferredMethod);
            channels[fallback] = channels.GetValueOrDefault(fallback) - remainingDeduct;
            return;
        }

        var floorAlloc = positiveChannels
            .Select(x => new
            {
                Method = x.Key,
                Deducted = Math.Floor(x.ExactDeduction),
                Fraction = x.ExactDeduction - Math.Floor(x.ExactDeduction),
                x.Value
            })
            .ToList();

        var allocated = floorAlloc.Sum(x => x.Deducted);
        var remainder = (int)Math.Round(remainingDeduct - allocated, MidpointRounding.AwayFromZero);

        foreach (var row in floorAlloc.OrderByDescending(x => x.Fraction).ThenByDescending(x => x.Value))
        {
            var deduct = row.Deducted + (remainder > 0 ? 1 : 0);
            if (remainder > 0) remainder--;
            channels[row.Method] = channels[row.Method] - deduct;
        }
    }

    private async Task<List<PaymentChannelRow>> GetSalePaymentChannelsAsync(
        MySqlConnection conn,
        IEnumerable<int> saleIds)
    {
        var ids = saleIds.Distinct().ToArray();
        if (ids.Length == 0) return new List<PaymentChannelRow>();

        var paymentRows = (await conn.QueryAsync<PaymentChannelRow>(@"
            SELECT
                cp.SaleId,
                COALESCE(NULLIF(cp.PaymentMethod, ''), 'Unknown') AS Method,
                IFNULL(cp.Amount, 0) AS Amount
            FROM CustomerPayments cp
            WHERE cp.SaleId IN @SaleIds", new { SaleIds = ids })).ToList();

        var fallbackRows = await conn.QueryAsync<PaymentChannelRow>(@"
            SELECT
                s.SaleId,
                COALESCE(NULLIF(s.PaymentMethod, ''), 'Unknown') AS Method,
                IFNULL(s.PaidAmount, 0) AS Amount
            FROM Sales s
            WHERE s.SaleId IN @SaleIds
              AND IFNULL(s.PaidAmount, 0) > 0
              AND NOT EXISTS (
                    SELECT 1
                    FROM CustomerPayments cp
                    WHERE cp.SaleId = s.SaleId
              )", new { SaleIds = ids });

        paymentRows.AddRange(fallbackRows);
        foreach (var row in paymentRows)
        {
            row.Method = CleanPaymentMethod(row.Method);
            row.Amount = Math.Max(0m, row.Amount);
        }

        return paymentRows;
    }

    private async Task<Dictionary<int, string>> GetNetPaymentSummariesAsync(
        MySqlConnection conn,
        IEnumerable<int> saleIds)
    {
        var ids = saleIds.Distinct().ToArray();
        var result = ids.ToDictionary(x => x, _ => "");
        if (ids.Length == 0) return result;

        await EnsureSaleReturnsPaymentMethodColumnAsync(conn);

        var paymentRows = await GetSalePaymentChannelsAsync(conn, ids);
        var returnRows = await conn.QueryAsync<ReturnChannelRow>(@"
            SELECT
                SaleId,
                IFNULL(RefundAmount, 0) AS RefundAmount,
                COALESCE(NULLIF(ReturnPaymentMethod, ''), '') AS ReturnPaymentMethod
            FROM SaleReturns
            WHERE SaleId IN @SaleIds", new { SaleIds = ids });

        foreach (var saleId in ids)
        {
            var channels = paymentRows
                .Where(x => x.SaleId == saleId)
                .GroupBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Sum(v => v.Amount), StringComparer.OrdinalIgnoreCase);

            foreach (var ret in returnRows.Where(x => x.SaleId == saleId))
            {
                DeductReturnFromChannels(channels, ret.RefundAmount, ret.ReturnPaymentMethod);
            }

            result[saleId] = string.Join(", ",
                channels
                    .Where(x => Math.Round(x.Value, 0, MidpointRounding.AwayFromZero) > 0)
                    .OrderBy(x => x.Key)
                    .Select(x => $"{x.Key} - {Math.Round(x.Value, 0, MidpointRounding.AwayFromZero)}"));
        }

        return result;
    }

    private static async Task EnsureBundleItemsSupportsServicesAsync(MySqlConnection conn)
    {
        var hasItemType = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'ItemType'");

        if (hasItemType == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN ItemType VARCHAR(20) NOT NULL DEFAULT 'Product'");
        }

        var hasServiceId = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'ServiceId'");

        if (hasServiceId == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN ServiceId INT NULL");
        }

        var hasUnitId = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'BundleItems'
  AND COLUMN_NAME = 'UnitId'");

        if (hasUnitId == 0)
        {
            await conn.ExecuteAsync("ALTER TABLE BundleItems ADD COLUMN UnitId INT NULL");
        }

        try
        {
            await conn.ExecuteAsync("ALTER TABLE BundleItems MODIFY COLUMN ComponentProductId INT NULL");
        }
        catch
        {
        }
    }

    // ၁။ Sale Reports: အရောင်းအနှစ်ချုပ် (Net Sales, Discount, Deli Fee အားလုံးပါဝင်သည်)
    [HttpGet("sales-summary")]
    public async Task<IActionResult> GetSalesSummary([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        string sql = @"
        SELECT 
            COUNT(s.SaleId) as TotalInvoices,
            IFNULL(SUM(s.TotalAmount), 0) as ItemsSubTotal,
            IFNULL(SUM(s.DiscountAmount), 0) as TotalDiscounts,
            IFNULL(SUM(s.DeliveryFee), 0) as TotalDeliveryFees,
            IFNULL(SUM(IFNULL(sr.ReturnedAmount, 0)), 0) as TotalReturnedAmount,
            IFNULL(SUM(GREATEST(0, (s.TotalAmount - s.DiscountAmount + s.DeliveryFee) - IFNULL(sr.ReturnedAmount, 0))), 0) as NetRevenue,
            IFNULL(( 
                SELECT SUM(cp.Amount)
                FROM CustomerPayments cp
                LEFT JOIN Sales ps ON ps.SaleId = cp.SaleId
                WHERE cp.PaymentDate BETWEEN @start AND @end
                  AND (@branchId IS NULL OR ps.BranchId = @branchId OR ps.SaleId IS NULL)
            ), 0) - IFNULL((
                SELECT SUM(sr2.RefundAmount)
                FROM SaleReturns sr2
                INNER JOIN Sales rs ON rs.SaleId = sr2.SaleId
                WHERE sr2.ReturnDate BETWEEN @start AND @end
                  AND (@branchId IS NULL OR rs.BranchId = @branchId)
            ), 0) as TotalCollected,
            IFNULL(SUM(GREATEST(0, s.DueAmount - IFNULL(sr.ReturnedAmount, 0))), 0) as TotalCredits
        FROM Sales s
        LEFT JOIN (
            SELECT SaleId, IFNULL(SUM(RefundAmount), 0) as ReturnedAmount
            FROM SaleReturns
            GROUP BY SaleId
        ) sr ON sr.SaleId = s.SaleId
        WHERE s.SaleDate BETWEEN @start AND @end
          AND (@branchId IS NULL OR s.BranchId = @branchId)";

        var summary = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId });
        return Ok(summary);
    }

    [HttpGet("sales-list")] // အမည်ကို ပိုရှင်းအောင် sales-list လို့ ပြောင်းလိုက်ပါတယ်
    public async Task<IActionResult> GetSalesList([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        // Discount မပါတဲ့ အရောင်းတွေပါ ထွက်လာဖို့ WHERE clause ကို ပြင်ရန်
        string sql = @"
        SELECT 
            s.SaleId,
            c.Name as CustomerName,
            s.TotalAmount as SubTotal,
            s.DiscountAmount,
            s.DeliveryFee,
            IFNULL(sr.ReturnedAmount, 0) as ReturnedAmount,
            GREATEST(0, s.TotalAmount - IFNULL(sr.ReturnedAmount, 0)) as NetSubTotal,
            GREATEST(0, (s.TotalAmount - s.DiscountAmount + s.DeliveryFee) - IFNULL(sr.ReturnedAmount, 0)) as NetTotal,
            s.PaymentMethod,
            s.PaidAmount,
            CASE
                WHEN cp.PaymentSummary IS NOT NULL AND cp.PaymentSummary <> '' THEN cp.PaymentSummary
                WHEN IFNULL(s.PaidAmount, 0) > 0 THEN CONCAT(COALESCE(NULLIF(s.PaymentMethod, ''), 'Unknown'), ' - ', ROUND(s.PaidAmount, 0))
                ELSE ''
            END AS PaymentSummary,
            s.SaleDate
        FROM Sales s
        LEFT JOIN Customers c ON s.CustomerId = c.CustomerId
        LEFT JOIN (
            SELECT
                x.SaleId,
                GROUP_CONCAT(CONCAT(x.Method, ' - ', x.TotalAmount) ORDER BY x.Method SEPARATOR ', ') AS PaymentSummary
            FROM (
                SELECT
                    SaleId,
                    COALESCE(NULLIF(PaymentMethod, ''), 'Unknown') AS Method,
                    ROUND(SUM(Amount), 0) AS TotalAmount
                FROM CustomerPayments
                GROUP BY SaleId, COALESCE(NULLIF(PaymentMethod, ''), 'Unknown')
            ) x
            GROUP BY x.SaleId
        ) cp ON cp.SaleId = s.SaleId
        LEFT JOIN (
            SELECT SaleId, IFNULL(SUM(RefundAmount), 0) as ReturnedAmount
            FROM SaleReturns
            GROUP BY SaleId
        ) sr ON sr.SaleId = s.SaleId
        WHERE s.SaleDate BETWEEN @start AND @end
          AND (@branchId IS NULL OR s.BranchId = @branchId)
        ORDER BY s.SaleDate DESC";

        var sales = (await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId })).ToList();
        var saleIds = sales.Select(x => Convert.ToInt32(((IDictionary<string, object>)x)["SaleId"])).ToArray();
        var netPaymentSummaries = await GetNetPaymentSummariesAsync(conn, saleIds);
        var responseRows = sales.Select(row =>
        {
            var source = (IDictionary<string, object>)row;
            var copy = (IDictionary<string, object?>)new ExpandoObject();
            foreach (var kv in source)
            {
                copy[kv.Key] = kv.Value;
            }

            var saleId = Convert.ToInt32(source["SaleId"]);
            if (netPaymentSummaries.TryGetValue(saleId, out var netSummary))
            {
                copy["PaymentSummary"] = netSummary;
            }

            return copy;
        }).ToList();
        return Ok(responseRows);
    }

    [HttpGet("sales-items")]
    public async Task<IActionResult> GetSalesItems([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        string sql = @"
        SELECT
            s.SaleId,
            CONCAT('SA-', LPAD(s.SaleId, 7, '0')) AS VoucherNo,
            s.SaleDate,
            IFNULL(c.Name, 'Walk-in Client') AS CustomerName,
            si.SaleItemId,
            COALESCE(
                CASE
                    WHEN si.ItemType = 'Product' THEN p.Name
                    WHEN si.ItemType = 'Service' THEN sv.ServiceName
                    WHEN si.ItemType = 'Bundle' THEN b.BundleName
                    ELSE NULL
                END,
                'Unknown Item'
            ) AS ItemName,
            COALESCE(p.Sku, '') AS Sku,
            COALESCE(u.UnitName, '') AS UnitName,
            IFNULL(si.Quantity, 0) AS Quantity,
            IFNULL(si.UnitPrice, 0) AS UnitPrice,
            IFNULL(si.DiscountAmount, 0) AS DiscountAmount,
            IFNULL(si.IsFOC, 0) AS IsFOC,
            CASE
                WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
                ELSE IFNULL(si.SubTotal, (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0))
            END AS LineTotal,
            si.ItemType
        FROM SaleItems si
        INNER JOIN Sales s ON s.SaleId = si.SaleId
        LEFT JOIN Customers c ON s.CustomerId = c.CustomerId
        LEFT JOIN Products p ON si.ProductId = p.ProductId
        LEFT JOIN Services sv ON si.ServiceId = sv.ServiceId
        LEFT JOIN Bundles b ON si.BundleId = b.BundleId
        LEFT JOIN Units u ON si.UnitId = u.UnitId
        WHERE s.SaleDate BETWEEN @start AND @end
          AND (@branchId IS NULL OR s.BranchId = @branchId)
        ORDER BY s.SaleDate DESC, s.SaleId DESC, si.SaleItemId DESC";

        var items = await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId });
        return Ok(items);
    }

    [HttpGet("credit-collections")]
    public async Task<IActionResult> GetCreditCollections([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        string sql = @"
        SELECT
            cp.PaymentId,
            cp.SaleId,
            CASE
                WHEN cp.SaleId IS NULL THEN 'Manual Credit'
                ELSE CONCAT('SA-', LPAD(cp.SaleId, 7, '0'))
            END AS VoucherNo,
            cp.PaymentDate,
            s.SaleDate,
            IFNULL(c.Name, 'Walk-in Client') AS CustomerName,
            COALESCE(NULLIF(cp.PaymentMethod, ''), 'Unknown') AS PaymentMethod,
            IFNULL(cp.Amount, 0) AS Amount,
            cp.ReferenceNo,
            cp.Note,
            IFNULL(s.DueAmount, 0) AS RemainingDue
        FROM CustomerPayments cp
        LEFT JOIN Sales s ON s.SaleId = cp.SaleId
        LEFT JOIN Customers c ON c.CustomerId = cp.CustomerId
        WHERE cp.PaymentDate BETWEEN @start AND @end
          AND (@branchId IS NULL OR s.BranchId = @branchId OR s.SaleId IS NULL)
        ORDER BY cp.PaymentDate DESC, cp.PaymentId DESC";

        var collections = await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId });
        return Ok(collections);
    }

    [HttpGet("purchase-report")]
    public async Task<IActionResult> GetPurchaseReport([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId, [FromQuery] int? supplierId)
    {
        // ၂။ ရလာတဲ့ Database Name နဲ့ Connection String ကို တောင်းပါ
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        try
        {
            string sql = @"
            SELECT 
                p.POId as PurchaseId, 
                IFNULL(s.SupplierName, 'N/A') as SupplierName, 
                p.TotalAmount,
                p.PaidAmount,
                sp.PaymentMethod,
                IFNULL(pr.ReturnedAmount, 0) as ReturnedAmount,
                GREATEST(0, p.TotalAmount - IFNULL(pr.ReturnedAmount, 0)) as NetTotalAmount,
                p.OrderDate as PurchaseDate,
                p.Status
            FROM PurchaseOrders p
            LEFT JOIN Suppliers s ON p.SupplierId = s.SupplierId
            LEFT JOIN (
                SELECT
                    ReferenceNo,
                    GROUP_CONCAT(DISTINCT COALESCE(NULLIF(PaymentMethod,''),'Unknown') ORDER BY PaymentMethod SEPARATOR ', ') AS PaymentMethod
                FROM SupplierPayments
                GROUP BY ReferenceNo
            ) sp ON sp.ReferenceNo = CONCAT('PU', LPAD(p.POId, 7, '0'))
            LEFT JOIN (
                SELECT POId, IFNULL(SUM(RefundAmount), 0) as ReturnedAmount
                FROM PurchaseReturns
                GROUP BY POId
            ) pr ON pr.POId = p.POId
            WHERE p.OrderDate BETWEEN @start AND @end
              AND (@branchId IS NULL OR p.BranchId = @branchId)
              AND (@supplierId IS NULL OR p.SupplierId = @supplierId)
            ORDER BY p.OrderDate DESC";

            var result = await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId, supplierId });
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("purchase-items")]
    public async Task<IActionResult> GetPurchaseItems([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId, [FromQuery] int? supplierId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        try
        {
            string sql = @"
            SELECT
                po.POId AS PurchaseId,
                CONCAT('PO-', LPAD(po.POId, 5, '0')) AS VoucherNo,
                po.OrderDate AS PurchaseDate,
                IFNULL(s.SupplierName, 'N/A') AS SupplierName,
                poi.POItemId,
                COALESCE(p.Name, CONCAT('Product #', poi.ProductId)) AS ProductName,
                COALESCE(p.Sku, '') AS Sku,
                COALESCE(u.UnitName, '') AS UnitName,
                IFNULL(poi.Quantity, 0) AS Quantity,
                IFNULL(poi.FOCQuantity, 0) AS FOCQuantity,
                IFNULL(poi.CostPrice, 0) AS CostPrice,
                IFNULL(pr.ReturnedQuantity, 0) AS ReturnedQuantity,
                GREATEST(0, IFNULL(poi.Quantity, 0) - IFNULL(pr.ReturnedQuantity, 0)) AS NetQuantity,
                GREATEST(0, IFNULL(poi.Quantity, 0) - IFNULL(pr.ReturnedQuantity, 0)) * IFNULL(poi.CostPrice, 0) AS LineTotal
            FROM PurchaseOrderItems poi
            INNER JOIN PurchaseOrders po ON po.POId = poi.POId
            LEFT JOIN Suppliers s ON po.SupplierId = s.SupplierId
            LEFT JOIN Products p ON poi.ProductId = p.ProductId
            LEFT JOIN Units u ON poi.UnitId = u.UnitId
            LEFT JOIN (
                SELECT POId, ProductId, UnitId, IFNULL(SUM(Quantity), 0) AS ReturnedQuantity
                FROM PurchaseReturns
                GROUP BY POId, ProductId, UnitId
            ) pr ON pr.POId = poi.POId AND pr.ProductId = poi.ProductId AND pr.UnitId = poi.UnitId
            WHERE po.OrderDate BETWEEN @start AND @end
              AND (@branchId IS NULL OR po.BranchId = @branchId)
              AND (@supplierId IS NULL OR po.SupplierId = @supplierId)
            ORDER BY po.OrderDate DESC, po.POId DESC, poi.POItemId DESC";

            var items = await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId, supplierId });
            return Ok(items);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    // ReportsController.cs
    [HttpGet("payment-methods-summary")]
    public async Task<IActionResult> GetPaymentMethodsSummary([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureSaleReturnsPaymentMethodColumnAsync(conn);
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        var paymentRows = await conn.QueryAsync<(int SaleId, string Method, decimal Amount)>(@"
            SELECT 
                cp.SaleId,
                COALESCE(NULLIF(cp.PaymentMethod, ''), 'Unknown') AS Method,
                IFNULL(cp.Amount, 0) AS Amount
            FROM CustomerPayments cp
            INNER JOIN Sales s ON s.SaleId = cp.SaleId
            WHERE cp.PaymentDate BETWEEN @start AND @end
              AND (@branchId IS NULL OR s.BranchId = @branchId)", new { start, end, branchId = scopedBranchId });

        var returnRows = await conn.QueryAsync<(int SaleId, decimal RefundAmount, string ReturnPaymentMethod)>(@"
            SELECT
                sr.SaleId,
                IFNULL(RefundAmount, 0) AS RefundAmount,
                COALESCE(NULLIF(ReturnPaymentMethod, ''), '') AS ReturnPaymentMethod
            FROM SaleReturns sr
            INNER JOIN Sales s ON s.SaleId = sr.SaleId
            WHERE sr.ReturnDate BETWEEN @start AND @end
              AND (@branchId IS NULL OR s.BranchId = @branchId)", new { start, end, branchId = scopedBranchId });

        var saleRowsWithoutCustomerPayments = await conn.QueryAsync<(int SaleId, string Method, decimal PaidAmount)>(@"
            SELECT
                s.SaleId,
                COALESCE(NULLIF(s.PaymentMethod, ''), 'Unknown') AS Method,
                IFNULL(s.PaidAmount, 0) AS PaidAmount
            FROM Sales s
            WHERE s.SaleDate BETWEEN @start AND @end
              AND (@branchId IS NULL OR s.BranchId = @branchId)
              AND IFNULL(s.PaidAmount, 0) > 0
              AND NOT EXISTS (
                    SELECT 1
                    FROM CustomerPayments cp2
                    WHERE cp2.SaleId = s.SaleId
              )", new { start, end, branchId = scopedBranchId });

        var methodTotals = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var payment in paymentRows)
        {
            var method = CleanPaymentMethod(payment.Method);
            if (!methodTotals.ContainsKey(method)) methodTotals[method] = 0m;
            methodTotals[method] += Math.Max(0m, payment.Amount);
        }

        foreach (var row in saleRowsWithoutCustomerPayments)
        {
            var method = CleanPaymentMethod(row.Method);
            if (!methodTotals.ContainsKey(method)) methodTotals[method] = 0m;
            methodTotals[method] += Math.Max(0m, row.PaidAmount);
        }

        var returnSaleIds = returnRows.Select(x => x.SaleId).Distinct().ToArray();
        var originalPaymentChannels = (await GetSalePaymentChannelsAsync(conn, returnSaleIds))
            .GroupBy(x => x.SaleId)
            .ToDictionary(
                x => x.Key,
                x => x.GroupBy(v => v.Method, StringComparer.OrdinalIgnoreCase)
                      .ToDictionary(v => v.Key, v => v.Sum(r => r.Amount), StringComparer.OrdinalIgnoreCase));

        foreach (var ret in returnRows)
        {
            Dictionary<string, decimal> returnChannels;
            if (originalPaymentChannels.TryGetValue(ret.SaleId, out var originalChannels) && originalChannels.Count > 0)
            {
                returnChannels = originalChannels.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                var fallback = CleanPaymentMethod(ret.ReturnPaymentMethod);
                returnChannels = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
                {
                    [fallback] = 0m
                };
            }

            var before = returnChannels.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            DeductReturnFromChannels(returnChannels, ret.RefundAmount, ret.ReturnPaymentMethod);

            foreach (var kv in before)
            {
                var deducted = kv.Value - returnChannels.GetValueOrDefault(kv.Key);
                if (deducted <= 0) continue;
                if (!methodTotals.ContainsKey(kv.Key)) methodTotals[kv.Key] = 0m;
                methodTotals[kv.Key] -= deducted;
            }

            foreach (var kv in returnChannels.Where(x => x.Value < 0))
            {
                if (!methodTotals.ContainsKey(kv.Key)) methodTotals[kv.Key] = 0m;
                methodTotals[kv.Key] += kv.Value;
            }
        }

        var rawResults = methodTotals
            .Select(x => new { Method = x.Key, TotalAmount = Math.Round(x.Value, 0, MidpointRounding.AwayFromZero) })
            .Where(x => x.TotalAmount != 0)
            .OrderByDescending(x => x.TotalAmount)
            .ToList();

        // Fix for corrupted encoding data (e.g. "??????" instead of Burmese)
        var fixedResults = rawResults.Select(r => {
            string m = r.Method?.ToString() ?? "Unknown";
            // Map common corrupted patterns back to Burmese
            if (m == "??????" || m == "လက်ငင်း") m = "လက်ငင်း"; // Cash
            else if (m == "????????????" || m == "ဘဏ်စာရင်း") m = "ဘဏ်စာရင်း"; // Banking
            else if (m.All(c => c == '?') && m.Length > 0) m = "လက်ငင်း";

            return new { Method = m, TotalAmount = r.TotalAmount };
        });

        return Ok(fixedResults);
    }

    [HttpGet("inventory-report")]
    public async Task<IActionResult> GetInventoryReport()
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // SQL မှာတင် NULL ဖြစ်နိုင်တဲ့ တန်ဖိုးတွေကို 0 အဖြစ် ပြောင်းလဲတွက်ချက်ပါမည်
        string sql = @"
        SELECT 
            ProductId, ProductName, SKU, CategoryName,
            IFNULL(StockQuantity, 0) as StockQuantity, 
            IFNULL(CostPrice, 0) as CostPrice,
            IFNULL(MinStockLevel, 0) as MinStockLevel,
            IFNULL(StockQuantity * CostPrice, 0) as TotalStockValue
        FROM Products
        WHERE IsActive = 1";

        var inventory = await conn.QueryAsync<dynamic>(sql);

        // C# ဘက်မှာ Sum တွက်တဲ့အခါ decimal? (nullable) ကို သုံးပြီး Error ကာကွယ်ပါမည်
        return Ok(new
        {
            Items = inventory,
            TotalValue = inventory.Sum(x => (decimal?)x.TotalStockValue ?? 0m)
        });
    }

    // ၂။ Discount Reports: ဘယ်သူတွေကို ဘယ်လောက် Discount ပေးခဲ့သလဲ
    [HttpGet("discount-report")]
    public async Task<IActionResult> GetDiscountReport([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var scopedBranchId = ResolveBranchScope(branchId);

        // ၁။ DateTime Range ကို တစ်ရက်အပြည့်ရအောင် ညှိခြင်း (အရေးကြီးဆုံး)
        var start = fromDate.Date; // 00:00:00
        var end = toDate.Date.AddDays(1).AddTicks(-1); // 23:59:59

        // ၂။ MySQL အတွက် EXISTS ကို Boolean (1/0) သေချာဖြစ်အောင် CAST လုပ်ခြင်း
        string sql = @"
        SELECT 
            s.SaleId,
            CONCAT('SA', LPAD(s.SaleId, 7, '0')) as SaleNo,
            c.Name as CustomerName,
            u.UserName as CashierName,
            s.TotalAmount as SubTotal,
            s.DiscountAmount as VoucherDiscount,
            s.SaleDate,
            IFNULL((SELECT SUM(si.DiscountAmount) FROM SaleItems si WHERE si.SaleId = s.SaleId), 0) as ItemTotalDiscount,
            -- EXISTS ကို MySQL မှာ Integer အနေနဲ့ ပြန်ပေးလို့ Frontend အတွက် bool ဆန်အောင် လုပ်ခြင်း
            CASE WHEN EXISTS(SELECT 1 FROM SaleItems WHERE SaleId = s.SaleId AND IsFOC = 1) THEN 1 ELSE 0 END as HasFOC
        FROM Sales s
        LEFT JOIN Customers c ON s.CustomerId = c.CustomerId
        LEFT JOIN ShopUsers u ON s.UserId = u.UserId
        WHERE 
            (s.DiscountAmount > 0 OR 
             EXISTS(SELECT 1 FROM SaleItems WHERE SaleId = s.SaleId AND (DiscountAmount > 0 OR IsFOC = 1)))
            AND s.SaleDate BETWEEN @start AND @end
            AND (@branchId IS NULL OR s.BranchId = @branchId)
        ORDER BY s.SaleDate DESC";

        // query parameter ကို start, end သုံးပြီး ခေါ်ပါ
        var reportData = (await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId })).ToList();

        // ၃။ ပစ္စည်းစာရင်းများကို ဆွဲယူခြင်း
        foreach (var sale in reportData)
        {
            // SaleItemId သို့မဟုတ် Id (Database table ထဲက နာမည်အတိုင်း ဖြစ်ရပါမည်)
            string itemDetailsSql = @"
            SELECT 
                si.SaleItemId as Id, 
                p.Name as ProductName,
                si.Quantity,
                si.DiscountAmount,
                si.IsFOC
            FROM SaleItems si
            JOIN Products p ON si.ProductId = p.ProductId
            WHERE si.SaleId = @SaleId AND (si.DiscountAmount > 0 OR si.IsFOC = 1)";

            sale.DiscountedItems = await conn.QueryAsync<dynamic>(itemDetailsSql, new { SaleId = sale.SaleId });
        }

        return Ok(reportData);
    }

    // ၃။ Total Profits: အသားတင်အမြတ် (ရင်းနှီးစရိတ် နှင့် Discount နှုတ်ပြီး)

    [HttpGet("consignment-report")]
    public async Task<IActionResult> GetConsignmentReport([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        const string summarySql = @"
SELECT
    IFNULL(SUM(CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE si.Quantity END), 0) AS SoldQuantity,
    IFNULL(SUM(CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE (si.Quantity * si.UnitPrice) - IFNULL(si.DiscountAmount, 0) END), 0) AS SalesRevenue,
    IFNULL(SUM(CASE
        WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
        WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
            (si.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, cfp.UnitPrice, 0)
        WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
            si.Quantity * si.CostPrice
        ELSE
            (si.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(cfp.UnitPrice, 0)
    END), 0) AS ConsignmentCost,
    IFNULL(SUM(CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE (si.Quantity * si.UnitPrice) - IFNULL(si.DiscountAmount, 0) END), 0)
    - IFNULL(SUM(CASE
        WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
        WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
            (si.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, cfp.UnitPrice, 0)
        WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
            si.Quantity * si.CostPrice
        ELSE
            (si.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(cfp.UnitPrice, 0)
    END), 0) AS GrossProfit,
    COUNT(DISTINCT s.SaleId) AS InvoiceCount,
    IFNULL((SELECT SUM(cs.TotalAmount)
            FROM ConsignmentSettlements cs
            WHERE cs.SettlementDate BETWEEN @start AND @end
              AND (@branchId IS NULL OR cs.BranchId = @branchId)), 0) AS PayoutPaid,
    IFNULL((SELECT SUM(ci2.SoldQuantity - ci2.SettledQuantity)
            FROM ConsignmentItems ci2
            JOIN Consignments c2 ON c2.ConsignId = ci2.ConsignId
            WHERE (@branchId IS NULL OR COALESCE(ci2.BranchId, c2.BranchId) = @branchId)), 0) AS CurrentUnsettledQuantity,
    IFNULL((SELECT SUM((ci2.SoldQuantity - ci2.SettledQuantity) * ci2.UnitPrice)
            FROM ConsignmentItems ci2
            JOIN Consignments c2 ON c2.ConsignId = ci2.ConsignId
            WHERE (@branchId IS NULL OR COALESCE(ci2.BranchId, c2.BranchId) = @branchId)), 0) AS CurrentPayoutDue,
    IFNULL((SELECT SUM(ci3.Quantity - ci3.SoldQuantity - ci3.ReturnedQuantity)
            FROM ConsignmentItems ci3
            JOIN Consignments c3 ON c3.ConsignId = ci3.ConsignId
            WHERE c3.Status = 'Active'
              AND (@branchId IS NULL OR COALESCE(ci3.BranchId, c3.BranchId) = @branchId)), 0) AS AvailableQuantity
FROM SaleItems si
JOIN Sales s ON s.SaleId = si.SaleId
LEFT JOIN Products p ON p.ProductId = si.ProductId
LEFT JOIN Categories pc ON pc.CategoryId = p.CategoryId
LEFT JOIN ConsignmentItems ci ON ci.ConsignItemId = si.ConsignItemId
LEFT JOIN (
    SELECT
        ci2.ProductId,
        COALESCE(ci2.BranchId, c2.BranchId) AS BranchId,
        MIN(ci2.UnitPrice) AS UnitPrice,
        MIN(c2.OrderDate) AS FirstConsignDate
    FROM ConsignmentItems ci2
    JOIN Consignments c2 ON c2.ConsignId = ci2.ConsignId
    GROUP BY ci2.ProductId, COALESCE(ci2.BranchId, c2.BranchId)
) cfp ON cfp.ProductId = si.ProductId AND cfp.BranchId = s.BranchId
LEFT JOIN UnitConversions uc ON uc.ProductId = si.ProductId AND uc.FromUnitId = si.UnitId
WHERE s.SaleDate BETWEEN @start AND @end
  AND (
      IFNULL(si.IsConsignment, 0) = 1
      OR si.ConsignItemId IS NOT NULL
      OR LOWER(TRIM(IFNULL(pc.CategoryName, ''))) = 'consignment'
      OR (
          cfp.ProductId IS NOT NULL
          AND cfp.FirstConsignDate <= s.SaleDate
          AND NOT EXISTS (
              SELECT 1
              FROM PurchaseOrderItems poi2
              JOIN PurchaseOrders po2 ON po2.POId = poi2.POId
              WHERE poi2.ProductId = si.ProductId
                AND po2.BranchId = s.BranchId
                AND IFNULL(poi2.Quantity, 0) > 0
          )
      )
  )
  AND (@branchId IS NULL OR s.BranchId = @branchId)";

        const string itemsSql = @"
SELECT
    s.SaleDate,
    CONCAT('SA', LPAD(s.SaleId, 7, '0')) AS VoucherNo,
    p.Name AS ProductName,
    p.Sku,
    COALESCE(sup.SupplierName, fsup.SupplierName) AS SupplierName,
    b.BranchName,
    IFNULL(si.Quantity, 0) AS Quantity,
    COALESCE(su.UnitName, cu.UnitName, fcu.UnitName, '') AS UnitName,
    IFNULL(si.Quantity, 0) * IFNULL(uc.Multiplier, 1) AS BaseQuantity,
    COALESCE(bu.UnitName, '') AS BaseUnitName,
    IFNULL(si.UnitPrice, 0) AS UnitPrice,
    IFNULL(si.DiscountAmount, 0) AS DiscountAmount,
    CASE
        WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, cfp.UnitPrice, 0)
        ELSE COALESCE(NULLIF(si.CostPrice, 0), cfp.UnitPrice, 0)
    END AS UnitCost,
    CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE (si.Quantity * si.UnitPrice) - IFNULL(si.DiscountAmount, 0) END AS Revenue,
    CASE
        WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
        WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
            (si.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, cfp.UnitPrice, 0)
        WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
            si.Quantity * si.CostPrice
        ELSE
            (si.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(cfp.UnitPrice, 0)
    END AS Cost,
    CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE (si.Quantity * si.UnitPrice) - IFNULL(si.DiscountAmount, 0) END
    - CASE
        WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
        WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
            (si.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, cfp.UnitPrice, 0)
        WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
            si.Quantity * si.CostPrice
        ELSE
            (si.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(cfp.UnitPrice, 0)
    END AS Profit
FROM SaleItems si
JOIN Sales s ON s.SaleId = si.SaleId
LEFT JOIN Products p ON p.ProductId = si.ProductId
LEFT JOIN Categories pc ON pc.CategoryId = p.CategoryId
LEFT JOIN ConsignmentItems ci ON ci.ConsignItemId = si.ConsignItemId
LEFT JOIN Consignments c ON c.ConsignId = ci.ConsignId
LEFT JOIN (
    SELECT
        ci2.ProductId,
        COALESCE(ci2.BranchId, c2.BranchId) AS BranchId,
        MIN(ci2.ConsignId) AS ConsignId,
        MIN(c2.SupplierId) AS SupplierId,
        MIN(ci2.UnitId) AS UnitId,
        MIN(ci2.UnitPrice) AS UnitPrice,
        MIN(c2.OrderDate) AS FirstConsignDate
    FROM ConsignmentItems ci2
    JOIN Consignments c2 ON c2.ConsignId = ci2.ConsignId
    GROUP BY ci2.ProductId, COALESCE(ci2.BranchId, c2.BranchId)
) cfp ON cfp.ProductId = si.ProductId AND cfp.BranchId = s.BranchId
LEFT JOIN Suppliers sup ON sup.SupplierId = c.SupplierId
LEFT JOIN Suppliers fsup ON fsup.SupplierId = cfp.SupplierId
LEFT JOIN Branches b ON b.BranchId = s.BranchId
LEFT JOIN Units su ON su.UnitId = si.UnitId
LEFT JOIN Units cu ON cu.UnitId = ci.UnitId
LEFT JOIN Units fcu ON fcu.UnitId = cfp.UnitId
LEFT JOIN Units bu ON bu.UnitId = p.UnitId
LEFT JOIN UnitConversions uc ON uc.ProductId = si.ProductId AND uc.FromUnitId = COALESCE(si.UnitId, ci.UnitId, cfp.UnitId)
WHERE s.SaleDate BETWEEN @start AND @end
  AND (
      IFNULL(si.IsConsignment, 0) = 1
      OR si.ConsignItemId IS NOT NULL
      OR LOWER(TRIM(IFNULL(pc.CategoryName, ''))) = 'consignment'
      OR (
          cfp.ProductId IS NOT NULL
          AND cfp.FirstConsignDate <= s.SaleDate
          AND NOT EXISTS (
              SELECT 1
              FROM PurchaseOrderItems poi2
              JOIN PurchaseOrders po2 ON po2.POId = poi2.POId
              WHERE poi2.ProductId = si.ProductId
                AND po2.BranchId = s.BranchId
                AND IFNULL(poi2.Quantity, 0) > 0
          )
      )
  )
  AND (@branchId IS NULL OR s.BranchId = @branchId)
ORDER BY s.SaleDate DESC, si.SaleItemId DESC";

        const string settlementsSql = @"
SELECT
    cs.SettlementId,
    cs.SettlementDate,
    sup.SupplierName,
    b.BranchName,
    cs.TotalAmount,
    cs.PaymentMethod,
    cs.ReferenceNo
FROM ConsignmentSettlements cs
LEFT JOIN Suppliers sup ON sup.SupplierId = cs.SupplierId
LEFT JOIN Branches b ON b.BranchId = cs.BranchId
WHERE cs.SettlementDate BETWEEN @start AND @end
  AND (@branchId IS NULL OR cs.BranchId = @branchId)
ORDER BY cs.SettlementDate DESC, cs.SettlementId DESC";

        const string inventorySql = @"
SELECT
    ci.ConsignItemId,
    ci.ConsignId,
    c.OrderDate,
    sup.SupplierName,
    b.BranchName,
    p.Name AS ProductName,
    p.Sku,
    u.UnitName,
    IFNULL(ci.Quantity, 0) AS Quantity,
    IFNULL(ci.SoldQuantity, 0) AS SoldQuantity,
    IFNULL(ci.ReturnedQuantity, 0) AS ReturnedQuantity,
    GREATEST(0, IFNULL(ci.Quantity, 0) - IFNULL(ci.SoldQuantity, 0) - IFNULL(ci.ReturnedQuantity, 0)) AS AvailableQuantity,
    IFNULL(ci.UnitPrice, 0) AS UnitCost,
    GREATEST(0, IFNULL(ci.Quantity, 0) - IFNULL(ci.SoldQuantity, 0) - IFNULL(ci.ReturnedQuantity, 0)) * IFNULL(ci.UnitPrice, 0) AS AvailableValue
FROM ConsignmentItems ci
JOIN Consignments c ON c.ConsignId = ci.ConsignId
LEFT JOIN Suppliers sup ON sup.SupplierId = c.SupplierId
LEFT JOIN Branches b ON b.BranchId = COALESCE(ci.BranchId, c.BranchId)
LEFT JOIN Products p ON p.ProductId = ci.ProductId
LEFT JOIN Units u ON u.UnitId = p.UnitId
WHERE c.Status = 'Active'
  AND (@branchId IS NULL OR COALESCE(ci.BranchId, c.BranchId) = @branchId)
  AND GREATEST(0, IFNULL(ci.Quantity, 0) - IFNULL(ci.SoldQuantity, 0) - IFNULL(ci.ReturnedQuantity, 0)) > 0
ORDER BY c.OrderDate DESC, ci.ConsignItemId DESC";

        var parameters = new { start, end, branchId = scopedBranchId };
        var summary = await conn.QueryFirstOrDefaultAsync<dynamic>(summarySql, parameters);
        var inventoryItems = (await conn.QueryAsync<dynamic>(inventorySql, parameters)).ToList();
        var currentAvailableValue = inventoryItems.Sum(x => (decimal?)x.AvailableValue ?? 0m);

        return Ok(new
        {
            Summary = summary,
            Items = (await conn.QueryAsync<dynamic>(itemsSql, parameters)).ToList(),
            Settlements = (await conn.QueryAsync<dynamic>(settlementsSql, parameters)).ToList(),
            InventoryItems = inventoryItems,
            CurrentAvailableValue = currentAvailableValue
        });
    }

    [HttpGet("profit-report")]
    public async Task<IActionResult> GetProfitReport([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureBundleItemsSupportsServicesAsync(conn);
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        // Keep the main profit aggregation on the last known-good query path.
        // Refund adjustment is applied afterward in C# so SaleReturns issues
        // cannot zero out the whole profit report.
        string sql = @"
    SELECT
        /* Revenue is derived from Qty*UnitPrice - Discount, not SaleItems.SubTotal (which may be NULL for legacy rows). */
        IFNULL(SUM(CASE 
            WHEN COALESCE(si.ServiceId, 0) > 0 
                 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN
                CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0) END
            ELSE 0
        END), 0) as ServiceRevenue,
        IFNULL(SUM(CASE 
            WHEN COALESCE(si.ServiceId, 0) > 0 
                 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN 0
            ELSE CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0) END
        END), 0) as ItemSoldValue,
        IFNULL(SUM(CASE 
            WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
            ELSE (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0)
        END), 0) as SalesTotal,
        /* Only product and bundle rows contribute to COGS. */
        IFNULL(SUM(CASE 
            WHEN COALESCE(si.ServiceId, 0) > 0 
                 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN 0
            WHEN COALESCE(si.BundleId, 0) > 0 
                 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%BUNDLE%' THEN
                si.Quantity * IFNULL((
                    SELECT SUM((bi.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(p2.CostPrice, 0))
                    FROM BundleItems bi
                    JOIN Products p2 ON bi.ComponentProductId = p2.ProductId
                    LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                    WHERE bi.BundleId = si.BundleId
                      AND bi.ComponentProductId IS NOT NULL
                      AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'
                ), 0)
            ELSE
                CASE
                    WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
                        (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, p.CostPrice, 0)
                    WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
                        IFNULL(si.Quantity, 0) * si.CostPrice
                    ELSE
                        (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(p.CostPrice, 0)
                END
        END), 0) as TotalCost, 
        /* Gross Profit = Sales - COGS */
        IFNULL(SUM(CASE 
            WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
            ELSE (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0)
        END), 0) - IFNULL(SUM(CASE 
            WHEN COALESCE(si.ServiceId, 0) > 0 
                 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN 0
            WHEN COALESCE(si.BundleId, 0) > 0 
                 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%BUNDLE%' THEN
                si.Quantity * IFNULL((
                    SELECT SUM((bi.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(p2.CostPrice, 0))
                    FROM BundleItems bi
                    JOIN Products p2 ON bi.ComponentProductId = p2.ProductId
                    LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                    WHERE bi.BundleId = si.BundleId
                      AND bi.ComponentProductId IS NOT NULL
                      AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'
                ), 0)
            ELSE
                CASE
                    WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
                        (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, p.CostPrice, 0)
                    WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
                        IFNULL(si.Quantity, 0) * si.CostPrice
                    ELSE
                        (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(p.CostPrice, 0)
                END
        END), 0) as GrossProfit,
        
        /* Voucher level discounts */
        IFNULL((SELECT SUM(DiscountAmount) FROM Sales 
                WHERE SaleDate BETWEEN @start AND @end 
                AND (@branchId IS NULL OR BranchId = @branchId)), 0) as TotalDiscounts,
        
        /* Delivery Fees as additive revenue */
        IFNULL((SELECT SUM(DeliveryFee) FROM Sales 
                WHERE SaleDate BETWEEN @start AND @end 
                AND (@branchId IS NULL OR BranchId = @branchId)), 0) as TotalDeliveryFees,
        
        /* Total Expenses */
        IFNULL((SELECT SUM(Amount) FROM Expenses 
                WHERE ExpenseDate BETWEEN @start AND @end 
                AND (@branchId IS NULL OR BranchId = @branchId)), 0) as TotalExpenses
        ,
        /* Fallback used when sale items were removed by a full return. */
        IFNULL((SELECT SUM(COALESCE(NULLIF(TotalAmount, 0), PaidAmount + DueAmount))
                FROM Sales
                WHERE SaleDate BETWEEN @start AND @end
                AND (@branchId IS NULL OR BranchId = @branchId)), 0) as SalesFallbackTotal
    FROM SaleItems si
    JOIN Sales s ON si.SaleId = s.SaleId
    LEFT JOIN Products p ON si.ProductId = p.ProductId
    LEFT JOIN ConsignmentItems ci ON ci.ConsignItemId = si.ConsignItemId
    LEFT JOIN UnitConversions suc ON suc.ProductId = si.ProductId AND suc.FromUnitId = si.UnitId
    WHERE s.SaleDate BETWEEN @start AND @end 
    AND (@branchId IS NULL OR s.BranchId = @branchId)";

        var data = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId });
        if (data is null)
        {
            return Ok(new
            {
                NetProfit = 0m,
                SalesTotal = 0m,
                ItemSoldValue = 0m,
                ServiceRevenue = 0m,
                TotalCost = 0m,
                TotalDiscounts = 0m,
                GrossProfit = 0m,
                FinalNetProfit = 0m,
                TotalExpenses = 0m
            });
        }

        var row = (IDictionary<string, object>)data;
        decimal GetAmount(string key)
        {
            return row.TryGetValue(key, out var value) && value is not null
                ? Convert.ToDecimal(value)
                : 0m;
        }

        var salesTotal = GetAmount("SalesTotal");
        var itemSoldValue = GetAmount("ItemSoldValue");
        var serviceRevenue = GetAmount("ServiceRevenue");
        var totalCost = GetAmount("TotalCost");
        var totalDiscounts = GetAmount("TotalDiscounts");
        var grossProfit = GetAmount("GrossProfit");
        var totalExpenses = GetAmount("TotalExpenses");
        var totalDeliveryFees = GetAmount("TotalDeliveryFees");
        var salesFallbackTotal = GetAmount("SalesFallbackTotal");
        decimal consignmentRevenue = 0m;
        decimal consignmentCost = 0m;
        decimal consignmentProfit = 0m;
        decimal consignmentPayoutDue = 0m;
        decimal returnedSalesAmount = 0m;
        decimal returnedItemRevenue = 0m;
        decimal returnedServiceRevenue = 0m;
        decimal returnedCostAmount = 0m;

        try
        {
            const string consignmentSql = @"
                SELECT
                    IFNULL(SUM(CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE (si.Quantity * si.UnitPrice) - IFNULL(si.DiscountAmount, 0) END), 0) AS Revenue,
                    IFNULL(SUM(CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE (si.Quantity * IFNULL(suc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, 0) END), 0) AS Cost,
                    IFNULL(SUM(CASE WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0 ELSE ((si.Quantity * si.UnitPrice) - IFNULL(si.DiscountAmount, 0)) - ((si.Quantity * IFNULL(suc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, 0)) END), 0) AS Profit,
                    IFNULL((SELECT SUM((ci2.SoldQuantity - ci2.SettledQuantity) * ci2.UnitPrice)
                            FROM ConsignmentItems ci2
                            JOIN Consignments c2 ON c2.ConsignId = ci2.ConsignId
                            WHERE (@branchId IS NULL OR COALESCE(ci2.BranchId, c2.BranchId) = @branchId)), 0) AS PayoutDue
                FROM SaleItems si
                JOIN Sales s ON s.SaleId = si.SaleId
                LEFT JOIN ConsignmentItems ci ON ci.ConsignItemId = si.ConsignItemId
                LEFT JOIN UnitConversions suc ON suc.ProductId = si.ProductId AND suc.FromUnitId = si.UnitId
                WHERE s.SaleDate BETWEEN @start AND @end
                  AND IFNULL(si.IsConsignment, 0) = 1
                  AND (@branchId IS NULL OR s.BranchId = @branchId)";

            var cons = await conn.QueryFirstOrDefaultAsync<dynamic>(consignmentSql, new { start, end, branchId = scopedBranchId });
            if (cons != null)
            {
                var cr = (IDictionary<string, object>)cons;
                decimal GetCons(string key) => cr.TryGetValue(key, out var v) && v is not null ? Convert.ToDecimal(v) : 0m;
                consignmentRevenue = GetCons("Revenue");
                consignmentCost = GetCons("Cost");
                consignmentProfit = GetCons("Profit");
                consignmentPayoutDue = GetCons("PayoutDue");
            }
        }
        catch
        {
            consignmentRevenue = 0m;
            consignmentCost = 0m;
            consignmentProfit = 0m;
            consignmentPayoutDue = 0m;
        }

        try
        {
            const string returnsSql = @"
                SELECT
                    IFNULL(SUM(sr.RefundAmount), 0) AS TotalReturnedRevenue,
                    IFNULL(SUM(CASE WHEN UPPER(TRIM(IFNULL(sr.ItemType, 'Product'))) LIKE '%SERVICE%' THEN sr.RefundAmount ELSE 0 END), 0) AS ReturnedServiceRevenue,
                    IFNULL(SUM(CASE WHEN UPPER(TRIM(IFNULL(sr.ItemType, 'Product'))) LIKE '%SERVICE%' THEN 0 ELSE sr.RefundAmount END), 0) AS ReturnedItemRevenue,
                    IFNULL(SUM(CASE
                        WHEN UPPER(TRIM(IFNULL(sr.ItemType, 'Product'))) LIKE '%SERVICE%' THEN 0
                        WHEN UPPER(TRIM(IFNULL(sr.ItemType, 'Product'))) LIKE '%BUNDLE%' THEN
                            sr.Quantity * IFNULL((
                                SELECT SUM((bi.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(p2.CostPrice, 0))
                                FROM BundleItems bi
                                JOIN Products p2 ON bi.ComponentProductId = p2.ProductId
                                LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                                WHERE bi.BundleId = sr.BundleId
                                  AND bi.ComponentProductId IS NOT NULL
                                  AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'
                            ), 0)
                        ELSE
                            /* Prefer the cost captured on the original sale line; fallback to current product cost. */
                            sr.Quantity * COALESCE(
                                NULLIF((
                                    SELECT si.CostPrice
                                    FROM SaleItems si
                                    WHERE si.SaleId = sr.SaleId
                                      AND si.ProductId = sr.ProductId
                                      AND (sr.UnitId IS NULL OR si.UnitId = sr.UnitId)
                                    ORDER BY si.SaleItemId DESC
                                    LIMIT 1
                                ), 0),
                                (SELECT p.CostPrice FROM Products p WHERE p.ProductId = sr.ProductId),
                                0
                            )
                    END), 0) AS ReturnedCost
                FROM SaleReturns sr
                INNER JOIN Sales rs ON rs.SaleId = sr.SaleId
                WHERE rs.SaleDate BETWEEN @start AND @end
                  AND (@branchId IS NULL OR rs.BranchId = @branchId)";

            var ret = await conn.QueryFirstOrDefaultAsync<dynamic>(returnsSql, new { start, end, branchId = scopedBranchId });
            if (ret != null)
            {
                var rr = (IDictionary<string, object>)ret;
                decimal GetRet(string key) => rr.TryGetValue(key, out var v) && v is not null ? Convert.ToDecimal(v) : 0m;
                returnedSalesAmount = GetRet("TotalReturnedRevenue");
                returnedServiceRevenue = GetRet("ReturnedServiceRevenue");
                returnedItemRevenue = GetRet("ReturnedItemRevenue");
                returnedCostAmount = GetRet("ReturnedCost");
            }
        }
        catch
        {
            returnedSalesAmount = 0m;
            returnedItemRevenue = 0m;
            returnedServiceRevenue = 0m;
            returnedCostAmount = 0m;
        }

        var netSalesTotal = Math.Max(0m, salesFallbackTotal - returnedSalesAmount);
        var netServiceRevenue = serviceRevenue - returnedServiceRevenue;
        var netItemSoldValue = Math.Max(0m, netSalesTotal - netServiceRevenue);
        // SaleItems-based COGS is already net of returns because returned lines are reduced
        // or removed from SaleItems during the return flows. Subtracting ReturnedCost again
        // double-counts the deduction and can make COGS go negative.
        var netTotalCost = totalCost;
        var netGrossProfit = netSalesTotal - netTotalCost;

        return Ok(new
        {
            NetProfit = netGrossProfit,
            SalesTotal = netSalesTotal,
            ItemSoldValue = netItemSoldValue,
            ServiceRevenue = netServiceRevenue,
            GrossSalesTotal = salesFallbackTotal,
            GrossItemSoldValue = itemSoldValue,
            GrossServiceRevenue = serviceRevenue,
            ReturnedRevenue = returnedSalesAmount,
            ReturnedItemRevenue = returnedItemRevenue,
            ReturnedServiceRevenue = returnedServiceRevenue,
            TotalCost = totalCost,
            ReturnedCost = returnedCostAmount,
            NetCost = netTotalCost,
            ConsignmentRevenue = consignmentRevenue,
            ConsignmentCost = consignmentCost,
            ConsignmentProfit = consignmentProfit,
            ConsignmentPayoutDue = consignmentPayoutDue,
            TotalDiscounts = totalDiscounts,
            GrossProfit = netGrossProfit,
            FinalNetProfit = netGrossProfit + totalDeliveryFees - totalDiscounts - totalExpenses,
            TotalExpenses = totalExpenses
        });
    }
    // ၄။ Inventory Reports: လက်ရှိ Stock တန်ဖိုး တွက်ချက်ခြင်း
    [HttpGet("profit-item-details")]
    public async Task<IActionResult> GetProfitItemDetails([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureBundleItemsSupportsServicesAsync(conn);
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        const string sql = @"
            SELECT
                s.SaleId,
                CONCAT('SA', LPAD(s.SaleId, 7, '0')) AS VoucherNo,
                s.SaleDate,
                si.SaleItemId,
                si.ProductId,
                COALESCE(p.Name, CONCAT('Product #', si.ProductId)) AS ItemName,
                CASE
                    WHEN COALESCE(si.ServiceId, 0) > 0 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN 'Service'
                    WHEN COALESCE(si.BundleId, 0) > 0 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%BUNDLE%' THEN 'Bundle'
                    ELSE 'Product'
                END AS ItemTypeLabel,
                IFNULL(si.Quantity, 0) AS Quantity,
                COALESCE(su.UnitName, '') AS UnitName,
                IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1) AS BaseQuantity,
                COALESCE(bu.UnitName, '') AS BaseUnitName,
                IFNULL(si.UnitPrice, 0) AS UnitPrice,
                IFNULL(si.DiscountAmount, 0) AS DiscountAmount,
                IFNULL(si.IsFOC, 0) AS IsFOC,
                CASE
                    WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
                    ELSE (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0)
                END AS Revenue,
                CASE
                    WHEN COALESCE(si.ServiceId, 0) > 0 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN 0
                    WHEN COALESCE(si.BundleId, 0) > 0 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%BUNDLE%' THEN
                        IFNULL(si.Quantity, 0) * IFNULL((
                            SELECT SUM((bi.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(p2.CostPrice, 0))
                            FROM BundleItems bi
                            JOIN Products p2 ON bi.ComponentProductId = p2.ProductId
                            LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                            WHERE bi.BundleId = si.BundleId
                              AND bi.ComponentProductId IS NOT NULL
                              AND UPPER(TRIM(IFNULL(bi.ItemType, 'Product'))) NOT LIKE '%SERVICE%'
                        ), 0)
                    ELSE
                        CASE
                            WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
                                (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, p.CostPrice, 0)
                            WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
                                IFNULL(si.Quantity, 0) * si.CostPrice
                            ELSE
                                (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(p.CostPrice, 0)
                        END
                END AS COGS,
                CASE
                    WHEN COALESCE(si.ServiceId, 0) > 0 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN 'Service item'
                    WHEN COALESCE(si.BundleId, 0) > 0 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%BUNDLE%' THEN 'Bundle component costs'
                    WHEN (IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL) AND NULLIF(si.CostPrice, 0) IS NOT NULL AND IFNULL(suc.Multiplier, 1) <> 1 THEN 'Consignment base cost x unit conversion'
                    WHEN (IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL) AND NULLIF(si.CostPrice, 0) IS NOT NULL THEN 'Consignment base cost'
                    WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN 'Sale line cost price'
                    WHEN IFNULL(suc.Multiplier, 1) <> 1 THEN 'Product master cost price x unit conversion'
                    ELSE 'Product master cost price'
                END AS CostSource,
                CASE
                    WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
                    ELSE (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0)
                END -
                CASE
                    WHEN COALESCE(si.ServiceId, 0) > 0 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN 0
                    WHEN COALESCE(si.BundleId, 0) > 0 OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%BUNDLE%' THEN
                        IFNULL(si.Quantity, 0) * IFNULL((
                            SELECT SUM((bi.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(p2.CostPrice, 0))
                            FROM BundleItems bi
                            JOIN Products p2 ON bi.ComponentProductId = p2.ProductId
                            LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                            WHERE bi.BundleId = si.BundleId
                              AND bi.ComponentProductId IS NOT NULL
                              AND UPPER(TRIM(IFNULL(bi.ItemType, 'Product'))) NOT LIKE '%SERVICE%'
                        ), 0)
                    ELSE
                        CASE
                            WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
                                (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, p.CostPrice, 0)
                            WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
                                IFNULL(si.Quantity, 0) * si.CostPrice
                            ELSE
                                (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(p.CostPrice, 0)
                        END
                END AS ItemProfit
            FROM SaleItems si
            INNER JOIN Sales s ON si.SaleId = s.SaleId
            LEFT JOIN Products p ON si.ProductId = p.ProductId
            LEFT JOIN ConsignmentItems ci ON ci.ConsignItemId = si.ConsignItemId
            LEFT JOIN Units su ON su.UnitId = si.UnitId
            LEFT JOIN Units bu ON bu.UnitId = p.UnitId
            LEFT JOIN UnitConversions suc ON suc.ProductId = si.ProductId AND suc.FromUnitId = si.UnitId
            WHERE s.SaleDate BETWEEN @start AND @end
              AND (@branchId IS NULL OR s.BranchId = @branchId)
            ORDER BY s.SaleDate DESC, si.SaleItemId DESC";

        var items = await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId });
        return Ok(items);
    }

    [HttpGet("inventory-value")]
    public async Task<IActionResult> GetInventoryValue([FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var scopedBranchId = ResolveBranchScope(branchId);

        // SQL မှာတင် IFNULL သုံးပြီး TotalValue က NULL မဖြစ်အောင် ကာကွယ်ပါ
        string sql = @"
         SELECT 
             p.ProductId,
             p.Name,
             p.SKU,
             ps.Quantity as StockQty,
             p.CostPrice as UnitCost,
             IFNULL(ps.Quantity * p.CostPrice, 0) as TotalValue
         FROM Products p
         JOIN ProductStocks ps ON p.ProductId = ps.ProductId
         WHERE ps.Quantity > 0
         AND (@branchId IS NULL OR ps.BranchId = @branchId)
         ORDER BY TotalValue DESC";

        var inventory = await conn.QueryAsync<dynamic>(sql, new { branchId = scopedBranchId });

        return Ok(new
        {
            Items = inventory,
            TotalStockQty = inventory.Sum(x => (decimal?)x.StockQty ?? 0m),
            // decimal? (nullable) ကိုသုံးပြီး null ဖြစ်ခဲ့ရင် 0m လို့ သတ်မှတ်ပါ
            TotalStockValue = inventory.Sum(x => (decimal?)x.TotalValue ?? 0m)
        });
    }

    // မူလရှိပြီးသား Branch Comparison (Discount နှင့် Profit Logic အသစ်ဖြင့်)
    [HttpGet("branch-comparison")]
    public async Task<IActionResult> GetBranchComparison([FromQuery] DateTime fromDate, [FromQuery] DateTime toDate, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureBundleItemsSupportsServicesAsync(conn);
        var start = fromDate.Date;
        var end = toDate.Date.AddDays(1).AddTicks(-1);
        var scopedBranchId = ResolveBranchScope(branchId);

        // Use the same net revenue / net COGS logic as GetProfitReport,
        // including the sales-master fallback for vouchers whose items were removed by a full return.
        // This keeps branch profit aligned with the main profit widget for legacy data.
        string sql = @"
        SELECT
            b.BranchName,
            COUNT(DISTINCT s.SaleId) AS TotalInvoices,
            /* Net revenue = sales-master fallback total - returned revenue. */
            (
                IFNULL(sf.SalesFallbackTotal, 0)
                - IFNULL(r.ReturnedRevenue, 0)
            ) AS TotalRevenue,
            /* Net profit = net revenue - current COGS + delivery - vouchers - expenses. */
            (
                (
                    IFNULL(sf.SalesFallbackTotal, 0)
                    - IFNULL(r.ReturnedRevenue, 0)
                )
                -
                IFNULL(SUM(
                    CASE
                        WHEN COALESCE(si.ServiceId, 0) > 0
                             OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%SERVICE%' THEN 0
                        WHEN COALESCE(si.BundleId, 0) > 0
                             OR UPPER(TRIM(IFNULL(si.ItemType, ''))) LIKE '%BUNDLE%' THEN
                            IFNULL(si.Quantity, 0) * IFNULL((
                                SELECT SUM((bi.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(p2.CostPrice, 0))
                                FROM BundleItems bi
                                JOIN Products p2 ON bi.ComponentProductId = p2.ProductId
                                LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                                WHERE bi.BundleId = si.BundleId
                                  AND bi.ComponentProductId IS NOT NULL
                                  AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'
                            ), 0)
                        ELSE
                            CASE
                                WHEN IFNULL(si.IsConsignment, 0) = 1 OR si.ConsignItemId IS NOT NULL THEN
                                    (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(NULLIF(si.CostPrice, 0), ci.UnitPrice, p.CostPrice, 0)
                                WHEN NULLIF(si.CostPrice, 0) IS NOT NULL THEN
                                    IFNULL(si.Quantity, 0) * si.CostPrice
                                ELSE
                                    (IFNULL(si.Quantity, 0) * IFNULL(suc.Multiplier, 1)) * COALESCE(p.CostPrice, 0)
                            END
                    END
                ), 0)
                + IFNULL(sf.DeliveryFees, 0)
                - IFNULL(sf.VoucherDiscounts, 0)
                - IFNULL(e.TotalExpenses, 0)
            ) AS TotalNetProfit
        FROM Branches b
        LEFT JOIN Sales s
            ON b.BranchId = s.BranchId
           AND s.SaleDate BETWEEN @start AND @end
        LEFT JOIN SaleItems si ON s.SaleId = si.SaleId
        LEFT JOIN Products p ON si.ProductId = p.ProductId
        LEFT JOIN ConsignmentItems ci ON ci.ConsignItemId = si.ConsignItemId
        LEFT JOIN UnitConversions suc ON suc.ProductId = si.ProductId AND suc.FromUnitId = si.UnitId
        /* Sales-level fallback totals for vouchers whose item rows were removed by a full return. */
        LEFT JOIN (
            SELECT
                BranchId,
                IFNULL(SUM(COALESCE(NULLIF(TotalAmount, 0), PaidAmount + DueAmount)), 0) AS SalesFallbackTotal,
                IFNULL(SUM(DeliveryFee), 0) AS DeliveryFees,
                IFNULL(SUM(DiscountAmount), 0) AS VoucherDiscounts
            FROM Sales
            WHERE SaleDate BETWEEN @start AND @end
            GROUP BY BranchId
        ) sf ON sf.BranchId = b.BranchId
        /* Returns are aggregated per branch on the same SaleDate window (matches GetProfitReport). */
        LEFT JOIN (
            SELECT
                rs.BranchId,
                IFNULL(SUM(sr.RefundAmount), 0) AS ReturnedRevenue,
                IFNULL(SUM(CASE
                    WHEN UPPER(TRIM(IFNULL(sr.ItemType, 'Product'))) LIKE '%SERVICE%' THEN 0
                    WHEN UPPER(TRIM(IFNULL(sr.ItemType, 'Product'))) LIKE '%BUNDLE%' THEN
                        IFNULL(sr.Quantity, 0) * IFNULL((
                            SELECT SUM((bi.Quantity * IFNULL(uc.Multiplier, 1)) * COALESCE(p2.CostPrice, 0))
                            FROM BundleItems bi
                            JOIN Products p2 ON bi.ComponentProductId = p2.ProductId
                            LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
                            WHERE bi.BundleId = sr.BundleId
                              AND bi.ComponentProductId IS NOT NULL
                              AND UPPER(TRIM(IFNULL(bi.ItemType,'Product'))) NOT LIKE '%SERVICE%'
                        ), 0)
                    ELSE
                        /* Prefer the cost captured on the original sale line; fallback to current product cost. */
                        IFNULL(sr.Quantity, 0) * COALESCE(
                            NULLIF((
                                SELECT si2.CostPrice
                                FROM SaleItems si2
                                WHERE si2.SaleId = sr.SaleId
                                  AND si2.ProductId = sr.ProductId
                                  AND (sr.UnitId IS NULL OR si2.UnitId = sr.UnitId)
                                ORDER BY si2.SaleItemId DESC
                                LIMIT 1
                            ), 0),
                            (SELECT p3.CostPrice FROM Products p3 WHERE p3.ProductId = sr.ProductId),
                            0
                        )
                END), 0) AS ReturnedCost
            FROM SaleReturns sr
            INNER JOIN Sales rs ON rs.SaleId = sr.SaleId
            WHERE rs.SaleDate BETWEEN @start AND @end
            GROUP BY rs.BranchId
        ) r ON r.BranchId = b.BranchId
        LEFT JOIN (
            SELECT
                BranchId,
                IFNULL(SUM(Amount), 0) AS TotalExpenses
            FROM Expenses
            WHERE ExpenseDate BETWEEN @start AND @end
            GROUP BY BranchId
        ) e ON e.BranchId = b.BranchId
        WHERE b.IsActive = 1
          AND (@branchId IS NULL OR b.BranchId = @branchId)
        GROUP BY b.BranchId, b.BranchName, sf.SalesFallbackTotal, r.ReturnedRevenue, r.ReturnedCost, sf.DeliveryFees, sf.VoucherDiscounts, e.TotalExpenses
        ORDER BY TotalRevenue DESC";

        var comparison = await conn.QueryAsync<dynamic>(sql, new { start, end, branchId = scopedBranchId });
        return Ok(comparison);
    }

    [HttpGet("expense-analytics")]
    public async Task<IActionResult> GetExpenseAnalytics([FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var scopedBranchId = ResolveBranchScope(branchId);

        // ၁။ Category အလိုက် စုစုပေါင်း (Pie Chart visual အတွက်)
        string categorySql = @"
        SELECT Category, SUM(Amount) as Total 
        FROM Expenses 
        WHERE (@branchId IS NULL OR BranchId = @branchId)
        GROUP BY Category 
        ORDER BY Total DESC";

        // ၂။ နောက်ဆုံး ၆ လစာ အသုံးစရိတ် Trend (Bar Chart visual အတွက်)
        string monthlySql = @"
        SELECT DATE_FORMAT(ExpenseDate, '%Y-%m') as Month, SUM(Amount) as Total 
        FROM Expenses 
        WHERE (@branchId IS NULL OR BranchId = @branchId)
        AND ExpenseDate >= DATE_SUB(NOW(), INTERVAL 6 MONTH)
        GROUP BY Month 
        ORDER BY Month ASC";

        var categories = (await conn.QueryAsync<dynamic>(categorySql, new { branchId = scopedBranchId })).ToList();
        var monthly = (await conn.QueryAsync<dynamic>(monthlySql, new { branchId = scopedBranchId })).ToList();

        return Ok(new { categories, monthly });
    }
}
