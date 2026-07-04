using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    namespace Versa.Core.Models
    {
        public class StockAdjustmentModel
        {
            // ဘယ်ဆိုင်ခွဲက စတော့ကို ညှိမှာလဲ
            public int BranchId { get; set; }

            // ဘယ်ပစ္စည်း (Product) ကို ညှိမှာလဲ
            public int ProductId { get; set; }

            // ညှိမယ့် အမျိုးအစား - 'Addition' (ပေါင်းထည့်) သို့မဟုတ် 'Subtraction' (နှုတ်ပယ်)
            // SQL Dump အရ Type column အတွက် ဖြစ်ပါတယ်
            public string Type { get; set; }

            // ညှိနှိုင်းမည့် အရေအတွက် (Base Unit ဖြင့်သာ ဖြစ်ရပါမည်)
            public decimal Quantity { get; set; }
            public int UnitId { get; set; }

            // အကြောင်းပြချက် (ဥပမာ - ပစ္စည်းပျက်စီးခြင်း၊ စာရင်းစစ်ဆေးတွေ့ရှိချက်)
            public string Reason { get; set; }
            public int? UserId { get; set; }
        }
    }
}
