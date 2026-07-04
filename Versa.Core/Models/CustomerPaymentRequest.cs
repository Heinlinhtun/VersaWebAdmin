using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class CustomerPaymentRequest
    {
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";

        // ✅ အသစ်ဖြည့်စွက်ထားသော field များ
        public string? ReferenceNo { get; set; }
        public string? Note { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;
    }
}
