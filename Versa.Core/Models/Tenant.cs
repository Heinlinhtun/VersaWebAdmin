namespace Versa.Core.Models;

// Models/Tenant.cs
public class Tenant
{
    public int TenantId { get; set; }
    public string ShopName { get; set; }
    public string ShopLoginId { get; set; }
    public string DbName { get; set; }
    public string SubscriptionPlan { get; set; }
    public DateTime SubscriptionStartDate { get; set; }
    public DateTime SubscriptionEndDate { get; set; }
    public bool IsActive { get; set; }
    public int MaxUsers { get; set; } = 5;
    public string? ShopType { get; set; }
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public DateTime CreatedAt { get; set; }

    // ကျန်ရှိရက်ကို တွက်ချက်ဖို့ Helper (Optional)
    public int DaysLeft => (SubscriptionEndDate - DateTime.Now).Days;
}

// ViewModel for Registration Form
public class RegisterShopViewModel
{
    public string ShopName { get; set; }      // ဆိုင်အမည် (ဥပမာ- စတိုးဆိုင်)
    public string ShopLoginId { get; set; }   // ဆိုင် Login ID (ဥပမာ- store01)
    public string AdminUsername { get; set; } // ပေးချင်တဲ့ Username
    public string AdminPassword { get; set; } // ပေးချင်တဲ့ Password
    public string? ShopType { get; set; }
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
}

// ViewModel for Edit Form
public class EditShopViewModel : Tenant
{
    public string? NewAdminPassword { get; set; }
}