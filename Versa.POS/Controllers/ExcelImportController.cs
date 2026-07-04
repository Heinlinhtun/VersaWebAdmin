using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using MiniExcelLibs;
using Versa.Core.Services;
using System.IO;
using System.Linq;

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
        public async Task<IActionResult> ImportFromExcel(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("Excel file requires choice.");

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await conn.OpenAsync();
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
                int poCount = 0;
                string debugInfo = "";

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

                foreach (var group in supplierGroups)
                {
                    string supplierName = group.Key.SupplierName;
                    string branchName = group.Key.BranchName;
                    
                    int? supplierId = await GetOrCreateId(conn, "Suppliers", "SupplierName", "SupplierId", supplierName, transaction);
                    int? branchId = string.IsNullOrWhiteSpace(branchName)
                        ? await GetDefaultBranchId(conn, transaction)
                        : await GetOrCreateId(conn, "Branches", "BranchName", "BranchId", branchName, transaction);

                    if (branchId == null || branchId == 0) 
                        throw new Exception($"Branch '{branchName}' could not be found. Please ensure exact name matching.");

                    // Create 1 Purchase Order for this supplier and branch
                    decimal poTotal = group.Sum(r => ParseDecimal(GetRawValue(r, "Qty")) * ParseDecimal(GetRawValue(r, "Purchase Price")));

                    string poSql = @"INSERT INTO PurchaseOrders (SupplierId, BranchId, TotalAmount, PaidAmount, Status, Remarks, OrderDate) 
                                     VALUES (@supplierId, @branchId, @poTotal, @poTotal, 'Paid', 'Excel Import', NOW());
                                     SELECT LAST_INSERT_ID();";
                    
                    int poId = await conn.ExecuteScalarAsync<int>(poSql, new { supplierId, branchId, poTotal }, transaction);
                    poCount++;

                    int rowIndex = 2; // For error reporting

                    foreach (var dRow in group)
                    {
                        string itemName = GetValue(dRow, "Item Name");
                        try
                        {
                            string sku = GetValue(dRow, "SKU/Barcode");
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

                            int? catId = await GetOrCreateId(conn, "Categories", "CategoryName", "CategoryId", categoryName, transaction);
                            int? unitId = await GetOrCreateId(conn, "Units", "ShortName", "UnitId", unitName, transaction);

                            int productId = 0;
                            if (!string.IsNullOrEmpty(sku))
                                productId = await conn.ExecuteScalarAsync<int>("SELECT ProductId FROM Products WHERE Sku = @sku LIMIT 1", new { sku }, transaction);

                            if (productId == 0)
                                productId = await conn.ExecuteScalarAsync<int>("SELECT ProductId FROM Products WHERE Name = @itemName LIMIT 1", new { itemName }, transaction);

                            if (productId > 0)
                            {
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
                            }
                            else
                            {
                                // IMPORTANT: Set IsActive = 1 so it shows in Sale Terminal
                                string insertSql = @"INSERT INTO Products (Name, Sku, CategoryId, UnitId, CostPrice, SalePrice, IsActive) 
                                    VALUES (@itemName, @sku, @catId, @unitId, @purchasePrice, @sellingPrice, 1);
                                    SELECT LAST_INSERT_ID();";
                                productId = await conn.ExecuteScalarAsync<int>(insertSql, new { itemName, sku, catId, unitId, purchasePrice, sellingPrice }, transaction);
                            }

                            // Insert Purchase Order Item linked to the Group PO
                            string poiSql = @"INSERT INTO PurchaseOrderItems (POId, ProductId, UnitId, Quantity, FOCQuantity, CostPrice, ExpiryDate, AlertDays, RemainingQuantity) 
                                            VALUES (@poId, @productId, @unitId, @qty, 0, @purchasePrice, @expiryDate, @warningDays, @remainingQuantity)";
                            await conn.ExecuteAsync(poiSql, new { poId, productId, unitId, qty, purchasePrice, expiryDate, warningDays, remainingQuantity = qty }, transaction);

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

                await transaction.CommitAsync();
                return Ok(new { Message = $"Success: {successItemCount} items imported across {poCount} Purchase Orders." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { Message = ex.ToString() });
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

        private async Task<int?> GetOrCreateId(MySqlConnection conn, string tableName, string nameCol, string idCol, string nameValue, MySqlTransaction trans)
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
                return await conn.ExecuteScalarAsync<int>(insertSql, new { nameValue }, trans);
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
