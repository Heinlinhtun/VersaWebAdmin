using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class ExpenseEntryModel
    {
        public int BranchId { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime? ExpenseDate { get; set; }
    }
}
