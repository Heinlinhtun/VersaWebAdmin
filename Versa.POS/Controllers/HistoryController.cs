using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;
using Versa.POS.Controllers;

[Authorize]
[Route("api/[controller]")]
public class HistoryController : BaseShopController
{
    private readonly DatabaseService _dbService;
    public HistoryController(DatabaseService dbService) { _dbService = dbService; }

    private string? CleanDate(string? date) => string.IsNullOrWhiteSpace(date) ? null : date;
    private string CleanText(string? value) => string.IsNullOrWhiteSpace(value) ? "" : value.Trim();

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
            await conn.ExecuteAsync("ALTER TABLE PurchaseOrderItems ADD COLUMN RemainingQuantity DECIMAL(18,4) NULL");
        }

        await conn.ExecuteAsync(@"
UPDATE PurchaseOrderItems poi
LEFT JOIN UnitConversions uc
  ON uc.ProductId = poi.ProductId
 AND uc.FromUnitId = poi.UnitId
SET poi.RemainingQuantity = (IFNULL(poi.Quantity, 0) + IFNULL(poi.FOCQuantity, 0)) * IFNULL(uc.Multiplier, 1)
WHERE poi.RemainingQuantity IS NULL");
    }

    private string BuildDateFilter(string dateField = "s.SaleDate")
    {
        // Returns SQL fragment for date range filtering
        return @"AND (@StartDate IS NULL OR DATE(" + dateField + @") >= @StartDate)
    AND (@EndDate IS NULL OR DATE(" + dateField + @") <= @EndDate)
    AND (@Date IS NULL OR DATE(" + dateField + @") = @Date)";
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

    [HttpGet("sale-history")]
    public async Task<IActionResult> GetSaleHistory([FromQuery] HistoryFilter filter)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.OpenAsync();
        await conn.ExecuteAsync("SET time_zone = '+06:30'");
        await EnsureSalesOrderStatusColumnAsync(conn);

        int? bId = User.IsInRole("Admin") ? filter.BranchId : int.Parse(User.FindFirst("BranchId").Value);

        string dateFilter = BuildDateFilter("s.SaleDate");

        // 💡 SQL Update: Item တွေရဲ့ အနှစ်ချုပ်ကိုပါ တစ်ခါတည်း ယူပါမည်
        string sql = $@"
    -- ၁။ အရောင်းမှတ်တမ်းစာရင်း (Items Summary ပါဝင်သည်)
    SELECT 
        s.SaleId as saleId, 
        s.SaleDate as saleDate, 
        IFNULL(sit.ItemSubtotal, 0) as itemSubtotal,
        IFNULL(s.DiscountAmount, 0) as discountAmount,
        IFNULL(s.DeliveryFee, 0) as deliveryFee,
        GREATEST(0, IFNULL(sit.ItemSubtotal, 0) - IFNULL(s.DiscountAmount, 0) + IFNULL(s.DeliveryFee, 0)) as totalAmount,
        GREATEST(0, IFNULL(sit.ItemSubtotal, 0) - IFNULL(s.DiscountAmount, 0) + IFNULL(s.DeliveryFee, 0)) as netAmount,
        c.Name as customerName,
        CASE
            WHEN IFNULL(s.DueAmount, 0) > 0 AND IFNULL(s.PaidAmount, 0) > 0 THEN 'Partial'
            WHEN IFNULL(s.DueAmount, 0) > 0 THEN 'Credit'
            ELSE 'Normal'
        END as saleType,
        s.VoucherNote as voucherNote,
        IFNULL(s.OrderStatus, 'Completed') as orderStatus,
        COALESCE(u.Username, 'System') as username,
        COALESCE(u.Username, 'System') as cashierName,
        (SELECT GROUP_CONCAT(
            CASE 
                WHEN si.ItemType = 'Product' THEN p.Name
                WHEN si.ItemType = 'Service' THEN sv.ServiceName
                WHEN si.ItemType = 'Bundle' THEN b.BundleName
                ELSE 'Unknown'
            END
         SEPARATOR ', ') 
         FROM SaleItems si 
         LEFT JOIN Products p ON si.ProductId = p.ProductId
         LEFT JOIN Services sv ON si.ServiceId = sv.ServiceId
         LEFT JOIN Bundles b ON si.BundleId = b.BundleId
         WHERE si.SaleId = s.SaleId) as itemsSummary
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
    LEFT JOIN Customers c ON s.CustomerId = c.CustomerId
    LEFT JOIN ShopUsers u ON s.UserId = u.UserId
    WHERE (@BranchId IS NULL OR s.BranchId = @BranchId)            
    AND (@Query = '' OR CAST(s.SaleId AS CHAR) = @Query OR s.SaleId LIKE CONCAT('%', @Query, '%'))
    AND (@CustomerName = '' OR IFNULL(c.Name, '') LIKE CONCAT('%', @CustomerName, '%'))
    {dateFilter}
    AND (@OrderStatus = '' OR IFNULL(s.OrderStatus,'Completed') = @OrderStatus)
    ORDER BY s.SaleDate DESC LIMIT @Limit OFFSET @Offset;

    -- ၂။ Pagination အတွက် Total Count
    SELECT COUNT(*) 
    FROM Sales s 
    LEFT JOIN Customers c ON s.CustomerId = c.CustomerId
    WHERE (@BranchId IS NULL OR s.BranchId = @BranchId)
    AND (@Query = '' OR CAST(s.SaleId AS CHAR) = @Query OR s.SaleId LIKE CONCAT('%', @Query, '%'))
    AND (@CustomerName = '' OR IFNULL(c.Name, '') LIKE CONCAT('%', @CustomerName, '%'))
    {dateFilter}
    AND (@OrderStatus = '' OR IFNULL(s.OrderStatus,'Completed') = @OrderStatus);";

        var multi = await conn.QueryMultipleAsync(sql, new
        {
            BranchId = bId,
            Query = filter.Query ?? "",
            CustomerName = CleanText(filter.CustomerName),
            Date = CleanDate(filter.Date),
            StartDate = CleanDate(filter.StartDate),
            EndDate = CleanDate(filter.EndDate),
            OrderStatus = CleanText(filter.OrderStatus),
            Limit = filter.Limit,
            Offset = (filter.Page - 1) * filter.Limit
        });

        return Ok(new
        {
            list = await multi.ReadAsync<dynamic>(),
            total = await multi.ReadFirstAsync<int>()
        });
    }

    [HttpGet("purchase-history")]
    public async Task<IActionResult> GetPurchaseHistory([FromQuery] HistoryFilter filter)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // Role-based Branch Filtering
        int? bId = User.IsInRole("Admin") ? filter.BranchId : int.Parse(User.FindFirst("BranchId").Value);

        string dateFilter = BuildDateFilter("p.OrderDate");

        string sql = $@"
        SELECT p.POId as poId, 
               p.OrderDate as orderDate, 
               p.TotalAmount as totalAmount, 
               p.PaidAmount as paidAmount,
               p.Status as status,
               sp.PaymentMethod as paymentMethod,
               s.SupplierName as supplierName,
               b.BranchName as branchName
        FROM PurchaseOrders p 
        LEFT JOIN Suppliers s ON p.SupplierId = s.SupplierId
        LEFT JOIN Branches b ON p.BranchId = b.BranchId
        LEFT JOIN (
            SELECT
                ReferenceNo,
                GROUP_CONCAT(DISTINCT COALESCE(NULLIF(PaymentMethod,''),'Unknown') ORDER BY PaymentMethod SEPARATOR ', ') AS PaymentMethod
            FROM SupplierPayments
            GROUP BY ReferenceNo
        ) sp ON sp.ReferenceNo = CONCAT('PU', LPAD(p.POId, 7, '0'))
        WHERE (@BranchId IS NULL OR p.BranchId = @BranchId)
        AND (@Query = '' OR CAST(p.POId AS CHAR) = @Query OR p.POId LIKE CONCAT('%', @Query, '%'))
        AND (@SupplierName = '' OR IFNULL(s.SupplierName, '') LIKE CONCAT('%', @SupplierName, '%'))
        {dateFilter}
        ORDER BY p.OrderDate DESC LIMIT @Limit OFFSET @Offset;

        -- Pagination အတွက် စစ်ထုတ်ထားသော Result အရေအတွက်ကို တွက်ပါသည်
        SELECT COUNT(*) FROM PurchaseOrders p
        LEFT JOIN Suppliers s ON p.SupplierId = s.SupplierId
        WHERE (@BranchId IS NULL OR p.BranchId = @BranchId)
        AND (@Query = '' OR CAST(p.POId AS CHAR) = @Query OR p.POId LIKE CONCAT('%', @Query, '%'))
        AND (@SupplierName = '' OR IFNULL(s.SupplierName, '') LIKE CONCAT('%', @SupplierName, '%'))
        {dateFilter};";

        var multi = await conn.QueryMultipleAsync(sql, new
        {
            BranchId = bId,
            Query = filter.Query ?? "",
            SupplierName = CleanText(filter.SupplierName),
            Date = CleanDate(filter.Date),
            StartDate = CleanDate(filter.StartDate),
            EndDate = CleanDate(filter.EndDate),
            Limit = filter.Limit,
            Offset = (filter.Page - 1) * filter.Limit
        });

        return Ok(new
        {
            list = await multi.ReadAsync<dynamic>(),
            total = await multi.ReadFirstAsync<int>()
        });
    }

    [HttpGet("sale-detail/{id}")]
    public async Task<IActionResult> GetSaleDetail(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        // ၁။ အရောင်းချုပ် (Sales + Customer) နှင့် ပစ္စည်းစာရင်း (SaleItems + Products + Units) ကို ဆွဲထုတ်ခြင်း
        string sql = @"
        -- Master Record
        SELECT s.*, c.Name as CustomerName, c.Phone as CustomerPhone, COALESCE(u.Username, 'System') as CashierName
        FROM Sales s LEFT JOIN Customers c ON s.CustomerId = c.CustomerId 
        LEFT JOIN ShopUsers u ON s.UserId = u.UserId
        WHERE s.SaleId = @id;

        -- Detail Items
        SELECT 
            si.SaleItemId,
            si.SaleId,
            si.ItemType,
            si.ProductId,
            si.ServiceId,
            si.BundleId,
            si.UnitId,
            si.Quantity,
            si.UnitPrice,
            si.DiscountAmount,
            si.IsFOC,
            si.CostPrice,
            CASE
                WHEN IFNULL(si.IsFOC, 0) = 1 THEN 0
                WHEN IFNULL(si.SubTotal, 0) > ((IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0))
                    THEN GREATEST(0, (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0))
                ELSE GREATEST(0, IFNULL(si.SubTotal, (IFNULL(si.Quantity, 0) * IFNULL(si.UnitPrice, 0)) - IFNULL(si.DiscountAmount, 0)))
            END AS SubTotal,
            si.Note,
            si.IsConsignment,
            si.ConsignItemId,
            CASE 
                WHEN si.ItemType = 'Product' THEN p.Name
                WHEN si.ItemType = 'Service' THEN sv.ServiceName
                WHEN si.ItemType = 'Bundle' THEN b.BundleName
                ELSE 'Unknown Item'
            END AS ItemName,
            u.UnitName
        FROM SaleItems si
        LEFT JOIN Products p ON si.ProductId = p.ProductId
        LEFT JOIN Services sv ON si.ServiceId = sv.ServiceId
        LEFT JOIN Bundles b ON si.BundleId = b.BundleId
        LEFT JOIN Units u ON si.UnitId = u.UnitId
        WHERE si.SaleId = @id;";

        using var multi = await conn.QueryMultipleAsync(sql, new { id });

        var sale = await multi.ReadFirstOrDefaultAsync<dynamic>();
        if (sale == null) return NotFound(new { Message = "Sale not found" });

        var items = await multi.ReadAsync<dynamic>();

        return Ok(new { sale, items });
    }

    [HttpGet("return-history")]
    public async Task<IActionResult> GetReturnHistory([FromQuery] HistoryFilter filter)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        int? bId = User.IsInRole("Admin") ? filter.BranchId : int.Parse(User.FindFirst("BranchId").Value);

        string dateFilter = BuildDateFilter("returnDate");

        string sql = $@"
        SELECT * FROM (
            SELECT 'Sale' as returnType, sr.ReturnId as returnId, sr.SaleId as saleId, NULL as poId, p.Name as productName, 
                   sr.Quantity as quantity, u.UnitName as unitName, sr.RefundAmount as refundAmount, 
                   sr.Reason as reason, sr.ReturnDate as returnDate, sr.BranchId 
            FROM SaleReturns sr 
            JOIN Products p ON sr.ProductId = p.ProductId
            LEFT JOIN Units u ON sr.UnitId = u.UnitId
            
            UNION ALL
            
            SELECT 'Purchase' as returnType, pr.ReturnId as returnId, NULL as saleId, pr.POId as poId, p.Name as productName, 
                   pr.Quantity as quantity, u.UnitName as unitName, pr.RefundAmount as refundAmount, 
                   pr.Reason as reason, pr.ReturnDate as returnDate, pr.BranchId 
            FROM PurchaseReturns pr 
            JOIN Products p ON pr.ProductId = p.ProductId
            LEFT JOIN Units u ON pr.UnitId = u.UnitId
        ) AS combined 
        WHERE (@BranchId IS NULL OR BranchId = @BranchId) 
        {dateFilter}
        ORDER BY returnDate DESC LIMIT @Limit OFFSET @Offset;";

        var list = await conn.QueryAsync<dynamic>(sql, new
        {
            BranchId = bId,
            Date = CleanDate(filter.Date),
            StartDate = CleanDate(filter.StartDate),
            EndDate = CleanDate(filter.EndDate),
            Limit = filter.Limit,
            Offset = (filter.Page - 1) * filter.Limit
        });
        return Ok(new { list, total = 0 });
    }

    [HttpGet("adjustment-history")]
    public async Task<IActionResult> GetAdjustmentHistory([FromQuery] HistoryFilter filter)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        int? bId = User.IsInRole("Admin") ? filter.BranchId : int.Parse(User.FindFirst("BranchId").Value);

        string dateFilter = BuildDateFilter("a.AdjustmentDate");

        string sql = $@"
        SELECT a.AdjustmentId as adjustmentId, a.AdjustmentDate as adjustmentDate, 
               a.Type as type, a.Quantity as quantity, u.UnitName as unitName, 
               a.Reason as reason, p.Name as productName, us.Username as username
        FROM StockAdjustments a 
        JOIN Products p ON a.ProductId = p.ProductId 
        LEFT JOIN Units u ON a.UnitId = u.UnitId
        LEFT JOIN ShopUsers us ON a.UserId = us.UserId
        WHERE (@BranchId IS NULL OR a.BranchId = @BranchId)
        {dateFilter}
        ORDER BY a.AdjustmentDate DESC LIMIT @Limit OFFSET @Offset;

        SELECT COUNT(*) FROM StockAdjustments a
        WHERE (@BranchId IS NULL OR a.BranchId = @BranchId) 
        {dateFilter};";

        var multi = await conn.QueryMultipleAsync(sql, new
        {
            BranchId = bId,
            Date = CleanDate(filter.Date),
            StartDate = CleanDate(filter.StartDate),
            EndDate = CleanDate(filter.EndDate),
            Limit = filter.Limit,
            Offset = (filter.Page - 1) * filter.Limit
        });

        return Ok(new
        {
            list = await multi.ReadAsync<dynamic>(),
            total = await multi.ReadFirstAsync<int>()
        });
    }

    [HttpGet("transfer-history")]
    public async Task<IActionResult> GetTransferHistory([FromQuery] HistoryFilter filter)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        int currentId = int.Parse(User.FindFirst("BranchId").Value);
        bool isAdmin = User.IsInRole("Admin");

        string dateFilter = BuildDateFilter("t.TransferDate");

        string sql = $@"
        SELECT t.TransferId as transferId, 
               t.TransferDate as transferDate, 
               t.Status as status, 
               t.Note as note,
               b1.BranchName as fromBranchName, 
               b2.BranchName as toBranchName,
               us.Username as username,
               (SELECT COUNT(*) FROM StockTransferItems WHERE TransferId = t.TransferId) as itemCount
        FROM StockTransfers t
        JOIN Branches b1 ON t.FromBranchId = b1.BranchId
        JOIN Branches b2 ON t.ToBranchId = b2.BranchId
        LEFT JOIN ShopUsers us ON t.UserId = us.UserId
        WHERE (@IsAdmin = 1 OR t.FromBranchId = @CurrentId OR t.ToBranchId = @CurrentId)
        AND (@BranchId IS NULL OR t.FromBranchId = @BranchId OR t.ToBranchId = @BranchId)
        {dateFilter}
        ORDER BY t.TransferDate DESC LIMIT @Limit OFFSET @Offset;

        SELECT COUNT(*) FROM StockTransfers t
        WHERE (@IsAdmin = 1 OR t.FromBranchId = @CurrentId OR t.ToBranchId = @CurrentId)
        AND (@BranchId IS NULL OR t.FromBranchId = @BranchId OR t.ToBranchId = @BranchId)
        {dateFilter};";

        var multi = await conn.QueryMultipleAsync(sql, new
        {
            IsAdmin = isAdmin,
            CurrentId = currentId,
            BranchId = filter.BranchId,
            Date = CleanDate(filter.Date),
            StartDate = CleanDate(filter.StartDate),
            EndDate = CleanDate(filter.EndDate),
            Limit = filter.Limit,
            Offset = (filter.Page - 1) * filter.Limit
        });

        return Ok(new
        {
            list = await multi.ReadAsync<dynamic>(),
            total = await multi.ReadFirstAsync<int>()
        });
    }

    [HttpGet("inventory-ledger-history")]
    public async Task<IActionResult> GetInventoryLedgerHistory([FromQuery] HistoryFilter filter)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsureSalesOrderStatusColumnAsync(conn);
        int? branchId = User.IsInRole("Admin") ? filter.BranchId : int.Parse(User.FindFirst("BranchId").Value);
        string query = CleanText(filter.Query);

        string sql = @"
