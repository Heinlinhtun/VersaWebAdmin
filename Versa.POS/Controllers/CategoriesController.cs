using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;
using Versa.POS.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CategoriesController : BaseShopController
{
    private readonly DatabaseService _dbService;
    public CategoriesController(DatabaseService dbService) => _dbService = dbService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var list = await conn.QueryAsync<Category>("SELECT * FROM Categories");
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Category model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = "INSERT INTO Categories (CategoryName) VALUES (@CategoryName); SELECT LAST_INSERT_ID();";
        model.CategoryId = await conn.ExecuteScalarAsync<int>(sql, model);
        return Ok(model);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Category model)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        string sql = "UPDATE Categories SET CategoryName = @CategoryName WHERE CategoryId = @id";
        await conn.ExecuteAsync(sql, new { model.CategoryName, id });
        return Ok(new { message = "Updated" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
        var usageCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Products WHERE CategoryId = @id AND IFNULL(IsActive, 1) = 1",
            new { id });

        if (usageCount > 0)
        {
            return BadRequest(new { message = "This category is used by products. It cannot be deleted." });
        }

        await conn.ExecuteAsync("DELETE FROM Categories WHERE CategoryId = @id", new { id });
        return Ok(new { message = "Deleted" });
    }
}
