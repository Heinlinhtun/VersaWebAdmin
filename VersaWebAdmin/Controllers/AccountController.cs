using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using System.Security.Claims;
using Versa.Core.Services;
using VersaWebAdmin.Models;

namespace VersaWebAdmin.Controllers;

public class AccountController : Controller
{
    private readonly DatabaseService _dbService;

    public AccountController(DatabaseService dbService)
    {
        _dbService = dbService;
    }

    // ─── Login ───────────────────────────────────────────────────────────────

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("ShopList", "Admin");

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        string? passwordHash = null;
        string? role = null;
        int userId = 0;

        using (var conn = new MySqlConnection(_dbService.GetMasterConnectionString()))
        {
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT UserId, PasswordHash, Role FROM MasterUsers WHERE Username = @u LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@u", model.Username.Trim());

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                userId = reader.GetInt32("UserId");
                passwordHash = reader.IsDBNull(reader.GetOrdinal("PasswordHash")) ? null : reader.GetString("PasswordHash");
                role = reader.IsDBNull(reader.GetOrdinal("Role")) ? "Admin" : reader.GetString("Role");
            }
        }

        if (passwordHash == null || !BCrypt.Net.BCrypt.Verify(model.Password, passwordHash))
        {
            ModelState.AddModelError("", "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, model.Username),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role ?? "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction("ShopList", "Admin");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    // ─── Settings ────────────────────────────────────────────────────────────

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var vm = new AccountSettingsViewModel();

        using var conn = new MySqlConnection(_dbService.GetMasterConnectionString());
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("SELECT UserId, Username, Role FROM MasterUsers ORDER BY UserId", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vm.Users.Add(new MasterUser
            {
                UserId = reader.GetInt32("UserId"),
                Username = reader.GetString("Username"),
                Role = reader.IsDBNull(reader.GetOrdinal("Role")) ? "Admin" : reader.GetString("Role")
            });
        }

        return View(vm);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(AccountSettingsViewModel vm)
    {
        if (!ModelState.IsValid)
            return await ReloadSettings(vm, "change");

        // Verify current password
        string? hash = null;
        using (var conn = new MySqlConnection(_dbService.GetMasterConnectionString()))
        {
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "SELECT PasswordHash FROM MasterUsers WHERE Username = @u LIMIT 1", conn);
            cmd.Parameters.AddWithValue("@u", User.Identity!.Name);
            hash = (string?)await cmd.ExecuteScalarAsync();
        }

        if (hash == null || !BCrypt.Net.BCrypt.Verify(vm.ChangePassword.CurrentPassword, hash))
        {
            TempData["ChangeError"] = "Current password is incorrect.";
            return RedirectToAction("Settings");
        }

        string newHash = BCrypt.Net.BCrypt.HashPassword(vm.ChangePassword.NewPassword);
        using (var conn = new MySqlConnection(_dbService.GetMasterConnectionString()))
        {
            await conn.OpenAsync();
            using var cmd = new MySqlCommand(
                "UPDATE MasterUsers SET PasswordHash = @p WHERE Username = @u", conn);
            cmd.Parameters.AddWithValue("@p", newHash);
            cmd.Parameters.AddWithValue("@u", User.Identity!.Name);
            await cmd.ExecuteNonQueryAsync();
        }

        TempData["ChangeSuccess"] = "Password changed successfully.";
        return RedirectToAction("Settings");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AccountSettingsViewModel vm)
    {
        // Check for duplicate username
        using (var conn = new MySqlConnection(_dbService.GetMasterConnectionString()))
        {
            await conn.OpenAsync();
            var exists = (long)(await new MySqlCommand(
                $"SELECT COUNT(*) FROM MasterUsers WHERE Username = @u", conn)
                { Parameters = { new MySqlParameter("@u", vm.CreateUser.Username.Trim()) } }
                .ExecuteScalarAsync() ?? 0);

            if (exists > 0)
            {
                TempData["CreateError"] = $"Username '{vm.CreateUser.Username}' already exists.";
                return RedirectToAction("Settings");
            }

            string newHash = BCrypt.Net.BCrypt.HashPassword(vm.CreateUser.Password);
            using var cmd = new MySqlCommand(
                "INSERT INTO MasterUsers (Username, PasswordHash, Role) VALUES (@u, @p, @r)", conn);
            cmd.Parameters.AddWithValue("@u", vm.CreateUser.Username.Trim());
            cmd.Parameters.AddWithValue("@p", newHash);
            cmd.Parameters.AddWithValue("@r", vm.CreateUser.Role);
            await cmd.ExecuteNonQueryAsync();
        }

        TempData["CreateSuccess"] = $"User '{vm.CreateUser.Username}' created successfully.";
        return RedirectToAction("Settings");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        // Don't allow deleting yourself
        var myId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (myId == userId.ToString())
        {
            TempData["DeleteError"] = "You cannot delete your own account.";
            return RedirectToAction("Settings");
        }

        using var conn = new MySqlConnection(_dbService.GetMasterConnectionString());
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("DELETE FROM MasterUsers WHERE UserId = @id", conn);
        cmd.Parameters.AddWithValue("@id", userId);
        await cmd.ExecuteNonQueryAsync();

        TempData["DeleteSuccess"] = "User deleted.";
        return RedirectToAction("Settings");
    }

    // Helper to reload settings view with validation errors preserved
    private async Task<IActionResult> ReloadSettings(AccountSettingsViewModel vm, string activeTab)
    {
        using var conn = new MySqlConnection(_dbService.GetMasterConnectionString());
        await conn.OpenAsync();
        using var cmd = new MySqlCommand("SELECT UserId, Username, Role FROM MasterUsers ORDER BY UserId", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            vm.Users.Add(new MasterUser
            {
                UserId = reader.GetInt32("UserId"),
                Username = reader.GetString("Username"),
                Role = reader.IsDBNull(reader.GetOrdinal("Role")) ? "Admin" : reader.GetString("Role")
            });
        }
        ViewData["ActiveTab"] = activeTab;
        return View("Settings", vm);
    }
}
