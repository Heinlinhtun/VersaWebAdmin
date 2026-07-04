using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class Setting
    {
        public int SettingId { get; set; }
        public string? ShopName { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? LogoData { get; set; }
        public string? CurrencySymbol { get; set; } = "Ks";
        public decimal TaxRate { get; set; } = 0;
        public string? ReceiptFooter { get; set; }
        public string LogoSize { get; set; } = "Large";
        public string ReceiptFontSize { get; set; } = "Medium";
        public bool UseDecimalQuantity { get; set; } = false;
    }
}
