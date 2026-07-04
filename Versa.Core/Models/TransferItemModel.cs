namespace Versa.Core.Models
{
    public class TransferItemModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public int UnitId { get; set; } // လွှဲပြောင်းသည့်ယူနစ် (ဥပမာ - ၁ ဖာ)
    }
}