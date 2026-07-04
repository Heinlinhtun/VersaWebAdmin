using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class PurchaseReturnModel
    {

        public int ReturnId { get; set; }
        public int? POItemId { get; set; }
        public int POId { get; set; }         // မူလ Purchase Order ID
        public int ProductId { get; set; }
        public int BranchId { get; set; }
        public decimal Quantity { get; set; }
        public int UnitId { get; set; }
        public decimal RefundAmount { get; set; } // Supplier က ပြန်ပေးသည့်ငွေ
        public string? Reason { get; set; }
        public DateTime ReturnDate { get; set; } = DateTime.Now;
    }
}
