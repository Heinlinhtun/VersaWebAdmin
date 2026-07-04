using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class Consignment
    {
        public int ConsignId { get; set; }
        public int SupplierId { get; set; }
        public int? BranchId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } // Active, Settled
        public string? Remark { get; set; }
        public List<ConsignmentItem> Items { get; set; }
    }
}
