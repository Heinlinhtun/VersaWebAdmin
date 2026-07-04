using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class SupplierPayment
    {
        public int PaymentId { get; set; }
        public int SupplierId { get; set; }
        public decimal PaidAmount { get; set; } // ပေးချေလိုက်သော ပမာဏ
        public string PaymentMethod { get; set; } // Cash, KPay, Bank Transfer
        public string ReferenceNo { get; set; } // ချက်လက်မှတ်နံပါတ် သို့မဟုတ် Transaction ID
        public string Note { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        // UI အတွက် Supplier အမည်ကို သိမ်းထားရန် (Optional)
        public string SupplierName { get; set; }
    }
}
