using System;

namespace UdpChatServer.Models
{
    /// <summary>
    /// Запись входа/выхода пользователя (лог)
    /// </summary>
    public class LogEntry
    {
        public int Id { get; set; }
        public int UserId { get; set; }      // ID пользователя
        public string Event { get; set; }    // Событие ("Login" или "Logout")
        public DateTime Timestamp { get; set; } // Время события
    }
}