SELECT
    p.ProductId AS productId,
    COALESCE(NULLIF(p.SKU, ''), CONCAT('P-', p.ProductId)) AS barcode,
    p.Name AS itemName,
    COALESCE(c.CategoryName, 'Uncategorized') AS categoryName,
    u.UnitName AS unitName,
    COALESCE(lp.LatestPurchasePrice, p.CostPrice, 0) AS latestPurchasePrice,
    COALESCE(ls.LatestSalePrice, p.SalePrice, 0) AS latestSalePrice,
    IFNULL(pur.TotalPurchased, 0) AS totalPurchased,
    IFNULL(sold.TotalSold, 0) AS totalSold,
    IFNULL(ret.TotalReturned, 0) AS totalReturned,
    IFNULL(stock.TotalStockRemains, 0) AS totalStockRemains
FROM Products p
LEFT JOIN Categories c ON p.CategoryId = c.CategoryId
LEFT JOIN Units u ON p.UnitId = u.UnitId
LEFT JOIN (
    SELECT poi.ProductId,
           SUM((IFNULL(poi.Quantity, 0) + IFNULL(poi.FOCQuantity, 0)) * IFNULL(uc.Multiplier, 1)) AS TotalPurchased
    FROM PurchaseOrderItems poi
    INNER JOIN PurchaseOrders po ON po.POId = poi.POId
    LEFT JOIN UnitConversions uc ON uc.ProductId = poi.ProductId AND uc.FromUnitId = poi.UnitId
    WHERE (@BranchId IS NULL OR po.BranchId = @BranchId)
    GROUP BY poi.ProductId
) pur ON pur.ProductId = p.ProductId
LEFT JOIN (
    SELECT ProductId, SUM(BaseQty) AS TotalSold
    FROM (
        SELECT si.ProductId,
               SUM(IFNULL(si.Quantity, 0) * IFNULL(uc.Multiplier, 1)) AS BaseQty
        FROM SaleItems si
        INNER JOIN Sales s ON s.SaleId = si.SaleId
        LEFT JOIN UnitConversions uc ON uc.ProductId = si.ProductId AND uc.FromUnitId = si.UnitId
        WHERE UPPER(TRIM(IFNULL(si.ItemType, 'Product'))) = 'PRODUCT'
          AND si.ProductId IS NOT NULL
          AND UPPER(TRIM(IFNULL(s.OrderStatus, 'Completed'))) <> 'CANCELLED'
          AND (@BranchId IS NULL OR s.BranchId = @BranchId)
        GROUP BY si.ProductId

        UNION ALL

        SELECT bi.ComponentProductId AS ProductId,
               SUM(IFNULL(si.Quantity, 0) * IFNULL(bi.Quantity, 0) * IFNULL(uc.Multiplier, 1)) AS BaseQty
        FROM SaleItems si
        INNER JOIN Sales s ON s.SaleId = si.SaleId
        INNER JOIN BundleItems bi ON bi.BundleId = si.BundleId
        LEFT JOIN UnitConversions uc ON uc.ProductId = bi.ComponentProductId AND uc.FromUnitId = bi.UnitId
        WHERE UPPER(TRIM(IFNULL(si.ItemType, ''))) = 'BUNDLE'
          AND bi.ComponentProductId IS NOT NULL
          AND UPPER(TRIM(IFNULL(bi.ItemType, 'Product'))) <> 'SERVICE'
          AND UPPER(TRIM(IFNULL(s.OrderStatus, 'Completed'))) <> 'CANCELLED'
          AND (@BranchId IS NULL OR s.BranchId = @BranchId)
        GROUP BY bi.ComponentProductId
    ) x
    GROUP BY ProductId
) sold ON sold.ProductId = p.ProductId
LEFT JOIN (
    SELECT pr.ProductId,
           SUM(IFNULL(pr.Quantity, 0) * IFNULL(uc.Multiplier, 1)) AS TotalReturned
    FROM PurchaseReturns pr
    LEFT JOIN UnitConversions uc ON uc.ProductId = pr.ProductId AND uc.FromUnitId = pr.UnitId
    WHERE (@BranchId IS NULL OR pr.BranchId = @BranchId)
    GROUP BY pr.ProductId
) ret ON ret.ProductId = p.ProductId
LEFT JOIN (
    SELECT ps.ProductId, SUM(IFNULL(ps.Quantity, 0)) AS TotalStockRemains
    FROM ProductStocks ps
    WHERE (@BranchId IS NULL OR ps.BranchId = @BranchId)
    GROUP BY ps.ProductId
) stock ON stock.ProductId = p.ProductId
LEFT JOIN (
    SELECT ranked.ProductId, ranked.CostPrice AS LatestPurchasePrice
    FROM (
        SELECT poi.ProductId, poi.CostPrice,
               ROW_NUMBER() OVER (PARTITION BY poi.ProductId ORDER BY po.OrderDate DESC, poi.POItemId DESC) AS rn
        FROM PurchaseOrderItems poi
        INNER JOIN PurchaseOrders po ON po.POId = poi.POId
        WHERE (@BranchId IS NULL OR po.BranchId = @BranchId)
    ) ranked
    WHERE ranked.rn = 1
) lp ON lp.ProductId = p.ProductId
LEFT JOIN (
    SELECT ranked.ProductId, ranked.UnitPrice AS LatestSalePrice
    FROM (
        SELECT si.ProductId, si.UnitPrice,
               ROW_NUMBER() OVER (PARTITION BY si.ProductId ORDER BY s.SaleDate DESC, si.SaleItemId DESC) AS rn
        FROM SaleItems si
        INNER JOIN Sales s ON s.SaleId = si.SaleId
        WHERE UPPER(TRIM(IFNULL(si.ItemType, 'Product'))) = 'PRODUCT'
          AND si.ProductId IS NOT NULL
          AND IFNULL(si.UnitPrice, 0) > 0
          AND UPPER(TRIM(IFNULL(s.OrderStatus, 'Completed'))) <> 'CANCELLED'
          AND (@BranchId IS NULL OR s.BranchId = @BranchId)
    ) ranked
    WHERE ranked.rn = 1
) ls ON ls.ProductId = p.ProductId
WHERE (@Query = ''
       OR p.Name LIKE CONCAT('%', @Query, '%')
       OR p.SKU LIKE CONCAT('%', @Query, '%'))
