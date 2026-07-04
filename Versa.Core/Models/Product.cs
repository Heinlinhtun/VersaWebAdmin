using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{

    public class Product
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string Sku { get; set; }
        public int? CategoryId { get; set; }
        public int? UnitId { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public decimal AlertQuantity { get; set; }
        public bool IsActive { get; set; }
    }
}
