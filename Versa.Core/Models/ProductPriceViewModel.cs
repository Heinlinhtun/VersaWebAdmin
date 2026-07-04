namespace Versa.Core.Models
{
    public class ProductPriceViewModel
    {
        // Database Column နာမည်များအတိုင်း ပေးထားခြင်း
        public string PriceName { get; set; }
        public decimal PriceValue { get; set; }
        public decimal MinQuantity { get; set; }
        public int UnitId { get; set; }
    }
}