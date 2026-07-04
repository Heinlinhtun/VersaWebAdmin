using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class Expense
    {
        public int ExpenseId { get; set; }
        public int BranchId { get; set; }
        public string Title { get; set; }
        public string Category { get; set; } // e.g., Staff Salary, Rent
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string PaymentMethod { get; set; }
        public string Note { get; set; }
    }
}
