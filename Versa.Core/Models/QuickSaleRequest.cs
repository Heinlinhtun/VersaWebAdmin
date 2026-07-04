namespace Versa.Core.Models
{
    public class QuickSaleRequest
    {
        public string Name { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int? UnitId { get; set; }
        public string? UnitName { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal Quantity { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public int BranchId { get; set; }
        public string? Sku { get; set; }
    }
}
