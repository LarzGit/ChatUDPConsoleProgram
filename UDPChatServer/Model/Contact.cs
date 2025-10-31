namespace UdpChatServer.Models
{
    /// <summary>
    /// Контакт пользователя (список контактов)
    /// </summary>
    public class Contact
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }       // Владелец списка контактов (ID пользователя)
        public int ContactUserId { get; set; } // ID пользователя-контакта
    }
}