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
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
