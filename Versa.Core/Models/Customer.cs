using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class Customer
    {
        public int? CustomerId { get; set; }
        public string Name { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public decimal CurrentBalance { get; set; } // ဝယ်သူထံမှ ရရန်ရှိသော ငွေ (အကြွေး)

        // Previous Credit: credit owed before the shop started using the system.
        // Nullable so clients that don't send it never overwrite the stored value.
        public decimal? OpeningBalance { get; set; }
        public decimal OpeningBalancePaid { get; set; }
        public decimal OpeningRemaining { get; set; }
        public decimal SalesBalance { get; set; }
        // Required when changing an existing previous-credit amount so the change is auditable.
        public string? OpeningBalanceAdjustmentReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
