using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class SaleEntryModel
    {
        public int BranchId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? DiscountType { get; set; }
        public decimal? DiscountInput { get; set; }

        public int? DiscountId { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaymentMethod { get; set; }
        public string? ReferenceNo { get; set; }
        public string? ClientSaleId { get; set; }
        public DateTime? SaleDate { get; set; }
        public string? Note { get; set; }
        public string? VoucherNote { get; set; }
        public string? SalesChannel { get; set; }
        public string SaleType { get; set; } // 'Normal', 'Credit' စသဖြင့်
        public List<SaleItemModel> Items { get; set; }
        public List<PaymentDetailModel> Payments { get; set; }
    }


}
