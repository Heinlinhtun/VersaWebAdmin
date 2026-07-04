namespace Versa.Core.Models
{
    public class SaleItemModel
    {
        // Item အမျိုးအစားခွဲရန်: "Product", "Service", "Bundle"
        public string ItemType { get; set; } = "Product";

        public int? ProductId { get; set; }
        public int? ServiceId { get; set; }
        public int? BundleId { get; set; }

        public int? UnitId { get; set; } // Service အတွက်ဆိုလျှင် Null ဖြစ်နိုင်ပါသည်
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool IsFOC { get; set; }
        public decimal Multiplier { get; set; }
        public decimal CostPrice { get; set; } // အရောင်းစာရင်းထဲတွင် အရင်းကိုပါ သိမ်းရန်
        public decimal SubTotal { get; set; }
        public string? Note { get; set; }
        public bool IsConsignment { get; set; }
        public int? ConsignItemId { get; set; }
    }
}
