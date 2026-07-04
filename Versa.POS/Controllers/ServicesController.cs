using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;
using Versa.POS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : BaseShopController
{
    private readonly DatabaseService _dbService;
    public ServicesController(DatabaseService dbService) => _dbService = dbService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var services = await conn.QueryAsync("SELECT * FROM Services WHERE IsActive = 1");
        return Ok(services);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServiceEntryModel model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = "INSERT INTO Services (ServiceName, Price, Description) VALUES (@ServiceName, @Price, @Description)";

        // model ကို တိုက်ရိုက်သုံးခြင်းဖြင့် @ServiceName သည် model.ServiceName နှင့် အလိုအလျောက် ချိတ်ပါမည်
        await conn.ExecuteAsync(sql, model);
        return Ok(new { Message = "Service Created" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        await conn.ExecuteAsync("UPDATE Services SET IsActive = 0 WHERE ServiceId = @id", new { id });
        return Ok(new { Message = "Service Removed" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] ServiceEntryModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.ServiceName))
            return BadRequest(new { Message = "Service Name is required." });

        try
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));

            // ID ကိုပါ Parameter အနေနဲ့ ထည့်သွင်းပေးရပါမယ်
            string sql = @"UPDATE Services 
                       SET ServiceName = @ServiceName, 
                           Price = @Price, 
                           Description = @Description 
                       WHERE ServiceId = @ServiceId";

            // ExecuteAsync ထဲမှာ model ရော id ကိုပါ dynamic object အဖြစ် ပေါင်းထည့်ပေးပါ
            await conn.ExecuteAsync(sql, new
            {
                model.ServiceName,
                model.Price,
                model.Description,
                ServiceId = id
            });

            return Ok(new { Message = "Service Updated Successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = "Database Error: " + ex.Message });
        }
    }
}