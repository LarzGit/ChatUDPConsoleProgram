using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UdpChatServer.Models;

namespace UdpChatServer
{
    public class ChatHandler
    {
        private readonly Dictionary<string, IPEndPoint> _clients;
        private readonly UdpClient _udpClient;
        private readonly DatabaseService _dbService;

        public ChatHandler(Dictionary<string, IPEndPoint> clients, UdpClient udpClient, DatabaseService dbService)
        {
            _clients = clients;
            _udpClient = udpClient;
            _dbService = dbService;
        }

        public async Task HandleIncomingMessage(string message, IPEndPoint senderEP)
        {
            try
            {
                using var doc = JsonDocument.Parse(message);
                var type = doc.RootElement.GetProperty("Type").GetString();
                if (type == "SendMessage")
                {
                    var msg = JsonSerializer.Deserialize<ChatMessage>(message);
                    if (msg == null || string.IsNullOrWhiteSpace(msg.Text)) return;

                    var sender = await _dbService.GetUserByLoginAsync(msg.SenderLogin);
                    if (sender == null) return;

                    var timestamp = DateTime.UtcNow;
                    await _dbService.AddMessageAsync(sender.Id, 0, msg.Text, timestamp); // 0 для групового чату

                    var notification = new
                    {
                        Type = "ReceiveMessage",
                        From = msg.SenderLogin,
                        Text = msg.Text,
                        Timestamp = timestamp
                    };

                    var json = JsonSerializer.Serialize(notification);
                    var data = Encoding.UTF8.GetBytes(json);

                    foreach (var client in _clients)
                    {
                        if (client.Key != msg.SenderLogin) // Не відправляти самому собі
                        {
                            await _udpClient.SendAsync(data, data.Length, client.Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка обробки повідомлення: {ex.Message}");
            }
        }

        public async Task NotifyContactsOnline(string login)
        {
            try
            {
                var user = await _dbService.GetUserByLoginAsync(login);
                if (user == null) return;

                var contacts = await _dbService.GetContactsAsync(login);
                var notification = new { Type = "UserOnline", Login = login };

                var json = JsonSerializer.Serialize(notification);
                var data = Encoding.UTF8.GetBytes(json);

                foreach (var contactLogin in contacts)
                {
                    if (_clients.TryGetValue(contactLogin, out var ep))
                    {
                        await _udpClient.SendAsync(data, data.Length, ep);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка сповіщення про онлайн: {ex.Message}");
            }
        }

        public async Task NotifyContactsOffline(string login)
        {
            try
            {
                var user = await _dbService.GetUserByLoginAsync(login);
                if (user == null) return;

                var contacts = await _dbService.GetContactsAsync(login);
                var notification = new { Type = "UserOffline", Login = login };

                var json = JsonSerializer.Serialize(notification);
                var data = Encoding.UTF8.GetBytes(json);

                foreach (var contactLogin in contacts)
                {
                    if (_clients.TryGetValue(contactLogin, out var ep))
                    {
                        await _udpClient.SendAsync(data, data.Length, ep);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка сповіщення про офлайн: {ex.Message}");
            }
        }
    }

    public class ChatMessage
    {
        public string Type { get; set; }
        public string SenderLogin { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
    }
}