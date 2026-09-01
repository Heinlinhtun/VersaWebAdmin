using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{

    public class HistoryFilter
    {
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
        public int? BranchId { get; set; }
        public string Query { get; set; }
        public string Date { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? OrderStatus { get; set; }
        public string? CustomerName { get; set; }
        public string? SupplierName { get; set; }
    }
}