ORDER BY p.Name ASC
LIMIT @Limit OFFSET @Offset;

SELECT COUNT(*)
FROM Products p
WHERE (@Query = ''
       OR p.Name LIKE CONCAT('%', @Query, '%')
       OR p.SKU LIKE CONCAT('%', @Query, '%'));";

        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            BranchId = branchId,
            Query = query,
            Limit = filter.Limit,
            Offset = (filter.Page - 1) * filter.Limit
        });

        return Ok(new
        {
            list = await multi.ReadAsync<dynamic>(),
            total = await multi.ReadFirstAsync<int>()
        });
    }

    [HttpGet("product-purchase-batches/{productId}")]
    public async Task<IActionResult> GetProductPurchaseBatches(int productId, [FromQuery] int? branchId)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await EnsurePurchaseBatchRemainingColumnAsync(conn);
        int? resolvedBranchId = User.IsInRole("Admin") ? branchId : int.Parse(User.FindFirst("BranchId").Value);

        const string sql = @"
SELECT
    'Purchase' AS sourceType,
    poi.POItemId AS batchId,
    poi.POItemId AS poItemId,
    po.POId AS poId,
    po.OrderDate AS orderDate,
    IFNULL(s.SupplierName, 'General Supplier') AS supplierName,
    b.BranchName AS branchName,
    poi.ProductId AS productId,
    p.Name AS productName,
    poi.UnitId AS unitId,
    u.UnitName AS unitName,
    IFNULL(poi.Quantity, 0) AS quantity,
    IFNULL(poi.FOCQuantity, 0) AS focQuantity,
    NULL AS soldQuantity,
    NULL AS returnedQuantity,
    IFNULL(poi.RemainingQuantity, 0) AS remainingBaseQuantity,
    (IFNULL(poi.RemainingQuantity, 0) / IFNULL(uc.Multiplier, 1)) AS remainingQuantity,
    IFNULL(poi.CostPrice, 0) AS costPrice,
    poi.ExpiryDate AS expiryDate,
    IFNULL(poi.AlertDays, 30) AS alertDays,
    po.Status AS batchStatus
