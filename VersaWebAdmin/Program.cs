using Microsoft.AspNetCore.Authentication.Cookies;
using MySqlConnector;
using Versa.Core.Services;
using VersaWebAdmin.Models;
using VersaWebAdmin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<EncryptionService>();
builder.Services.Configure<DropboxBackupOptions>(builder.Configuration.GetSection(DropboxBackupOptions.SectionName));
builder.Services.Configure<TelegramAlertOptions>(builder.Configuration.GetSection(TelegramAlertOptions.SectionName));
builder.Services.AddSingleton<IDropboxBackupUploadService, DropboxBackupUploadService>();
builder.Services.AddHttpClient<ITelegramAlertService, TelegramAlertService>();
builder.Services.AddScoped<ITenantBackupService, TenantBackupService>();
builder.Services.AddHostedService<DailyTenantBackupHostedService>();
// Add services to the container.
builder.Services.AddControllersWithViews();

// Cookie-based authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

var app = builder.Build();

// --- Seed default admin if no MasterUsers exist ---
var dbService = app.Services.CreateScope().ServiceProvider.GetRequiredService<DatabaseService>();
using (var conn = new MySqlConnection(dbService.GetMasterConnectionString()))
{
    await conn.OpenAsync();
    var count = (long)(await new MySqlCommand("SELECT COUNT(*) FROM MasterUsers", conn).ExecuteScalarAsync() ?? 0);
    if (count == 0)
    {
        string hash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
        var cmd = new MySqlCommand(
            "INSERT INTO MasterUsers (Username, PasswordHash, Role) VALUES (@u, @p, 'SuperAdmin')", conn);
        cmd.Parameters.AddWithValue("@u", "admin");
        cmd.Parameters.AddWithValue("@p", hash);
        await cmd.ExecuteNonQueryAsync();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=ShopList}/{id?}")
    .WithStaticAssets();

app.Run();
