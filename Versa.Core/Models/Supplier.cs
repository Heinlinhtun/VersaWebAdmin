using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class Supplier
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string ContactPerson { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public decimal CurrentDebt { get; set; } // Supplier ကို ပေးရန်ရှိသော ငွေ
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
