using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Services;
using Versa.Core.Models; // Model ကို သုံးရန်
using System.Security.Claims;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BranchesController : BaseShopController
    {
        private readonly DatabaseService _dbService;
        public BranchesController(DatabaseService dbService) { _dbService = dbService; }

        private bool CanManageBranches =>
            User.IsInRole("Admin") &&
            bool.TryParse(User.FindFirstValue("AllowBranch"), out var allowed) && allowed;

        private IActionResult BranchManagementDenied() =>
            StatusCode(StatusCodes.Status403Forbidden, new { Message = "Branch management is not enabled for this shop." });

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            // Model နှင့် ချိတ်ဆက်၍ ဆွဲထုတ်ခြင်း
            var branches = await conn.QueryAsync<Branch>("SELECT * FROM Branches ORDER BY BranchId DESC");
            return Ok(branches);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Branch branch) // dynamic အစား Branch ကို သုံးထားသည်
        {
            if (!CanManageBranches) return BranchManagementDenied();
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            if (branch.IsMainBranch)
            {
                await conn.ExecuteAsync("UPDATE Branches SET IsMainBranch = FALSE");
            }

            string sql = @"INSERT INTO Branches (BranchName, Location, Phone, IsMainBranch, IsActive, CreatedAt) 
                       VALUES (@BranchName, @Location, @Phone, @IsMainBranch, @IsActive, NOW())";

            await conn.ExecuteAsync(sql, branch);
            return Ok(new { Message = "Branch created successfully" });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Branch branch) // Branch Model ကို သုံးသည်
        {
            if (!CanManageBranches) return BranchManagementDenied();
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // URL Id နှင့် Body Id ကို ကိုက်ညီမှု ရှိမရှိ စစ်ဆေးခြင်း
            branch.BranchId = id;

            if (branch.IsMainBranch)
            {
                await conn.ExecuteAsync("UPDATE Branches SET IsMainBranch = FALSE");
            }

            string sql = @"UPDATE Branches SET 
                       BranchName = @BranchName, 
                       Location = @Location, 
                       Phone = @Phone, 
                       IsMainBranch = @IsMainBranch, 
                       IsActive = @IsActive 
                       WHERE BranchId = @BranchId";

            await conn.ExecuteAsync(sql, branch);
            return Ok(new { Message = "Branch updated successfully" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (!CanManageBranches) return BranchManagementDenied();
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            var usageCount = await conn.ExecuteScalarAsync<int>(@"
SELECT SUM(RefCount)
FROM (
    SELECT COUNT(*) AS RefCount FROM Sales WHERE BranchId = @id
    UNION ALL SELECT COUNT(*) FROM PurchaseOrders WHERE BranchId = @id
    UNION ALL SELECT COUNT(*) FROM SaleReturns WHERE BranchId = @id
    UNION ALL SELECT COUNT(*) FROM PurchaseReturns WHERE BranchId = @id
    UNION ALL SELECT COUNT(*) FROM StockAdjustments WHERE BranchId = @id
    UNION ALL SELECT COUNT(*) FROM StockTransfers WHERE FromBranchId = @id OR ToBranchId = @id
    UNION ALL SELECT COUNT(*) FROM ProductStocks WHERE BranchId = @id AND IFNULL(Quantity, 0) <> 0
    UNION ALL SELECT COUNT(*) FROM ShopUsers WHERE BranchId = @id
    UNION ALL SELECT COUNT(*) FROM Expenses WHERE BranchId = @id
    UNION ALL SELECT COUNT(*) FROM Consignments WHERE BranchId = @id
    UNION ALL SELECT COUNT(*) FROM ConsignmentItems WHERE BranchId = @id
) refs", new { id });

            if (usageCount > 0)
            {
                return BadRequest(new { Message = "This branch already has sales, purchases, stock, users, expenses, transfer, return, or consignment history. Please make it inactive instead of deleting." });
            }

            await conn.ExecuteAsync("DELETE FROM Branches WHERE BranchId = @id", new { id });
            return Ok(new { Message = "Branch deleted successfully." });
        }
    }
}
