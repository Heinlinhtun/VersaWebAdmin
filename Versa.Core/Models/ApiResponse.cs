using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }
}
