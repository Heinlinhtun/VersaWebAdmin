using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class SaleReturnModel
    {
        public int ReturnId { get; set; }
        public int SaleId { get; set; }      // မူလအရောင်း Voucher ID
        public int ProductId { get; set; }
        public int BranchId { get; set; }    // ဘယ်ဆိုင်ခွဲကို ပြန်အပ်တာလဲ
        public decimal Quantity { get; set; } // ပြန်အပ်သည့် အရေအတွက်
        public int UnitId { get; set; }
        public decimal RefundAmount { get; set; } // Customer ကို ပြန်ပေးလိုက်ရသည့်ငွေ
        public string? Reason { get; set; }   // ဘာလို့ပြန်အပ်တာလဲ (ဥပမာ- မှားဝယ်လို့)
        public string? PreferredPaymentMethod { get; set; }
        public DateTime ReturnDate { get; set; } = DateTime.Now;
    }
}
