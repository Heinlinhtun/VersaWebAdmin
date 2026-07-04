using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class InventoryStockModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal BaseQuantity { get; set; } // ProductStocks ထဲက Quantity
        public string BaseUnitName { get; set; } // Units ထဲက UnitName
        public decimal CostPrice { get; set; }    // Products ထဲက CostPrice (သို့) Cost
        public decimal SalePrice { get; set; }    // Products ထဲက RetailPrice
    }
}
