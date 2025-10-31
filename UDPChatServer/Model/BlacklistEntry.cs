namespace UdpChatServer.Models
{
    /// <summary>
    /// Запись из черного списка (блокировка пользователя)
    /// </summary>
    public class BlacklistEntry
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }        // Владелец черного списка (ID пользователя)
        public int BlockedUserId { get; set; }  // Заблокированный пользователь (ID)
    }
}