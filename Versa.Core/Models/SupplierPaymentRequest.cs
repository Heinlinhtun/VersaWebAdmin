namespace Versa.Core.Models
{
    public class SupplierPaymentRequest
    {
        public string? RequestId { get; set; }
        public int SupplierId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string? ReferenceNo { get; set; }
        public string? Note { get; set; }

        // Back-date သွင်းနိုင်ရန်
        public DateTime PaymentDate { get; set; }
    }
}
