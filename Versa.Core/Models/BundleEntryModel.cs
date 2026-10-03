using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class BundleEntryModel
    {
        public string BundleName { get; set; } = "";
        public decimal BundlePrice { get; set; }
        public bool AddAsProduct { get; set; }
        public int? BranchId { get; set; }
        public int? ProductionUnitId { get; set; }
        public decimal ProductionQuantity { get; set; }
        public List<BundleComponentSelector> BundleItems { get; set; } = new();
    }

    public class BundleProductionAvailabilityModel
    {
        public int? BranchId { get; set; }
        public List<BundleComponentSelector> BundleItems { get; set; } = new();
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
