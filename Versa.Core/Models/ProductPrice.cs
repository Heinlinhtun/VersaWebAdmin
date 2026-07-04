using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class ProductPrice
    {
        public int PriceId { get; set; }
        public int ProductId { get; set; }
        public int UnitId { get; set; }
        public string PriceName { get; set; } = "Regular"; // Wholesale, VIP, etc.
        public decimal PriceValue { get; set; }
        public decimal MinQuantity { get; set; }
    }
}
