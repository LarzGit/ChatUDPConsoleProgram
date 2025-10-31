using System;

namespace UdpChatServer.Models
{
    /// <summary>
    /// Сообщение чата
    /// </summary>
    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }    // Отправитель (ID пользователя)
        public int ReceiverId { get; set; }  // Получатель (ID пользователя)
        public string Content { get; set; }  // Текст сообщения
        public DateTime Timestamp { get; set; } // Время отправки
    }
}