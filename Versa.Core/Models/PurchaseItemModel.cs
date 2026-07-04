namespace Versa.Core.Models
{
    public class PurchaseItemModel
    {
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public decimal Quantity { get; set; }

        // ✅ FOC အရေအတွက်အတွက် အသစ်ထည့်ရန်
        public decimal? FOCQuantity { get; set; }

        public decimal CostPrice { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int AlertDays { get; set; } = 30;
        public decimal NewSalePrice { get; set; }
    }
}