using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Versa.Core.Services;
using Versa.POS.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Ensure JWT claims are not remapped by ASP.NET Core
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// --- ၁။ Services Registration (Build မလုပ်ခင် အကုန်ထည့်ရမည့်အပိုင်း) ---

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Database Service
builder.Services.AddScoped<DatabaseService>();

// CORS Policy Configuration (UI Project က လှမ်းခေါ်နိုင်ရန်)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// JWT Authentication Configuration
// appsettings.json ထဲက Key ကို သုံးတာ ပိုကောင်းပါတယ် (ဒါက လက်ရှိ model အတွက် ဥပမာပါ)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "Your_Secret_Key_Here_Must_Be_Long_Enough_12345";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero // Token သက်တမ်းကုန်တာနဲ့ ချက်ချင်း သိစေရန်
    };
});

// Swagger (Debug လုပ်ရလွယ်ကူရန် ထည့်ထားပေးပါသည်)
builder.Services.AddEndpointsApiExplorer();


// --- ၂။ Build Application ---
var app = builder.Build();

// --- ၃။ Middleware Pipeline (အစဉ်လိုက်အတိုင်း သွားရမည့်အပိုင်း) ---

app.UseHttpsRedirection();

// Global Exception Mapping Middleware
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    // Catch the specific exception thrown by BaseShopController when TenantDb is missing
    catch (UnauthorizedAccessException ex)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = ex.Message });
    }
    // For any other unexpected exceptions, you could handle them here as well
});

// Routing က အပေါ်ဆုံးမှာ ရှိရပါမယ်
app.UseRouting();

// CORS က Routing နဲ့ Authentication ကြားမှာ ရှိရပါမယ်
app.UseCors("AllowAll");

app.UseAuthentication(); // ဘယ်သူလဲ စစ်ဆေးခြင်း
app.UseMiddleware<SingleSessionMiddleware>(); // Single-session enforcement
app.UseAuthorization();  // ဘာလုပ်ပိုင်ခွင့်ရှိလဲ စစ်ဆေးခြင်း

app.MapControllers();

app.Run();