FROM PurchaseOrderItems poi
INNER JOIN PurchaseOrders po ON po.POId = poi.POId
INNER JOIN Products p ON p.ProductId = poi.ProductId
LEFT JOIN Units u ON u.UnitId = poi.UnitId
LEFT JOIN UnitConversions uc ON uc.ProductId = poi.ProductId AND uc.FromUnitId = poi.UnitId
LEFT JOIN Suppliers s ON s.SupplierId = po.SupplierId
LEFT JOIN Branches b ON b.BranchId = po.BranchId
WHERE poi.ProductId = @ProductId
  AND (@BranchId IS NULL OR po.BranchId = @BranchId)
  AND IFNULL(poi.RemainingQuantity, 0) > 0

UNION ALL

SELECT
    'Consignment' AS sourceType,
    ci.ConsignItemId AS batchId,
    ci.ConsignItemId AS poItemId,
    ci.ConsignId AS poId,
    c.OrderDate AS orderDate,
    IFNULL(s.SupplierName, 'General Supplier') AS supplierName,
    b.BranchName AS branchName,
    ci.ProductId AS productId,
    p.Name AS productName,
    ci.UnitId AS unitId,
    COALESCE(cu.UnitName, bu.UnitName) AS unitName,
    IFNULL(ci.Quantity, 0) AS quantity,
    0 AS focQuantity,
    IFNULL(ci.SoldQuantity, 0) AS soldQuantity,
    IFNULL(ci.ReturnedQuantity, 0) AS returnedQuantity,
    GREATEST(0, IFNULL(ci.Quantity, 0) - IFNULL(ci.SoldQuantity, 0) - IFNULL(ci.ReturnedQuantity, 0)) AS remainingBaseQuantity,
    GREATEST(0, IFNULL(ci.Quantity, 0) - IFNULL(ci.SoldQuantity, 0) - IFNULL(ci.ReturnedQuantity, 0)) AS remainingQuantity,
    IFNULL(ci.UnitPrice, 0) AS costPrice,
    NULL AS expiryDate,
    NULL AS alertDays,
    c.Status AS batchStatus
