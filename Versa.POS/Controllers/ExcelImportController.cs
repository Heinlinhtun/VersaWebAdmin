using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using MiniExcelLibs;
using Versa.Core.Services;
using System.IO;
using System.Linq;
using System.Security.Claims;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExcelImportController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public ExcelImportController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        private static async Task EnsureImportTrackingTablesAsync(MySqlConnection conn)
        {
            await conn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS ExcelImportBatches (
    BatchId INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FileName VARCHAR(255) NOT NULL,
    ImportedAt DATETIME NOT NULL,
    ImportedBy INT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'Completed',
    RolledBackAt DATETIME NULL,
    RolledBackBy INT NULL,
    ItemCount INT NOT NULL DEFAULT 0,
    CreatedProductCount INT NOT NULL DEFAULT 0,
    UpdatedProductCount INT NOT NULL DEFAULT 0,
    PurchaseOrderCount INT NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS ExcelImportBatchPurchaseOrders (
    BatchId INT NOT NULL,
    POId INT NOT NULL,
    PRIMARY KEY (BatchId, POId)
);
CREATE TABLE IF NOT EXISTS ExcelImportBatchProducts (
    BatchId INT NOT NULL,
    ProductId INT NOT NULL,
    WasCreated TINYINT(1) NOT NULL,
    PreviousName VARCHAR(255) NULL,
    PreviousSku VARCHAR(255) NULL,
    PreviousCategoryId INT NULL,
    PreviousUnitId INT NULL,
    PreviousCostPrice DECIMAL(18,4) NULL,
    PreviousSalePrice DECIMAL(18,4) NULL,
    PreviousIsActive TINYINT(1) NULL,
    PostName VARCHAR(255) NULL,
    PostSku VARCHAR(255) NULL,
    PostCategoryId INT NULL,
    PostUnitId INT NULL,
    PostCostPrice DECIMAL(18,4) NULL,
    PostSalePrice DECIMAL(18,4) NULL,
    PostIsActive TINYINT(1) NULL,
    PRIMARY KEY (BatchId, ProductId)
);
CREATE TABLE IF NOT EXISTS ExcelImportBatchStocks (
    BatchId INT NOT NULL,
    BranchId INT NOT NULL,
    ProductId INT NOT NULL,
    HadStockRow TINYINT(1) NOT NULL,
    PreviousQuantity DECIMAL(18,4) NOT NULL,
    ImportedQuantity DECIMAL(18,4) NOT NULL,
    PostImportQuantity DECIMAL(18,4) NULL,
    PRIMARY KEY (BatchId, BranchId, ProductId)
);
CREATE TABLE IF NOT EXISTS ExcelImportBatchEntities (
    BatchId INT NOT NULL,
    EntityType VARCHAR(20) NOT NULL,
    EntityId INT NOT NULL,
    PRIMARY KEY (BatchId, EntityType, EntityId)
);" );
        }

        private static async Task EnsurePurchaseBatchRemainingColumnAsync(MySqlConnection conn, MySqlTransaction transaction)
        {
            var hasColumn = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'PurchaseOrderItems'
  AND COLUMN_NAME = 'RemainingQuantity'",
                transaction: transaction);

            if (hasColumn == 0)
            {
                await conn.ExecuteAsync(
                    "ALTER TABLE PurchaseOrderItems ADD COLUMN RemainingQuantity DECIMAL(18,4) NULL",
                    transaction: transaction);
            }

            await conn.ExecuteAsync(@"
UPDATE PurchaseOrderItems poi
LEFT JOIN UnitConversions uc
  ON uc.ProductId = poi.ProductId
 AND uc.FromUnitId = poi.UnitId
SET poi.RemainingQuantity = (IFNULL(poi.Quantity, 0) + IFNULL(poi.FOCQuantity, 0)) * IFNULL(uc.Multiplier, 1)
WHERE poi.RemainingQuantity IS NULL",
                transaction: transaction);
        }

        [HttpPost("import")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> ImportFromExcel(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Excel file requires choice.");

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            await EnsureImportTrackingTablesAsync(conn);
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);

                var excelType = GetExcelType(file.FileName);
                List<dynamic> rows;
                try
                {
                    rows = file.OpenReadStream().Query(useHeaderRow: true, excelType: excelType).ToList();
                }
                catch (System.IO.InvalidDataException)
                {
                    return BadRequest(new { Message = "The uploaded file is not a valid Excel format. If you downloaded the template as a CSV, please ensure you uploaded it as a .csv file, or save it properly as an Excel Workbook (.xlsx)." });
                }
                
                int successItemCount = 0;
                int createdProductCount = 0;
                int updatedProductCount = 0;
                int poCount = 0;

                // Pre-filter valid rows and group by Branch Name and Supplier Name
                var supplierGroups = rows
                    .Select(r => r as IDictionary<string, object>)
                    .Where(r => r != null && !string.IsNullOrEmpty(GetValue(r, "Item Name")))
                    .GroupBy(r => new { 
                        SupplierName = GetValue(r, "Supplier Name"),
                        BranchName = GetValue(r, "Branch Name")
                    })
                    .ToList();

                if (supplierGroups.Count == 0)
                {
                    return BadRequest(new { Message = "No valid data rows found. Ensure 'Item Name' is populated." });
                }

                var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? importedBy = int.TryParse(userIdValue, out var authenticatedUserId) ? authenticatedUserId : null;
                var safeFileName = Path.GetFileName(file.FileName);
                int batchId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO ExcelImportBatches (FileName, ImportedAt, ImportedBy, Status)
VALUES (@FileName, NOW(), @ImportedBy, 'InProgress');
SELECT LAST_INSERT_ID();",
                    new { FileName = safeFileName, ImportedBy = importedBy }, transaction);

                var generatedPoIds = new List<int>();

                foreach (var group in supplierGroups)
                {
                    string supplierName = group.Key.SupplierName;
                    string branchName = group.Key.BranchName;
                    
                    int? supplierId = await GetOrCreateId(conn, "Suppliers", "SupplierName", "SupplierId", supplierName, transaction, batchId, "Supplier");
                    int? branchId = string.IsNullOrWhiteSpace(branchName)
                        ? await GetDefaultBranchId(conn, transaction)
                        : await GetOrCreateId(conn, "Branches", "BranchName", "BranchId", branchName, transaction, batchId, "Branch");

                    if (branchId == null || branchId == 0) 
                        throw new Exception($"Branch '{branchName}' could not be found. Please ensure exact name matching.");

                    if (!User.IsInRole("Admin"))
                    {
                        var managerBranchValue = User.FindFirst("BranchId")?.Value;
                        if (!int.TryParse(managerBranchValue, out var managerBranchId) || branchId.Value != managerBranchId)
                            throw new Exception("Managers can import inventory only into their assigned branch.");
                    }

                    // Create 1 Purchase Order for this supplier and branch
                    decimal poTotal = group.Sum(r => ParseDecimal(GetRawValue(r, "Qty")) * ParseDecimal(GetRawValue(r, "Purchase Price")));

                    string poSql = @"INSERT INTO PurchaseOrders (SupplierId, BranchId, TotalAmount, PaidAmount, Status, Remarks, OrderDate) 
                                     VALUES (@supplierId, @branchId, @poTotal, @poTotal, 'Paid', 'Excel Import', NOW());
                                     SELECT LAST_INSERT_ID();";
                    
                    int poId = await conn.ExecuteScalarAsync<int>(poSql, new { supplierId, branchId, poTotal }, transaction);
                    await conn.ExecuteAsync(
                        "INSERT INTO ExcelImportBatchPurchaseOrders (BatchId, POId) VALUES (@batchId, @poId)",
                        new { batchId, poId }, transaction);
                    generatedPoIds.Add(poId);
                    poCount++;

                    int rowIndex = 2; // For error reporting

                    foreach (var dRow in group)
                    {
                        string itemName = GetValue(dRow, "Item Name");
                        try
                        {
                            string sku = GetValue(dRow, "SKU/Barcode");
                            bool hasProvidedSku = !string.IsNullOrWhiteSpace(sku);
                            string categoryName = GetValue(dRow, "Category");
                            string unitName = GetValue(dRow, "Basic Unit");
                            decimal qty = ParseDecimal(GetRawValue(dRow, "Qty"));
                            decimal purchasePrice = ParseDecimal(GetRawValue(dRow, "Purchase Price"));
                            decimal sellingPrice = ParseDecimal(GetRawValue(dRow, "Selling Price"));
                            DateTime? expiryDate = ParseDate(GetRawValue(dRow, "Expiry Date"));
                            int warningDays = (int)ParseDecimal(GetRawValue(dRow, "Warning Day count"));

                            // Auto-generate SKU if not provided (same algorithm as ProductsController.Create)
                            if (string.IsNullOrWhiteSpace(sku))
                            {
                                sku = "SKU" + DateTime.Now.ToString("yyyyMMdd") + (DateTime.Now.Ticks % 1000000).ToString("D6");
                            }

                            int? catId = await GetOrCreateId(conn, "Categories", "CategoryName", "CategoryId", categoryName, transaction, batchId, "Category");
                            int? unitId = await GetOrCreateId(conn, "Units", "ShortName", "UnitId", unitName, transaction, batchId, "Unit");

                            int productId = 0;
                            if (hasProvidedSku)
                                productId = await conn.ExecuteScalarAsync<int>("SELECT ProductId FROM Products WHERE Sku = @sku LIMIT 1", new { sku }, transaction);

                            if (productId == 0 && !hasProvidedSku)
                                productId = await conn.ExecuteScalarAsync<int>("SELECT ProductId FROM Products WHERE Name = @itemName LIMIT 1", new { itemName }, transaction);

                            if (productId > 0)
                            {
                                await conn.ExecuteAsync(@"
INSERT IGNORE INTO ExcelImportBatchProducts
    (BatchId, ProductId, WasCreated, PreviousName, PreviousSku, PreviousCategoryId,
     PreviousUnitId, PreviousCostPrice, PreviousSalePrice, PreviousIsActive)
SELECT @batchId, ProductId, 0, Name, Sku, CategoryId, UnitId, CostPrice, SalePrice, IsActive
FROM Products
WHERE ProductId = @productId",
                                    new { batchId, productId }, transaction);

                                // Calculate Moving Weighted Average Cost
                                var currentProductData = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                                    SELECT p.CostPrice as OldCost, IFNULL(SUM(ps.Quantity), 0) as CurrentTotalStock 
                                    FROM Products p 
                                    LEFT JOIN ProductStocks ps ON p.ProductId = ps.ProductId 
                                    WHERE p.ProductId = @productId
                                    GROUP BY p.ProductId, p.CostPrice",
                                    new { productId }, transaction);

                                decimal oldCost = currentProductData?.OldCost ?? 0m;
                                decimal currentStock = currentProductData?.CurrentTotalStock ?? 0m;

                                decimal calculatedNewCost;
                                if (currentStock + qty > 0)
                                {
                                    calculatedNewCost = ((currentStock * oldCost) + (qty * purchasePrice)) / (currentStock + qty);
                                }
                                else
                                {
                                    calculatedNewCost = purchasePrice;
                                }

                                string updateSql = @"UPDATE Products SET 
                                    Name=@itemName, Sku=@sku, CategoryId=@catId, UnitId=@unitId, 
                                    CostPrice=@calculatedNewCost, SalePrice=@sellingPrice, IsActive=1
                                    WHERE ProductId=@productId";
                                await conn.ExecuteAsync(updateSql, new { itemName, sku, catId, unitId, calculatedNewCost, sellingPrice, productId }, transaction);
                                updatedProductCount++;
                            }
                            else
                            {
                                // IMPORTANT: Set IsActive = 1 so it shows in Sale Terminal
                                string insertSql = @"INSERT INTO Products (Name, Sku, CategoryId, UnitId, CostPrice, SalePrice, IsActive) 
                                    VALUES (@itemName, @sku, @catId, @unitId, @purchasePrice, @sellingPrice, 1);
                                    SELECT LAST_INSERT_ID();";
                                productId = await conn.ExecuteScalarAsync<int>(insertSql, new { itemName, sku, catId, unitId, purchasePrice, sellingPrice }, transaction);
                                await conn.ExecuteAsync(@"
INSERT INTO ExcelImportBatchProducts (BatchId, ProductId, WasCreated)
VALUES (@batchId, @productId, 1)", new { batchId, productId }, transaction);
                                createdProductCount++;
                            }

                            // Insert Purchase Order Item linked to the Group PO
                            string poiSql = @"INSERT INTO PurchaseOrderItems (POId, ProductId, UnitId, Quantity, FOCQuantity, CostPrice, ExpiryDate, AlertDays, RemainingQuantity) 
                                            VALUES (@poId, @productId, @unitId, @qty, 0, @purchasePrice, @expiryDate, @warningDays, @remainingQuantity)";
                            await conn.ExecuteAsync(poiSql, new { poId, productId, unitId, qty, purchasePrice, expiryDate, warningDays, remainingQuantity = qty }, transaction);

                            // Preserve the pre-import stock once, then accumulate every row
                            // from this batch that targets the same product and branch.
                            var previousStock = await conn.QueryFirstOrDefaultAsync<decimal?>(@"
SELECT Quantity FROM ProductStocks
WHERE BranchId = @branchId AND ProductId = @productId
FOR UPDATE", new { branchId, productId }, transaction);
                            await conn.ExecuteAsync(@"
INSERT INTO ExcelImportBatchStocks
    (BatchId, BranchId, ProductId, HadStockRow, PreviousQuantity, ImportedQuantity)
VALUES
    (@batchId, @branchId, @productId, @hadStockRow, @previousQuantity, @qty)
ON DUPLICATE KEY UPDATE ImportedQuantity = ImportedQuantity + VALUES(ImportedQuantity)",
                                new
                                {
                                    batchId,
                                    branchId,
                                    productId,
                                    hadStockRow = previousStock.HasValue,
                                    previousQuantity = previousStock ?? 0m,
                                    qty
                                }, transaction);

                            // Update Stock for the specific branch
                            string stockSql = @"
                                INSERT INTO ProductStocks (BranchId, ProductId, Quantity) 
                                VALUES (@branchId, @productId, @qty) 
                                ON DUPLICATE KEY UPDATE Quantity = Quantity + @qty";
                            await conn.ExecuteAsync(stockSql, new { branchId, productId, qty }, transaction);

                            successItemCount++;
                        }
                        catch (Exception rowEx)
                        {
                            throw new Exception($"Error at item [{itemName}]: {rowEx.Message}");
                        }
                        rowIndex++;
                    }
                }

                await conn.ExecuteAsync(@"
UPDATE ExcelImportBatchStocks ibs
LEFT JOIN ProductStocks ps ON ps.BranchId = ibs.BranchId AND ps.ProductId = ibs.ProductId
SET ibs.PostImportQuantity = IFNULL(ps.Quantity, 0)
WHERE ibs.BatchId = @batchId;

UPDATE ExcelImportBatchProducts ibp
JOIN Products p ON p.ProductId = ibp.ProductId
SET ibp.PostName = p.Name,
    ibp.PostSku = p.Sku,
    ibp.PostCategoryId = p.CategoryId,
    ibp.PostUnitId = p.UnitId,
    ibp.PostCostPrice = p.CostPrice,
    ibp.PostSalePrice = p.SalePrice,
    ibp.PostIsActive = p.IsActive
WHERE ibp.BatchId = @batchId;

UPDATE ExcelImportBatches
SET Status = 'Completed', ItemCount = @successItemCount,
    CreatedProductCount = @createdProductCount,
    UpdatedProductCount = @updatedProductCount,
    PurchaseOrderCount = @poCount
WHERE BatchId = @batchId;",
                    new { batchId, successItemCount, createdProductCount, updatedProductCount, poCount }, transaction);

                await transaction.CommitAsync();
                return Ok(new
                {
                    Message = $"Success: {successItemCount} rows imported across {poCount} Purchase Orders. Products created: {createdProductCount}, updated: {updatedProductCount}.",
                    BatchId = batchId,
                    PurchaseOrderIds = generatedPoIds,
                    PurchaseOrderNumbers = generatedPoIds.Select(id => $"PO-{id:D5}").ToArray()
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.ToString() });
            }
        }

        [HttpGet("history")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetImportHistory()
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            await EnsureImportTrackingTablesAsync(conn);

            var isAdmin = User.IsInRole("Admin");
            var branchValue = User.FindFirst("BranchId")?.Value;
            int? branchId = int.TryParse(branchValue, out var parsedBranchId) ? parsedBranchId : null;

            var batches = await conn.QueryAsync<dynamic>(@"
SELECT
    b.BatchId,
    b.FileName,
    b.ImportedAt,
    b.Status,
    b.RolledBackAt,
    b.ItemCount,
    b.CreatedProductCount,
    b.UpdatedProductCount,
    b.PurchaseOrderCount,
    COALESCE(u.Username, 'System') AS ImportedByName,
    GROUP_CONCAT(CONCAT('PO-', LPAD(bp.POId, 5, '0')) ORDER BY bp.POId SEPARATOR ', ') AS PurchaseOrderNumbers
FROM ExcelImportBatches b
LEFT JOIN ShopUsers u ON u.UserId = b.ImportedBy
LEFT JOIN ExcelImportBatchPurchaseOrders bp ON bp.BatchId = b.BatchId
WHERE (@IsAdmin = 1 OR (@BranchId IS NOT NULL AND NOT EXISTS (
    SELECT 1
    FROM ExcelImportBatchStocks scopeStock
    WHERE scopeStock.BatchId = b.BatchId
      AND scopeStock.BranchId <> @BranchId
)))
GROUP BY b.BatchId, b.FileName, b.ImportedAt, b.Status, b.RolledBackAt,
         b.ItemCount, b.CreatedProductCount, b.UpdatedProductCount,
         b.PurchaseOrderCount, u.Username
ORDER BY b.ImportedAt DESC, b.BatchId DESC
LIMIT 25", new { IsAdmin = isAdmin, BranchId = branchId });

            return Ok(batches);
        }

        [HttpPost("rollback/{batchId:int}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> RollbackImport(int batchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
            await EnsureImportTrackingTablesAsync(conn);
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                await EnsurePurchaseBatchRemainingColumnAsync(conn, (MySqlTransaction)transaction);
                var batch = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT * FROM ExcelImportBatches WHERE BatchId = @batchId FOR UPDATE",
                    new { batchId }, transaction);

                if (batch == null)
                    return NotFound(new { Message = "Import batch not found." });
                if (!string.Equals((string)batch.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { Message = "This import has already been rolled back or is not available for rollback." });

                if (!User.IsInRole("Admin"))
                {
                    var branchValue = User.FindFirst("BranchId")?.Value;
                    if (!int.TryParse(branchValue, out var managerBranchId))
                        return Forbid();

                    var outsideBranchCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM ExcelImportBatchStocks ibs
WHERE ibs.BatchId = @batchId AND ibs.BranchId <> @managerBranchId",
                        new { batchId, managerBranchId }, transaction);
                    if (outsideBranchCount > 0)
                        return StatusCode(403, new { Message = "Only an administrator can undo an import that affected multiple branches." });
                }

                // Lock all records that the safety checks depend on so another transaction
                // cannot sell, return, or edit this import between validation and rollback.
                await conn.QueryAsync<int>(@"
SELECT po.POId
FROM PurchaseOrders po
JOIN ExcelImportBatchPurchaseOrders bp ON bp.POId = po.POId
WHERE bp.BatchId = @batchId
FOR UPDATE", new { batchId }, transaction);
                await conn.QueryAsync<int>(@"
SELECT poi.POItemId
FROM PurchaseOrderItems poi
JOIN ExcelImportBatchPurchaseOrders bp ON bp.POId = poi.POId
WHERE bp.BatchId = @batchId
FOR UPDATE", new { batchId }, transaction);
                await conn.QueryAsync<int>(@"
SELECT ps.ProductId
FROM ProductStocks ps
JOIN ExcelImportBatchStocks ibs
  ON ibs.BranchId = ps.BranchId AND ibs.ProductId = ps.ProductId
WHERE ibs.BatchId = @batchId
FOR UPDATE", new { batchId }, transaction);
                await conn.QueryAsync<int>(@"
SELECT p.ProductId
FROM Products p
JOIN ExcelImportBatchProducts ibp ON ibp.ProductId = p.ProductId
WHERE ibp.BatchId = @batchId
FOR UPDATE", new { batchId }, transaction);

                var missingPoCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM ExcelImportBatchPurchaseOrders bp
LEFT JOIN PurchaseOrders po ON po.POId = bp.POId
WHERE bp.BatchId = @batchId AND po.POId IS NULL", new { batchId }, transaction);
                if (missingPoCount > 0)
                    return BadRequest(new { Message = "Undo blocked because an imported purchase order has already been changed or deleted." });

                var currentImportedItemCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM PurchaseOrderItems poi
JOIN ExcelImportBatchPurchaseOrders bp ON bp.POId = poi.POId
WHERE bp.BatchId = @batchId", new { batchId }, transaction);
                if (currentImportedItemCount != Convert.ToInt32(batch.ItemCount))
                    return BadRequest(new { Message = "Undo blocked because an item in an imported purchase order has already been changed or deleted." });

                var stockChangedCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM ExcelImportBatchStocks ibs
LEFT JOIN ProductStocks ps ON ps.BranchId = ibs.BranchId AND ps.ProductId = ibs.ProductId
WHERE ibs.BatchId = @batchId
  AND (ps.ProductId IS NULL OR ABS(IFNULL(ps.Quantity, 0) - IFNULL(ibs.PostImportQuantity, 0)) > 0.0001)",
                    new { batchId }, transaction);
                if (stockChangedCount > 0)
                    return BadRequest(new { Message = "Undo blocked because stock from this import has since been sold, transferred, adjusted, returned, or otherwise changed." });

                var purchaseBatchChangedCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM ExcelImportBatchPurchaseOrders bp
JOIN PurchaseOrderItems poi ON poi.POId = bp.POId
LEFT JOIN UnitConversions uc ON uc.ProductId = poi.ProductId AND uc.FromUnitId = poi.UnitId
WHERE bp.BatchId = @batchId
  AND ABS(IFNULL(poi.RemainingQuantity, 0) - ((IFNULL(poi.Quantity, 0) + IFNULL(poi.FOCQuantity, 0)) * IFNULL(uc.Multiplier, 1))) > 0.0001",
                    new { batchId }, transaction);
                var purchaseReturnCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM PurchaseReturns pr
JOIN ExcelImportBatchPurchaseOrders bp ON bp.POId = pr.POId
WHERE bp.BatchId = @batchId", new { batchId }, transaction);
                if (purchaseBatchChangedCount > 0 || purchaseReturnCount > 0)
                    return BadRequest(new { Message = "Undo blocked because items from an imported purchase order have already been consumed or returned." });

                var productChangedCount = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(*)
FROM ExcelImportBatchProducts ibp
LEFT JOIN Products p ON p.ProductId = ibp.ProductId
WHERE ibp.BatchId = @batchId
  AND (p.ProductId IS NULL
       OR NOT (BINARY p.Name <=> BINARY ibp.PostName)
       OR NOT (BINARY p.Sku <=> BINARY ibp.PostSku)
       OR NOT (p.CategoryId <=> ibp.PostCategoryId)
       OR NOT (p.UnitId <=> ibp.PostUnitId)
       OR ABS(IFNULL(p.CostPrice, 0) - IFNULL(ibp.PostCostPrice, 0)) > 0.0001
       OR ABS(IFNULL(p.SalePrice, 0) - IFNULL(ibp.PostSalePrice, 0)) > 0.0001
       OR NOT (p.IsActive <=> ibp.PostIsActive))",
                    new { batchId }, transaction);
                if (productChangedCount > 0)
                    return BadRequest(new { Message = "Undo blocked because one or more imported products were edited after the import." });

                var createdProductIds = (await conn.QueryAsync<int>(@"
SELECT ProductId FROM ExcelImportBatchProducts
WHERE BatchId = @batchId AND WasCreated = 1",
                    new { batchId }, transaction)).ToList();

                foreach (var productId in createdProductIds)
                {
                    var referenceCount = await conn.ExecuteScalarAsync<int>(@"
SELECT SUM(RefCount) FROM (
    SELECT COUNT(*) AS RefCount FROM SaleItems WHERE ProductId = @productId
    UNION ALL SELECT COUNT(*) FROM SaleReturns WHERE ProductId = @productId
    UNION ALL SELECT COUNT(*) FROM PurchaseReturns WHERE ProductId = @productId
    UNION ALL SELECT COUNT(*) FROM StockAdjustments WHERE ProductId = @productId
    UNION ALL SELECT COUNT(*) FROM StockTransferItems WHERE ProductId = @productId
    UNION ALL SELECT COUNT(*) FROM BundleItems WHERE ComponentProductId = @productId
    UNION ALL SELECT COUNT(*) FROM ConsignmentItems WHERE ProductId = @productId
    UNION ALL SELECT COUNT(*) FROM ProductPrices WHERE ProductId = @productId
    UNION ALL SELECT COUNT(*) FROM UnitConversions WHERE ProductId = @productId
    UNION ALL
        SELECT COUNT(*) FROM PurchaseOrderItems poi
        WHERE poi.ProductId = @productId
          AND NOT EXISTS (
              SELECT 1 FROM ExcelImportBatchPurchaseOrders bp
              WHERE bp.BatchId = @batchId AND bp.POId = poi.POId
          )
    UNION ALL
        SELECT COUNT(*) FROM ProductStocks ps
        WHERE ps.ProductId = @productId
          AND NOT EXISTS (
              SELECT 1 FROM ExcelImportBatchStocks ibs
              WHERE ibs.BatchId = @batchId
                AND ibs.BranchId = ps.BranchId
                AND ibs.ProductId = ps.ProductId
          )
) refs", new { batchId, productId }, transaction);

                    if (referenceCount > 0)
                        return BadRequest(new { Message = "Undo blocked because a product created by this import is now used by another transaction or configuration." });
                }

                await conn.ExecuteAsync(@"
DELETE poi
FROM PurchaseOrderItems poi
JOIN ExcelImportBatchPurchaseOrders bp ON bp.POId = poi.POId
WHERE bp.BatchId = @batchId;

DELETE po
FROM PurchaseOrders po
JOIN ExcelImportBatchPurchaseOrders bp ON bp.POId = po.POId
WHERE bp.BatchId = @batchId;

UPDATE ProductStocks ps
JOIN ExcelImportBatchStocks ibs
  ON ibs.BranchId = ps.BranchId AND ibs.ProductId = ps.ProductId
SET ps.Quantity = ibs.PreviousQuantity
WHERE ibs.BatchId = @batchId AND ibs.HadStockRow = 1;

DELETE ps
FROM ProductStocks ps
JOIN ExcelImportBatchStocks ibs
  ON ibs.BranchId = ps.BranchId AND ibs.ProductId = ps.ProductId
WHERE ibs.BatchId = @batchId AND ibs.HadStockRow = 0;

UPDATE Products p
JOIN ExcelImportBatchProducts ibp ON ibp.ProductId = p.ProductId
SET p.Name = ibp.PreviousName,
    p.Sku = ibp.PreviousSku,
    p.CategoryId = ibp.PreviousCategoryId,
    p.UnitId = ibp.PreviousUnitId,
    p.CostPrice = ibp.PreviousCostPrice,
    p.SalePrice = ibp.PreviousSalePrice,
    p.IsActive = ibp.PreviousIsActive
WHERE ibp.BatchId = @batchId AND ibp.WasCreated = 0;

DELETE p
FROM Products p
JOIN ExcelImportBatchProducts ibp ON ibp.ProductId = p.ProductId
WHERE ibp.BatchId = @batchId AND ibp.WasCreated = 1;",
                    new { batchId }, transaction);

                await DeleteUnusedCreatedEntitiesAsync(conn, (MySqlTransaction)transaction, batchId);

                var rollbackUserValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? rolledBackBy = int.TryParse(rollbackUserValue, out var rollbackUserId) ? rollbackUserId : null;
                await conn.ExecuteAsync(@"
UPDATE ExcelImportBatches
SET Status = 'RolledBack', RolledBackAt = NOW(), RolledBackBy = @rolledBackBy
WHERE BatchId = @batchId",
                    new { batchId, rolledBackBy }, transaction);

                await transaction.CommitAsync();
                return Ok(new { Message = "Excel import rolled back successfully." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = "Import rollback failed: " + ex.Message });
            }
        }

        private string GetValue(IDictionary<string, object> row, string key)
        {
            if (row == null) return string.Empty;

            // Trim the search key
            key = key.Trim();

            // Case-insensitive lookup with trimmed keys
            string foundKey = row.Keys.FirstOrDefault(k => k.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
            if (foundKey != null) return row[foundKey]?.ToString()?.Trim() ?? string.Empty;

            return string.Empty;
        }

        // Overload to get the raw object if needed for typed parsing
        private object GetRawValue(IDictionary<string, object> row, string key)
        {
            if (row == null) return null;
            key = key.Trim();
            string foundKey = row.Keys.FirstOrDefault(k => k.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
            if (foundKey != null) return row[foundKey];
            return null;
        }

        private decimal ParseDecimal(object val)
        {
            if (val == null || val == DBNull.Value) return 0;
            if (val is decimal d) return d;
            if (val is double db) return (decimal)db;
            if (val is int i) return (decimal)i;
            if (val is float f) return (decimal)f;

            string s = val.ToString();
            return decimal.TryParse(s, out decimal res) ? res : 0;
        }

        private DateTime? ParseDate(object val)
        {
            if (val == null || val == DBNull.Value) return null;
            if (val is DateTime dt) return dt;

            string s = val.ToString();
            return DateTime.TryParse(s, out DateTime res) ? res : null;
        }

        private ExcelType GetExcelType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext == ".csv") return ExcelType.CSV;
            return ExcelType.XLSX;
        }

        private static async Task DeleteUnusedCreatedEntitiesAsync(MySqlConnection conn, MySqlTransaction transaction, int batchId)
        {
            await conn.ExecuteAsync(@"
DELETE c
FROM Categories c
JOIN ExcelImportBatchEntities e ON e.EntityId = c.CategoryId AND e.EntityType = 'Category'
WHERE e.BatchId = @batchId
  AND NOT EXISTS (SELECT 1 FROM Products p WHERE p.CategoryId = c.CategoryId);

DELETE u
FROM Units u
JOIN ExcelImportBatchEntities e ON e.EntityId = u.UnitId AND e.EntityType = 'Unit'
WHERE e.BatchId = @batchId
  AND NOT EXISTS (SELECT 1 FROM Products p WHERE p.UnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM UnitConversions uc WHERE uc.FromUnitId = u.UnitId OR uc.ToUnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM ProductPrices pp WHERE pp.UnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM SaleItems si WHERE si.UnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM PurchaseOrderItems poi WHERE poi.UnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM SaleReturns sr WHERE sr.UnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM PurchaseReturns pr WHERE pr.UnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM StockAdjustments sa WHERE sa.UnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM StockTransferItems sti WHERE sti.UnitId = u.UnitId)
  AND NOT EXISTS (SELECT 1 FROM BundleItems bi WHERE bi.UnitId = u.UnitId);

DELETE s
FROM Suppliers s
JOIN ExcelImportBatchEntities e ON e.EntityId = s.SupplierId AND e.EntityType = 'Supplier'
WHERE e.BatchId = @batchId
  AND NOT EXISTS (SELECT 1 FROM PurchaseOrders po WHERE po.SupplierId = s.SupplierId)
  AND NOT EXISTS (SELECT 1 FROM SupplierPayments sp WHERE sp.SupplierId = s.SupplierId)
  AND NOT EXISTS (SELECT 1 FROM Consignments c WHERE c.SupplierId = s.SupplierId);

DELETE b
FROM Branches b
JOIN ExcelImportBatchEntities e ON e.EntityId = b.BranchId AND e.EntityType = 'Branch'
WHERE e.BatchId = @batchId
  AND NOT EXISTS (SELECT 1 FROM ProductStocks ps WHERE ps.BranchId = b.BranchId)
  AND NOT EXISTS (SELECT 1 FROM Sales s WHERE s.BranchId = b.BranchId)
  AND NOT EXISTS (SELECT 1 FROM PurchaseOrders po WHERE po.BranchId = b.BranchId)
  AND NOT EXISTS (SELECT 1 FROM ShopUsers su WHERE su.BranchId = b.BranchId)
  AND NOT EXISTS (SELECT 1 FROM Expenses ex WHERE ex.BranchId = b.BranchId)
  AND NOT EXISTS (SELECT 1 FROM StockTransfers st WHERE st.FromBranchId = b.BranchId OR st.ToBranchId = b.BranchId)
  AND NOT EXISTS (SELECT 1 FROM Consignments c WHERE c.BranchId = b.BranchId)
  AND NOT EXISTS (SELECT 1 FROM ConsignmentItems ci WHERE ci.BranchId = b.BranchId);",
                new { batchId }, transaction);
        }

        private async Task<int?> GetOrCreateId(
            MySqlConnection conn,
            string tableName,
            string nameCol,
            string idCol,
            string nameValue,
            MySqlTransaction trans,
            int? batchId = null,
            string? entityType = null)
        {
            if (string.IsNullOrEmpty(nameValue)) return null;
            nameValue = nameValue.Trim();

            string selectSql = $"SELECT {idCol} FROM {tableName} WHERE {nameCol} = @nameValue LIMIT 1";
            var existingId = await conn.ExecuteScalarAsync<int?>(selectSql, new { nameValue }, trans);

            if (existingId.HasValue) return existingId.Value;

            try
            {
                string insertSql;
                if (tableName == "Branches")
                {
                    // Branches require additional NOT NULL columns in most schemas.
                    insertSql = @$"INSERT INTO {tableName} (BranchName, Location, Phone, IsMainBranch, IsActive, CreatedAt)
                                   VALUES (@nameValue, '', '', FALSE, TRUE, NOW());
                                   SELECT LAST_INSERT_ID();";
                }
                else if (tableName == "Units")
                {
                    // Units table requires both UnitName and ShortName
                    insertSql = $"INSERT INTO {tableName} (UnitName, {nameCol}) VALUES (@nameValue, @nameValue); SELECT LAST_INSERT_ID();";
                }
                else
                {
                    insertSql = $"INSERT INTO {tableName} ({nameCol}) VALUES (@nameValue); SELECT LAST_INSERT_ID();";
                }
                var createdId = await conn.ExecuteScalarAsync<int>(insertSql, new { nameValue }, trans);
                if (batchId.HasValue && !string.IsNullOrWhiteSpace(entityType))
                {
                    await conn.ExecuteAsync(@"
INSERT IGNORE INTO ExcelImportBatchEntities (BatchId, EntityType, EntityId)
VALUES (@BatchId, @EntityType, @EntityId)",
                        new { BatchId = batchId.Value, EntityType = entityType, EntityId = createdId }, trans);
                }
                return createdId;
            }
            catch (Exception ex)
            {
                // Explicitly throwing this for better debugging if a new master data insert fails
                throw new Exception($"Failed to auto-create missing '{nameValue}' in {tableName}. Error: {ex.Message}");
            }
        }

        private async Task<int> GetDefaultBranchId(MySqlConnection conn, MySqlTransaction trans)
        {
            // Prefer explicit main branch flag.
            var mainId = await conn.ExecuteScalarAsync<int?>(
                "SELECT BranchId FROM Branches WHERE IsMainBranch = TRUE LIMIT 1",
                transaction: trans);
            if (mainId.HasValue) return mainId.Value;

            // Fallback to conventional name used across UI.
            var byNameId = await conn.ExecuteScalarAsync<int?>(
                "SELECT BranchId FROM Branches WHERE LOWER(BranchName) = 'main branch' LIMIT 1",
                transaction: trans);
            if (byNameId.HasValue) return byNameId.Value;

            // Final fallback: first branch in the table.
            var anyId = await conn.ExecuteScalarAsync<int?>(
                "SELECT BranchId FROM Branches ORDER BY BranchId ASC LIMIT 1",
                transaction: trans);
            if (anyId.HasValue) return anyId.Value;

            throw new Exception("No branches exist in the system. Create a branch first, then retry the import.");
        }

    }
}
