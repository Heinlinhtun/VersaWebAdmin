using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivityLogsController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public ActivityLogsController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // Log စာရင်းများကို ပြန်ကြည့်ခြင်း (Branch အလိုက်)
        [HttpGet("{branchId}")]
        public async Task<IActionResult> GetLogs(int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            string sql = @"
                SELECT l.*, u.Username 
                FROM ActivityLogs l
                JOIN ShopUsers u ON l.ShopUserId = u.UserId
                WHERE l.BranchId = @branchId
                ORDER BY l.CreatedAt DESC 
                LIMIT 100";

            return Ok(await conn.QueryAsync<dynamic>(sql, new { branchId }));
        }

        // Log အသစ်သွင်းခြင်း (Internal သို့မဟုတ် API မှတစ်ဆင့် သုံးနိုင်ရန်)
        [HttpPost]
        public async Task<IActionResult> CreateLog([FromBody] ActivityLogModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            string sql = @"INSERT INTO ActivityLogs (ShopUserId, BranchId, Action, Details, CreatedAt) 
                           VALUES (@ShopUserId, @BranchId, @Action, @Details, NOW())";

            await conn.ExecuteAsync(sql, model);
            return Ok();
        }
    }
}