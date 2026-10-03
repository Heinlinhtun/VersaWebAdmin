using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class ShopSettingViewModel
    {
        public int SettingId { get; set; }
        public string ShopName { get; set; }
        public string Address { get; set; }
        public string Phone { get; set; }
        public string LogoData { get; set; }
        public string CurrencySymbol { get; set; }
        public decimal TaxRate { get; set; }
        public string ReceiptFooter { get; set; }
        public bool UseDecimalQuantity { get; set; }
        public int QuantityDecimalPlaces { get; set; } = 2;
    }
}
