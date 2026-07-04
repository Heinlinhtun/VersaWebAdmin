using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Versa.POS.Controllers;
// Controllers/BaseShopController.cs


[Authorize]
public class BaseShopController : ControllerBase
{
    protected string TenantDbName
    {
        get
        {
            var dbName = User.FindFirst("TenantDb")?.Value;
            if (string.IsNullOrEmpty(dbName))
            {
                // DB Name မပါရင် Error တစ်ခုခု ပေးတာမျိုး ဒါမှမဟုတ် Default တစ်ခု သတ်မှတ်နိုင်ပါတယ်
                // Throw an exception that can be caught by a global exception handler or middleware
                // to return a 401 Unauthorized status code instead of a 500 Internal Server error.
                // For a base controller property, returning a string forces you to either throw
                // or return a default value. If you want to return an ActionResult, this logic
                // needs to be evaluated within the endpoint action itself or via an ActionFilter.
                throw new UnauthorizedAccessException("Tenant database identity not found in token.");
            }
            return dbName;
        }
    }
}