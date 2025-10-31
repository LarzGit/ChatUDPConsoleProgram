using System;

namespace UdpChatServer.Models
{
    /// <summary>
    /// Пользователь чата
    /// </summary>
    public class User
    {
        public int Id { get; set; }             // Идентификатор (PK)
        public string Login { get; set; }       // Логин
        public string PasswordHash { get; set; }// Хеш пароля
        public string Email { get; set; }       // Email
        public bool IsOnline { get; set; }      // Онлайн-статус
    }
}