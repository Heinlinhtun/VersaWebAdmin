using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class SaleViewModel
    {
        public int CustomerId { get; set; }
        public string SaleType { get; set; } // Normal, Credit
        public List<SaleItemModel> Items { get; set; }
        // Split Payment အတွက် List ထည့်ခြင်း
        public List<PaymentDetailViewModel> Payments { get; set; }
    }
}
