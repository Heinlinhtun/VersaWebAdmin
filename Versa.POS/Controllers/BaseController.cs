using Microsoft.AspNetCore.Mvc;
using Versa.Core.Services;

namespace Versa.POS.Controllers
{
    // Controllers/BaseController.cs
    // Controllers/BaseShopController.cs
    public class BaseController : ControllerBase
    {
        // Token ထဲမှာ ပါလာတဲ့ Tenant DB Name ကို ဆွဲထုတ်ပေးမယ့် Property
        protected string TenantDbName => User.FindFirst("TenantDb")?.Value ?? "";
        protected int CurrentBranchId => int.Parse(User.FindFirst("BranchId")?.Value ?? "0");
    }
}
