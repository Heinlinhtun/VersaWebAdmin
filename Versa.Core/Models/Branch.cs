using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class Branch
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string Location { get; set; }
        public string Phone { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
