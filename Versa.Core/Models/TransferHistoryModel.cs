using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class TransferHistoryViewModel
    {
        public int TransferId { get; set; }
        public string FromBranchName { get; set; }
        public string ToBranchName { get; set; }
        public DateTime TransferDate { get; set; }
        public string Status { get; set; }
        public string? UserName { get; set; }
        public string Note { get; set; }
    }
}
