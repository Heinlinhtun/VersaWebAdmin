using Dapper;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Versa.Core.Models;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExpensesController : BaseShopController
    {
        private readonly DatabaseService _dbService;

        public ExpensesController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        private static async Task EnsureExpenseSchemaAsync(MySqlConnection conn)
        {
            var exists = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Expenses' AND COLUMN_NAME = 'IsPaid'");
            if (exists == 0)
            {
                // Existing expenses were recorded as paid before this feature.
                try { await conn.ExecuteAsync("ALTER TABLE Expenses ADD COLUMN IsPaid TINYINT(1) NOT NULL DEFAULT 1"); }
                catch (MySqlException ex) when (ex.Number == 1060) { }
            }
        }

        private int? ResolveBranchScope(int? requestedBranchId)
        {
            if (User.IsInRole("Admin"))
            {
                return requestedBranchId > 0 ? requestedBranchId : null;
            }

            var branchClaim = User.FindFirst("BranchId")?.Value;
            return int.TryParse(branchClaim, out var branchId) ? branchId : requestedBranchId;
        }

        private int ResolveExpenseBranch(int requestedBranchId)
        {
            if (User.IsInRole("Admin"))
            {
                return requestedBranchId > 0 ? requestedBranchId : 1;
            }

            var branchClaim = User.FindFirst("BranchId")?.Value;
            return int.TryParse(branchClaim, out var branchId) && branchId > 0
                ? branchId
                : requestedBranchId;
        }

        // အသုံးစရိတ်အသစ်ထည့်ခြင်း
        [HttpPost]
        public async Task<IActionResult> AddExpense([FromBody] ExpenseEntryModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await EnsureExpenseSchemaAsync(conn);
            var branchId = ResolveExpenseBranch(model.BranchId);
            string sql = @"INSERT INTO Expenses (BranchId, Title, Category, Amount, PaymentMethod, ExpenseDate, IsPaid)
                           VALUES (@BranchId, @Title, @Category, @Amount, @PaymentMethod, @ExpenseDate, @IsPaid)";

            await conn.ExecuteAsync(sql, new
            {
                BranchId = branchId,
                model.Title,
                model.Category,
                model.Amount,
                model.IsPaid,
                PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod,
                ExpenseDate = model.ExpenseDate ?? DateTime.Now
            });
            return Ok(new { Message = "Expense recorded successfully." });
        }

        // ဆိုင်ခွဲအလိုက် အသုံးစရိတ်စာရင်း ပြန်ကြည့်ခြင်း
        [HttpGet("{branchId}")]
        public async Task<IActionResult> GetExpenses(int branchId)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await EnsureExpenseSchemaAsync(conn);
            var scopedBranchId = ResolveBranchScope(branchId);
            string sql = "SELECT * FROM Expenses WHERE (@branchId IS NULL OR BranchId = @branchId) ORDER BY ExpenseDate DESC";
            var expenses = await conn.QueryAsync(sql, new { branchId = scopedBranchId });
            return Ok(expenses);
        }
        // အသုံးစရိတ် ပြန်လည်ပြင်ဆင်ခြင်း (Update)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(int id, [FromBody] ExpenseEntryModel model)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            await EnsureExpenseSchemaAsync(conn);
            var branchId = ResolveExpenseBranch(model.BranchId);

            // Database ထဲမှာ အချက်အလက်များကို Update လုပ်ခြင်း
            string sql = @"UPDATE Expenses 
                   SET Title = @Title, 
                       Category = @Category, 
                       Amount = @Amount,
                       IsPaid = @IsPaid,
                       PaymentMethod = @PaymentMethod,
                       ExpenseDate = @ExpenseDate,
                       BranchId = @BranchId 
                   WHERE ExpenseId = @id";

            // Parameters တွေကို Anonymous object နဲ့ ပို့ပေးပါ
            await conn.ExecuteAsync(sql, new
            {
                model.Title,
                model.Category,
                model.Amount,
                model.IsPaid,
                PaymentMethod = string.IsNullOrWhiteSpace(model.PaymentMethod) ? "Cash" : model.PaymentMethod,
                ExpenseDate = model.ExpenseDate ?? DateTime.Now,
                BranchId = branchId,
                id
            });

            return Ok(new { Message = "Expense updated successfully." });
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            using var conn = new MySqlConnection(_dbService.GetTenantConnectionString(TenantDbName));
            string sql = "DELETE FROM Expenses WHERE ExpenseId = @id";
            await conn.ExecuteAsync(sql, new { id });
            return Ok(new { Message = "Expense deleted successfully." });
        }
    }
}
