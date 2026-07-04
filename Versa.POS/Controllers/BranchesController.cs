using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Services;
using Versa.Core.Models; // Model ကို သုံးရန်

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BranchesController : BaseShopController
    {
        private readonly DatabaseService _dbService;
        public BranchesController(DatabaseService dbService) { _dbService = dbService; }

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
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            var saleCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Sales WHERE BranchId = @id", new { id });
            if (saleCount > 0)
            {
                return BadRequest(new { Message = "ဤဌာနခွဲတွင် အရောင်းမှတ်တမ်းများရှိနေသောကြောင့် ဖျက်၍မရပါ။ Inactive သာ လုပ်ပေးပါ။" });
            }

            await conn.ExecuteAsync("DELETE FROM Branches WHERE BranchId = @id", new { id });
            return Ok(new { Message = "Branch deleted successfully." });
        }
    }
}