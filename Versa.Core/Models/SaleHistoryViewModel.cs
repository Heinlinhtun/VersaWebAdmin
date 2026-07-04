using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class SaleHistoryViewModel
    {
        public int SaleId { get; set; }
        public string CustomerName { get; set; }
        public string CashierName { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DueAmount { get; set; }
        public string SaleType { get; set; }
        public DateTime SaleDate { get; set; }
    }
}
