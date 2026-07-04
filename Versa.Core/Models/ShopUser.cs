using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class ShopUser
    {
        public int UserId { get; set; }
        public int BranchId { get; set; }
        public string Username { get; set; }
        public string? PasswordHash { get; set; }

        // UI မှ Password အသစ်ရိုက်ထည့်ရန် (Database တွင် မသိမ်းပါ)
        public string? Password { get; set; }

        public string Role { get; set; }
        public bool IsActive { get; set; } = true;

        // Session tracking — overwritten on each login to invalidate prior sessions
        public string? SessionToken { get; set; }

        // Join Query မှလာမည့် Branch အမည် (Read-only)
        public string? BranchName { get; set; }
    }
}