FROM ConsignmentItems ci
INNER JOIN Consignments c ON c.ConsignId = ci.ConsignId
INNER JOIN Products p ON p.ProductId = ci.ProductId
LEFT JOIN Units cu ON cu.UnitId = ci.UnitId
LEFT JOIN Units bu ON bu.UnitId = p.UnitId
LEFT JOIN Suppliers s ON s.SupplierId = c.SupplierId
LEFT JOIN Branches b ON b.BranchId = COALESCE(ci.BranchId, c.BranchId)
WHERE ci.ProductId = @ProductId
  AND (@BranchId IS NULL OR COALESCE(ci.BranchId, c.BranchId) = @BranchId)
  AND GREATEST(0, IFNULL(ci.Quantity, 0) - IFNULL(ci.SoldQuantity, 0) - IFNULL(ci.ReturnedQuantity, 0)) > 0
ORDER BY orderDate DESC, poId DESC, batchId DESC;";

        var rows = await conn.QueryAsync<dynamic>(sql, new
        {
            ProductId = productId,
            BranchId = resolvedBranchId
        });

        return Ok(rows);
    }

    [HttpGet("transfer-detail/{id}")]
    public async Task<IActionResult> GetTransferDetail(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

        string sql = @"
        -- Master Record
        SELECT t.TransferId, t.TransferDate, t.Status, t.Note,
               b1.BranchName as FromBranchName, b2.BranchName as ToBranchName,
               us.Username as CreatedBy
        FROM StockTransfers t
        JOIN Branches b1 ON t.FromBranchId = b1.BranchId
        JOIN Branches b2 ON t.ToBranchId = b2.BranchId
        LEFT JOIN ShopUsers us ON t.UserId = us.UserId
        WHERE t.TransferId = @id;

        -- Detail Items
        SELECT ti.*, p.Name as ProductName, u.UnitName
        FROM StockTransferItems ti
        JOIN Products p ON ti.ProductId = p.ProductId
        LEFT JOIN Units u ON ti.unitId = u.UnitId
        WHERE ti.TransferId = @id;";

        using var multi = await conn.QueryMultipleAsync(sql, new { id });

        var transfer = await multi.ReadFirstOrDefaultAsync<dynamic>();
        if (transfer == null) return NotFound(new { Message = "Transfer not found" });

        var items = await multi.ReadAsync<dynamic>();

        return Ok(new { transfer, items });
    }
}
