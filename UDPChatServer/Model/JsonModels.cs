using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UdpChatServer.Models
{
    public class RegisterRequest
    {
        public string Type { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
    }


    public class LoginRequest
    {
        public string Type { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
    }


    public class ContactRequest
    {
        public string Type { get; set; }
        public string OwnerLogin { get; set; }
        public string ContactLogin { get; set; }
    }


    public class JsonResponse
    {
        public string Type { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }
}