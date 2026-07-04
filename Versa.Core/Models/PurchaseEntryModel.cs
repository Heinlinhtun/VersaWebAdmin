using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class PurchaseEntryModel
    {
        public int SupplierId { get; set; }
        public int BranchId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? PaidAmount { get; set; } // Database ရှိ PaidAmount column အတွက် ထည့်သွင်းရန်
        public string? PaymentMethod { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public string? Remarks { get; set; }
        public List<PurchaseItemModel> Items { get; set; }
    }
}
