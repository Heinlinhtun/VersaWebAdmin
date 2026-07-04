namespace Versa.Core.Models
{
    public class ActivityLogModel
    {
        public int ShopUserId { get; set; }
        public int BranchId { get; set; }
        public string Action { get; set; } // ဥပမာ - 'CREATE_SALE', 'DELETE_PRODUCT'
        public string Details { get; set; } // ဥပမာ - 'Sold Item ID: 5'
    }
}