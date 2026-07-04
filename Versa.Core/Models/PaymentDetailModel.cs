namespace Versa.Core.Models
{
    public class PaymentDetailModel
    {
        public string PaymentMethod { get; set; } // Cash, KPay, Banking, etc.
        public decimal Amount { get; set; }
    }
}