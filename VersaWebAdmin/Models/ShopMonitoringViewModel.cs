namespace VersaWebAdmin.Models;

/// <summary>
/// Represents monitoring data for a single shop/tenant.
/// </summary>
public class ShopMonitoringViewModel
{
    // --- Shop Identity ---
    public int TenantId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ShopLoginId { get; set; } = string.Empty;
    public string DbName { get; set; } = string.Empty;
    public string SubscriptionPlan { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // --- Online Status ---
    /// <summary>Whether any user from this shop has an active session (SessionToken is not null/empty).</summary>
    public bool IsOnline { get; set; }
    /// <summary>Number of users currently holding an active session token.</summary>
    public int OnlineUserCount { get; set; }
    /// <summary>Total number of users (active + inactive) in this shop.</summary>
    public int TotalUserCount { get; set; }

    // --- Activity ---
    /// <summary>Date of the most recent activity log entry, or null if none.</summary>
    public DateTime? LastActivityDate { get; set; }
    /// <summary>Days since the last recorded activity. -1 if no activity ever.</summary>
    public int DaysSinceLastActivity =>
        LastActivityDate.HasValue ? (DateTime.Now - LastActivityDate.Value).Days : -1;

    // --- Data / Usage ---
    /// <summary>Approximate database size in megabytes.</summary>
    public double DbSizeMb { get; set; }
    /// <summary>Total number of sales transactions.</summary>
    public long TotalSales { get; set; }
    /// <summary>Total number of products.</summary>
    public long TotalProducts { get; set; }
    /// <summary>Total number of customers.</summary>
    public long TotalCustomers { get; set; }

    // --- Subscription ---
    public DateTime SubscriptionStartDate { get; set; }
    public DateTime SubscriptionEndDate { get; set; }
    public int DaysLeft => (SubscriptionEndDate - DateTime.Now).Days;
    public int MaxUsers { get; set; }
}
