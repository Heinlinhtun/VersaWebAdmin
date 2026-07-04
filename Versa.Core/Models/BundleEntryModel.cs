using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class BundleEntryModel
    {
        public string BundleName { get; set; }
        public decimal BundlePrice { get; set; }
        public List<BundleComponentSelector> BundleItems { get; set; }
    }

    public class BundleComponentSelector
    {
        public string ItemType { get; set; } = "Product"; // Product | Service
        public int? ProductId { get; set; }
        public int? ServiceId { get; set; }
        public int? UnitId { get; set; }
        public decimal Quantity { get; set; }
    }
}
