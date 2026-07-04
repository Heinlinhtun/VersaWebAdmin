using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class Unit
    {
        public int UnitId { get; set; }
        public string UnitName { get; set; }
        public string ShortName { get; set; } // e.g., kg, pcs, box
    }
}
