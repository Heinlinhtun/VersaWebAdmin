using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySqlConnector;
using Dapper;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [Authorize] // Token ရှိသူများသာ အသုံးပြုခွင့်ရှိစေရန်
    [ApiController]
    [Route("api/[controller]")]
    public class UnitsController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public UnitsController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // ၁။ Units အားလုံးကို ဆွဲထုတ်ခြင်း
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            // BaseShopController မှ TenantDbName (Token ထဲမှ DB အမည်) ကို ယူသည်
            string connStr = _dbService.GetTenantConnectionString(TenantDbName);

            using var conn = new MySqlConnection(connStr);
            var units = await conn.QueryAsync<Unit>("SELECT * FROM Units");
            return Ok(units);
        }

        // ၂။ Unit အသစ်ထည့်ခြင်း
        [HttpPost]
        public async Task<IActionResult> Create(Unit unit)
        {
            string connStr = _dbService.GetTenantConnectionString(TenantDbName);

            using var conn = new MySqlConnection(connStr);
            string sql = @"INSERT INTO Units (UnitName, ShortName) 
                           VALUES (@UnitName, @ShortName); 
                           SELECT LAST_INSERT_ID();";

            var id = await conn.ExecuteScalarAsync<int>(sql, unit);
            unit.UnitId = id;
            return Ok(unit);
        }

        // ၃။ Unit ကို ပြင်ဆင်ခြင်း
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, Unit unit)
        {
            string connStr = _dbService.GetTenantConnectionString(TenantDbName);

            using var conn = new MySqlConnection(connStr);
            string sql = @"UPDATE Units 
                           SET UnitName = @UnitName, ShortName = @ShortName 
                           WHERE UnitId = @id";

            var affectedRows = await conn.ExecuteAsync(sql, new { unit.UnitName, unit.ShortName, id });

            if (affectedRows == 0) return NotFound(new { message = "Unit မရှိပါ" });
            return Ok(new { message = "Update Successful" });
        }

        // ၄။ Unit ကို ဖျက်ခြင်း
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            string connStr = _dbService.GetTenantConnectionString(TenantDbName);

            using var conn = new MySqlConnection(connStr);
            string sql = "DELETE FROM Units WHERE UnitId = @id";

            var affectedRows = await conn.ExecuteAsync(sql, new { id });

            if (affectedRows == 0) return NotFound(new { message = "Unit မရှိပါ" });
            return Ok(new { message = "Delete Successful" });
        }
    }
}