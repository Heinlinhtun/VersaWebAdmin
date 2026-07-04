using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class StockTransferModel
    {
        public int FromBranchId { get; set; }
        public int ToBranchId { get; set; }
        public int? UserId { get; set; }
        public string? Note { get; set; }
        public List<TransferItemModel> Items { get; set; }
    }
}
