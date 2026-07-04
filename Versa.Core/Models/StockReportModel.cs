using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class StockReportModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string Sku { get; set; }
        public decimal TotalBaseQuantity { get; set; } // အသေးဆုံးယူနစ်ဖြင့် စုစုပေါင်းလက်ကျန်
        public string FormattedStock { get; set; } // ဥပမာ - "10 Boxes, 5 Pcs"
    }
}
