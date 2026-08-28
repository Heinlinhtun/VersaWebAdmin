using System;
using System.Collections.Generic;
using System.Net.ServerSentEvents;
using System.Text;

namespace Versa.Core.Models
{
    public class Sale
    {
        public int SaleId { get; set; }
        public int? BranchId { get; set; }
        public int? CustomerId { get; set; }
        public int? UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? DiscountType { get; set; }
        public decimal? DiscountInput { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DueAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string VoucherNote { get; set; }
        public DateTime SaleDate { get; set; }
        public string SaleType { get; set; } // Normal, Credit, etc.
    }
}
