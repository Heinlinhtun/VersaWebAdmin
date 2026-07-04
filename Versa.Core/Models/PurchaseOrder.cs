using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class PurchaseOrder
    {
        public int POId { get; set; } // Primary Key
        public int SupplierId { get; set; }
        public int BranchId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string Status { get; set; } // 'Pending', 'Completed' စသဖြင့်
        public DateTime OrderDate { get; set; }
    }
}
