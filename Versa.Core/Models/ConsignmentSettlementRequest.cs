using System.Collections.Generic;

namespace Versa.Core.Models
{
    public class ConsignmentSettlementRequest
    {
        public int SupplierId { get; set; }
        public int? BranchId { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReferenceNo { get; set; }
        public string? Note { get; set; }
        public List<ConsignmentSettlementItemRequest> Items { get; set; } = new();
    }

    public class ConsignmentSettlementItemRequest
    {
        public int ConsignItemId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
    }
}
