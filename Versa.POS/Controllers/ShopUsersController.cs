using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Services;
using Versa.Core.Models;
using System.Security.Claims;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShopUsersController : BaseShopController
    {
        private readonly DatabaseService _dbService;
        public ShopUsersController(DatabaseService dbService) { _dbService = dbService; }

        // ၁။ ဝန်ထမ်းအားလုံးစာရင်းကြည့်ရန် (Branch Name ပါ Join ထားသည်)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userBranchIdClaim = User.FindFirstValue("BranchId");
            int userBranchId = 0;
            if (!string.IsNullOrEmpty(userBranchIdClaim))
                int.TryParse(userBranchIdClaim, out userBranchId);

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            string sql;
            
            if (userRole == "Manager")
            {
                sql = @"
                    SELECT u.UserId, u.BranchId, u.Username, u.Role, u.IsActive, b.BranchName 
                    FROM ShopUsers u 
                    LEFT JOIN Branches b ON u.BranchId = b.BranchId 
                    WHERE u.BranchId = @userBranchId
                    ORDER BY u.IsActive DESC, u.UserId DESC";
                var users = await conn.QueryAsync<dynamic>(sql, new { userBranchId });
                return Ok(users);
            }
            else
            {
                sql = @"
                    SELECT u.UserId, u.BranchId, u.Username, u.Role, u.IsActive, b.BranchName 
                    FROM ShopUsers u 
                    LEFT JOIN Branches b ON u.BranchId = b.BranchId 
                    ORDER BY u.IsActive DESC, u.UserId DESC";
                var users = await conn.QueryAsync<dynamic>(sql);
                return Ok(users);
            }
        }

        // ၁.၁။ ဝန်ထမ်းတစ်ဦးတည်းအချက်အလက်ကို ID ဖြင့်ကြည့်ရန်
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userBranchIdClaim = User.FindFirstValue("BranchId");
            int userBranchId = 0;
            if (!string.IsNullOrEmpty(userBranchIdClaim))
                int.TryParse(userBranchIdClaim, out userBranchId);

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            string sql = @"
                SELECT u.UserId, u.BranchId, u.Username, u.Role, u.IsActive, b.BranchName 
                FROM ShopUsers u 
                LEFT JOIN Branches b ON u.BranchId = b.BranchId 
                WHERE u.UserId = @id";

            var user = await conn.QueryFirstOrDefaultAsync<dynamic>(sql, new { id });
            if (user == null) return NotFound(new { Message = "ဝန်ထမ်းကို ရှာမတွေ့ပါ။" });

            if (userRole == "Manager" && user.BranchId != userBranchId)
            {
                return Forbid();
            }

            return Ok(user);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ShopUser model)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userBranchIdClaim = User.FindFirstValue("BranchId");
            int userBranchId = 0;
            if (!string.IsNullOrEmpty(userBranchIdClaim))
                int.TryParse(userBranchIdClaim, out userBranchId);

            if (userRole == "Manager")
            {
                if (model.BranchId != userBranchId)
                    return BadRequest(new { Message = "မန်နေဂျာများသည် မိမိဆိုင်ခွဲ (Branch) တွင်သာ ဝန်ထမ်းအသစ်ထည့်နိုင်ပါသည်။" });
                
                if (model.Role == "Admin")
                    return BadRequest(new { Message = "မန်နေဂျာများသည် Admin အကောင့် ဖန်တီးခွင့်မရှိပါ။" });
            }

            // ၁။ User Limit စစ်ဆေးခြင်း
            int maxUsers = await _dbService.GetMaxUsersByDbName(TenantDbName);
            
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            var currentUserCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ShopUsers WHERE IsActive = 1");
            if (currentUserCount >= maxUsers)
            {
                return BadRequest(new { Message = $"Client အလိုက် ခွင့်ပြုထားသော အကောင့်အရေအတွက် ({maxUsers}) ပြည့်သွားပါပြီ။" });
            }

            var existingUser = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM ShopUsers WHERE Username = @Username", new { model.Username });

            if (existingUser > 0) return BadRequest(new { Message = "ဤ Username သည် ရှိပြီးသားဖြစ်နေပါသည်။" });

            // Password ကို Hash လုပ်ခြင်း
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            // Table Column များဖြစ်သော UserId, BranchId, Username, PasswordHash, Role, IsActive နှင့် ညှိထားပါသည်
            string sql = @"INSERT INTO ShopUsers (BranchId, Username, PasswordHash, Role, IsActive) 
                           VALUES (@BranchId, @Username, @passwordHash, @Role, @IsActive)";

            await conn.ExecuteAsync(sql, new
            {
                model.BranchId,
                model.Username,
                passwordHash,
                model.Role,
                model.IsActive
            });

            return Ok(new { Message = "ဝန်ထမ်းအသစ်ကို အောင်မြင်စွာ သိမ်းဆည်းပြီးပါပြီ။" });
        }

        // ၃။ ဝန်ထမ်းအချက်အလက် ပြင်ဆင်ရန်
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ShopUser model)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userBranchIdClaim = User.FindFirstValue("BranchId");
            int userBranchId = 0;
            if (!string.IsNullOrEmpty(userBranchIdClaim))
                int.TryParse(userBranchIdClaim, out userBranchId);

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            if (userRole == "Manager")
            {
                var existingUserDb = await conn.QueryFirstOrDefaultAsync<ShopUser>("SELECT BranchId, Role FROM ShopUsers WHERE UserId = @id", new { id });
                if (existingUserDb == null) return NotFound(new { Message = "ဝန်ထမ်းကို ရှာမတွေ့ပါ။" });

                if (existingUserDb.BranchId != userBranchId)
                    return BadRequest(new { Message = "မန်နေဂျာများသည် အခြားဆိုင်ခွဲမှ ဝန်ထမ်းအချက်အလက်များကို ပြင်ဆင်ခွင့်မရှိပါ။" });
                
                if (model.BranchId != userBranchId)
                    return BadRequest(new { Message = "မန်နေဂျာများသည် ဝန်ထမ်းများကို အခြားဆိုင်ခွဲသို့ ပြောင်းရွှေ့ခွင့်မရှိပါ။" });

                if (existingUserDb.Role == "Admin" || model.Role == "Admin")
                    return BadRequest(new { Message = "မန်နေဂျာများသည် Admin အကောင့် ပြင်ဆင် ဖန်တီးခွင့်မရှိပါ။" });
            }

            model.UserId = id;

            string sql;
            // Password ပါမှသာ Update လုပ်မည့် Logic
            if (!string.IsNullOrEmpty(model.Password))
            {
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                sql = @"UPDATE ShopUsers SET 
                        BranchId=@BranchId, 
                        Username=@Username, 
                        PasswordHash=@passwordHash, 
                        Role=@Role, 
                        IsActive=@IsActive 
                        WHERE UserId=@UserId";

                await conn.ExecuteAsync(sql, new
                {
                    model.BranchId,
                    model.Username,
                    passwordHash,
                    model.Role,
                    model.IsActive,
                    model.UserId
                });
            }
            else
            {
                // Password မပါလျှင် ရှိရင်းစွဲ PasswordHash ကို မထိဘဲ ကျန်တာပြင်ခြင်း
                sql = @"UPDATE ShopUsers SET 
                        BranchId=@BranchId, 
                        Username=@Username, 
                        Role=@Role, 
                        IsActive=@IsActive 
                        WHERE UserId=@UserId";

                await conn.ExecuteAsync(sql, new
                {
                    model.BranchId,
                    model.Username,
                    model.Role,
                    model.IsActive,
                    model.UserId
                });
            }

            return Ok(new { Message = "ပြင်ဆင်မှု အောင်မြင်ပါသည်။" });
        }

        // ၄။ ဝန်ထမ်းဖျက်ရန်
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var userBranchIdClaim = User.FindFirstValue("BranchId");
            int userBranchId = 0;
            if (!string.IsNullOrEmpty(userBranchIdClaim))
                int.TryParse(userBranchIdClaim, out userBranchId);

            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            var user = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT Role, BranchId FROM ShopUsers WHERE UserId = @id", new { id });
            if (user == null) return NotFound(new { Message = "ဝန်ထမ်းကို ရှာမတွေ့ပါ။" });
            
            if (user.Role == "Admin") return BadRequest(new { Message = "Admin အကောင့်ကို ဖျက်၍မရပါ။" });

            if (userRole == "Manager" && user.BranchId != userBranchId)
            {
                return BadRequest(new { Message = "မန်နေဂျာများသည် မိမိဆိုင်ခွဲ (Branch) ရှိ ဝန်ထမ်းများကိုသာ ဖျက်ခွင့်ရှိပါသည်။" });
            }

            await conn.ExecuteAsync("UPDATE ShopUsers SET IsActive = 0 WHERE UserId = @id", new { id });
            return Ok(new { Message = "ဝန်ထမ်းကို ဖျက်ပြီးပါပြီ။" });
        }
    }
}
