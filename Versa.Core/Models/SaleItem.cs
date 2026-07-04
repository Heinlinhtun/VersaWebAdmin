using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class SaleItem
    {
        public int SaleItemId { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public int? UnitId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SubTotal { get; set; }
        public string? Note { get; set; }
    }
}
