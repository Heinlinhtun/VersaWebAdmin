using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class BundleItem
    {
        public int BundleItemId { get; set; }
        public int ParentProductId { get; set; }    // Bundle Product ၏ ID
        public int ComponentProductId { get; set; } // ထဲတွင်ပါဝင်သော Item သို့မဟုတ် Service ID
        public decimal Quantity { get; set; }       // ပါဝင်သော ပမာဏ
        public decimal UnitPrice { get; set; }      // Bundle ထဲတွင် တွက်ချက်မည့် စျေးနှုန်း
    }
}
