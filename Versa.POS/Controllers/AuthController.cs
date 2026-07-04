using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Versa.Core.Models;
using Versa.Core.Services;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly DatabaseService _dbService;
    private readonly IConfiguration _config;

    public AuthController(DatabaseService dbService, IConfiguration config)
    {
        _dbService = dbService;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] ShopLoginViewModel model)
    {
        // ၁။ ဆိုင်ရှိမရှိ စစ်ဆေးခြင်း
        var tenant = await _dbService.GetTenantByLoginId(model.ShopLoginId);

        if (tenant == null)
            return Unauthorized(new { message = "ဆိုင်ကုဒ် (Shop ID) မမှန်ပါ။" });

        if (!tenant.IsActive)
            return Unauthorized(new { message = "ဤဆိုင်မှာ လက်ရှိပိတ်ထားပါသည်။" });

        if (tenant.SubscriptionEndDate < DateTime.Now)
            return Unauthorized(new { message = $"သင်၏သက်တမ်းမှာ {tenant.SubscriptionEndDate:dd-MM-yyyy} တွင် ကုန်ဆုံးသွားပါပြီ။" });

        // ၂။ User စစ်ဆေးခြင်း
        var user = await _dbService.GetShopUser(tenant.DbName, model.Username);

        // Password စစ်ဆေးခြင်း (BCrypt သုံးထားလျှင်)
        if (user != null && BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            // Generate a new session token — overwrites any previous session, forcing single-session
            var sessionToken = Guid.NewGuid().ToString("N");
            await _dbService.UpdateSessionTokenAsync(tenant.DbName, user.UserId, sessionToken);

            var token = GenerateJwtToken(user, tenant.DbName, model.ShopLoginId, sessionToken);
            var branchName = await _dbService.GetBranchNameAsync(tenant.DbName, user.BranchId);

            return Ok(new
            {
                token = token,
                UserId = user.UserId,
                username = user.Username,
                // Frontend က localStorage.getItem('Role') နဲ့ ဖတ်ဖို့အတွက်
                Role = user.Role,
                shopName = tenant.ShopName,
                // Frontend က localStorage.getItem('BranchId') နဲ့ ဖတ်ဖို့အတွက်
                BranchId = user.BranchId,
                BranchName = branchName
            });
        }

        return Unauthorized(new { message = "Username သို့မဟုတ် Password မှားနေပါသည်။" });
    }

    private string GenerateJwtToken(ShopUser user, string dbName, string shopId, string sessionToken)
    {
        var tokenHandler = new JwtSecurityTokenHandler();

        // Key ကို config ထဲကဖတ်လို့မရရင် default key တစ်ခု သုံးထားပေးပါ (သို့မဟုတ် Error ပြပါ)
        var secretKey = _config["Jwt:Key"] ?? "Your_Secret_Key_Here_Must_Be_Long_Enough_12345";
        var key = Encoding.ASCII.GetBytes(secretKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("TenantDb", dbName), // ဤ Claim သည် BaseShopController အတွက် အရေးကြီးပါသည်
                new Claim("ShopId", shopId),
                new Claim("BranchId", user.BranchId.ToString()),
                new Claim("SessionToken", sessionToken) // Single-session enforcement
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}