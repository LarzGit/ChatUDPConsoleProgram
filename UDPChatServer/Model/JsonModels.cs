using System;

namespace UdpChatServer.Models
{
    public class RegisterRequest
    {
        public string Type { get; set; }
        public string Login { get; set; }
        public string Email { get; set; }      // нове
        public string Name { get; set; }       // нове
        public string Surname { get; set; }    // нове
        public string Password { get; set; }
        public string PasswordConfirm { get; set; } // нове
        public DateTime? Birthday { get; set; }     // нове
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
        public string Status { get; set; } // "Success" / "Fail"
        public string Message { get; set; }
        public object Data { get; set; }   // додаткові дані (опціонально)
    }
}
