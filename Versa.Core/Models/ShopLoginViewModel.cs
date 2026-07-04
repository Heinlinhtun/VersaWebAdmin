using System;
using System.Collections.Generic;
using System.Text;

namespace Versa.Core.Models
{
    public class ShopLoginViewModel
    {
        public string ShopLoginId { get; set; } // ဆိုင်ကုဒ် (e.g., kpaypos)
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
