using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class ReturnEntryModel
    {
        public int BranchId { get; set; } // ဘယ်ဆိုင်ခွဲက ပြန်တာလဲ
        public int ProductId { get; set; } // ပြန်မည့်ပစ္စည်း
        public int UnitId { get; set; } // ပြန်သည့်ယူနစ် (ဥပမာ - အထုပ် သို့မဟုတ် အလုံး)
        public decimal ReturnQty { get; set; } // ပြန်မည့်အရေအတွက်

        // Sales Return အတွက် လိုအပ်သည်များ
        public int? SaleId { get; set; }
        public int? CustomerId { get; set; }
        public decimal UnitPrice { get; set; } // ရောင်းဈေး (Customer Balance ပြန်နှုတ်ရန်)

        // Purchase Return အတွက် လိုအပ်သည်များ
        public int? POId { get; set; } // Purchase Order ID
        public int? SupplierId { get; set; }
        public decimal UnitCost { get; set; } // ဝယ်ဈေး (Supplier Debt ပြန်နှုတ်ရန်)

        public string Reason { get; set; } // ပြန်ရသည့်အကြောင်းအရင်း
    }
}
