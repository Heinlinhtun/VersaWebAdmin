namespace Versa.Core.Models
{
    public class ProductEntryViewModel
    {
        public string Name { get; set; } //
        public string Sku { get; set; } //
        public int? CategoryId { get; set; } //
        public string? CategoryName { get; set; } //
        public int? UnitId { get; set; } // Base Unit (ဥပမာ - "ခု")
        public string? UnitName { get; set; } //
        public decimal CostPrice { get; set; } //
        public decimal SalePrice { get; set; } //
        public decimal AlertQuantity { get; set; }//

        // ယူနစ်အလိုက် ဈေးနှုန်းမျိုးစုံ (ဥပမာ - "အထုပ်" အတွက် ဈေးတစ်ခု၊ "ကတ်" အတွက် ဈေးတစ်ခု)
        public List<UnitConversionViewModel> Conversions { get; set; } = new List<UnitConversionViewModel>(); //
        public List<ProductPriceViewModel> AdditionalPrices { get; set; }
    }

    public class UnitConversionViewModel
    {
        public int ProductId { get; set; } //
        public int FromUnitId { get; set; } // ပြောင်းလဲမည့်ယူနစ် ID (ဥပမာ - အထုပ် ID)
        public int ToUnitId { get; set; }   // အခြေခံယူနစ် ID (ဥပမာ - ခု ID)
        public decimal Multiplier { get; set; } // တစ်ထုပ်မှာ ဘယ်နှစ်ခုပါလဲ (ဥပမာ - 12)
        public decimal BaseSalePrice { get; set; } // ထိုယူနစ်အတွက် သီးသန့်ရောင်းဈေး
    }
}