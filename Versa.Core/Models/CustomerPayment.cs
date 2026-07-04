using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class CustomerPayment
    {
        public int PaymentId { get; set; }
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } // Cash, KPay, Wave
        public string Note { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // UI မှာ နာမည်ပြဖို့ (Optional)
        public string CustomerName { get; set; }
    }
}
