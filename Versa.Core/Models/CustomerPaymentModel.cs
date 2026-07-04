using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class CustomerPaymentModel
    {
        public int CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public int? SaleId { get; set; }
    }
}
