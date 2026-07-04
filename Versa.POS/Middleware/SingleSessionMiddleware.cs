using System.Security.Claims;
using Versa.Core.Services;

namespace Versa.POS.Middleware;

/// <summary>
/// Enforces single-session per user. On each authenticated request the SessionToken
/// claim embedded in the JWT is compared against the value stored in ShopUsers.SessionToken.
/// If they differ (because a newer login has overwritten it), a 401 is returned.
/// The Versa.UI api.js interceptor already handles 401 by clearing localStorage and
/// redirecting to /login — no frontend changes needed.
/// </summary>
public class SingleSessionMiddleware
{
    private readonly RequestDelegate _next;

    public SingleSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DatabaseService dbService)
    {
        // Only check authenticated requests that carry a valid JWT
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdStr  = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tenantDb   = context.User.FindFirstValue("TenantDb");
            var jwtSession = context.User.FindFirstValue("SessionToken");

            // If any of these are missing the JWT is malformed — reject
            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(tenantDb) || string.IsNullOrEmpty(jwtSession))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Invalid session. Please log in again." });
                return;
            }

            if (!int.TryParse(userIdStr, out var userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Invalid session. Please log in again." });
                return;
            }

            // Compare JWT's session token with what's currently stored in the DB
            var storedToken = await dbService.GetSessionTokenAsync(tenantDb, userId);

            if (storedToken != jwtSession)
            {
                // A newer login has overwritten the stored token — this session is expired
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Session expired. Your account was logged in elsewhere. Please log in again."
                });
                return;
            }
        }

        await _next(context);
    }
}
