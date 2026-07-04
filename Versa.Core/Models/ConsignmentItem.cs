namespace Versa.Core.Models
{
    // Versa.Core/Models/ConsignmentItem.cs
    public class ConsignmentItem
    {
        public int ConsignItemId { get; set; }
        public int? ConsignId { get; set; }
        public int? ProductId { get; set; }
        public int? UnitId { get; set; }
        public int? BranchId { get; set; }
        public decimal Quantity { get; set; }
        public decimal SoldQuantity { get; set; }
        public decimal SettledQuantity { get; set; }
        public decimal ReturnedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
