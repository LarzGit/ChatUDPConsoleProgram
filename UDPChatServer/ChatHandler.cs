// Фрагмент кода для этапов 5–7
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using UdpChatServer;
using UdpChatServer.Models;

public class ChatHandler
{
    private readonly Dictionary<string, IPEndPoint> _onlineUsers;
    private readonly UdpClient _udpClient;

    public ChatHandler(Dictionary<string, IPEndPoint> onlineUsers, UdpClient udpClient)
    {
        _onlineUsers = onlineUsers;
        _udpClient = udpClient;
    }

    public async Task HandleIncomingMessage(string json, IPEndPoint senderEndPoint)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Type", out var typeProp)) return;

        string type = typeProp.GetString();

        switch (type)
        {
            case "SendMessage":
                await HandleSendMessage(root, senderEndPoint);
                break;

            case "GetHistory":
                await HandleGetHistory(root, senderEndPoint);
                break;

            // 🟢 Добавь вот эти два case:
            case "SearchContacts":
                await HandleSearchContacts(root, senderEndPoint);
                break;

            case "SearchMessages":
                await HandleSearchMessages(root, senderEndPoint);
                break;
        }
    }


    private async Task HandleSendMessage(JsonElement root, IPEndPoint senderEP)
    {
        var login = root.GetProperty("SenderLogin").GetString();
        var recipients = root.GetProperty("Recipients").EnumerateArray().Select(x => x.GetString()).ToList();
        var text = root.GetProperty("Text").GetString();
        var timestamp = root.GetProperty("Timestamp").GetDateTime();

        using var db = new AppDbContext();

        var senderUser = await db.Users.FirstOrDefaultAsync(u => u.Login == login);
        if (senderUser == null || !_onlineUsers.ContainsKey(login)) return;

        foreach (var rec in recipients)
        {
            if (rec == "Group")
            {
                var groupRecipients = db.Users.Where(u => u.Login != login).ToList();
                foreach (var u in groupRecipients)
                {
                    if (!_onlineUsers.ContainsKey(u.Login)) continue;
                    if (IsBlocked(db, senderUser.Id, u.Id)) continue;

                    await SendMessagePacket(u.Login, login, text, timestamp);
                    db.Messages.Add(new Message
                    {
                        SenderId = senderUser.Id,
                        ReceiverId = u.Id,
                        Content = text,
                        Timestamp = timestamp
                    });
                }
            }
            else
            {
                var receiver = db.Users.FirstOrDefault(u => u.Login == rec);
                if (receiver == null || !_onlineUsers.ContainsKey(rec)) continue;
                if (IsBlocked(db, senderUser.Id, receiver.Id)) continue;

                await SendMessagePacket(rec, login, text, timestamp);
                db.Messages.Add(new Message
                {
                    SenderId = senderUser.Id,
                    ReceiverId = receiver.Id,
                    Content = text,
                    Timestamp = timestamp
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private async Task SendMessagePacket(string recipientLogin, string senderLogin, string text, DateTime timestamp)
    {
        var packet = new
        {
            Type = "ReceiveMessage",
            From = senderLogin,
            Text = text,
            Timestamp = timestamp
        };
        var json = JsonSerializer.Serialize(packet);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _udpClient.SendAsync(bytes, bytes.Length, _onlineUsers[recipientLogin]);
    }

    private async Task HandleGetHistory(JsonElement root, IPEndPoint senderEP)
    {
        var login = root.GetProperty("SenderLogin").GetString();
        var contactLogin = root.TryGetProperty("ContactLogin", out var c) ? c.GetString() : null;

        using var db = new AppDbContext();
        var sender = db.Users.FirstOrDefault(u => u.Login == login);
        if (sender == null) return;

        List<Message> messages;

        if (!string.IsNullOrEmpty(contactLogin))
        {
            var contact = db.Users.FirstOrDefault(u => u.Login == contactLogin);
            if (contact == null) return;

            messages = db.Messages.Where(m =>
                (m.SenderId == sender.Id && m.ReceiverId == contact.Id) ||
                (m.SenderId == contact.Id && m.ReceiverId == sender.Id))
                .OrderBy(m => m.Timestamp)
                .ToList();
        }
        else
        {
            messages = db.Messages.Where(m => m.SenderId == sender.Id || m.ReceiverId == sender.Id)
                .OrderBy(m => m.Timestamp).ToList();
        }

        var result = messages.Select(m => new
        {
            From = db.Users.Find(m.SenderId)?.Login,
            To = db.Users.Find(m.ReceiverId)?.Login,
            m.Content,
            m.Timestamp
        });

        var packet = new
        {
            Type = "HistoryResult",
            Messages = result
        };

        var json = JsonSerializer.Serialize(packet);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _udpClient.SendAsync(bytes, bytes.Length, senderEP);
    }

    private bool IsBlocked(AppDbContext db, int senderId, int receiverId)
    {
        return db.BlacklistEntries.Any(b =>
            (b.OwnerId == senderId && b.BlockedUserId == receiverId) ||
            (b.OwnerId == receiverId && b.BlockedUserId == senderId));
    }

    public async Task NotifyContactsOnline(string login)
    {
        using var db = new AppDbContext();
        var user = db.Users.FirstOrDefault(u => u.Login == login);
        if (user == null) return;

        var contacts = db.Contacts.Where(c => c.OwnerId == user.Id).Select(c => c.ContactUserId).ToList();
        foreach (var contactId in contacts)
        {
            var contactUser = db.Users.Find(contactId);
            if (contactUser == null || !_onlineUsers.ContainsKey(contactUser.Login)) continue;
            if (IsBlocked(db, user.Id, contactUser.Id)) continue;

            var packet = new { Type = "UserOnline", Login = login };
            var json = JsonSerializer.Serialize(packet);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _udpClient.SendAsync(bytes, bytes.Length, _onlineUsers[contactUser.Login]);
        }

        db.LogEntries.Add(new LogEntry { UserId = user.Id, Event = "Login", Timestamp = DateTime.UtcNow });
        db.SaveChanges();
    }

    public async Task NotifyContactsOffline(string login)
    {
        using var db = new AppDbContext();
        var user = db.Users.FirstOrDefault(u => u.Login == login);
        if (user == null) return;

        var contacts = db.Contacts.Where(c => c.OwnerId == user.Id).Select(c => c.ContactUserId).ToList();
        foreach (var contactId in contacts)
        {
            var contactUser = db.Users.Find(contactId);
            if (contactUser == null || !_onlineUsers.ContainsKey(contactUser.Login)) continue;
            if (IsBlocked(db, user.Id, contactUser.Id)) continue;

            var packet = new { Type = "UserOffline", Login = login };
            var json = JsonSerializer.Serialize(packet);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _udpClient.SendAsync(bytes, bytes.Length, _onlineUsers[contactUser.Login]);
        }

        db.LogEntries.Add(new LogEntry { UserId = user.Id, Event = "Logout", Timestamp = DateTime.UtcNow });
        db.SaveChanges();
    }

    private async Task HandleSearchContacts(JsonElement root, IPEndPoint senderEP)
    {
        // Получаем поисковую строку из запроса
        var query = root.GetProperty("Query").GetString();
        using var db = new AppDbContext();

        // Ищем все пользователи, у которых логин содержит подстроку query (LINQ Contains поддерживается EF Core:contentReference[oaicite:0]{index=0})
        var results = db.Users
                        .Where(u => u.Login.Contains(query))
                        .Select(u => u.Login)
                        .ToList();

        // Формируем JSON-ответ с найденными логинами
        var response = new
        {
            Type = "SearchContactsResult",
            Results = results  // список строковых логинов
        };
        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _udpClient.SendAsync(bytes, bytes.Length, senderEP);
    }

    private async Task HandleSearchMessages(JsonElement root, IPEndPoint senderEP)
    {
        // Получаем поисковую строку и (опционально) логин отправителя
        var query = root.GetProperty("Query").GetString();
        var senderLogin = root.TryGetProperty("SenderLogin", out var sl) ? sl.GetString() : null;
        using var db = new AppDbContext();

        List<Message> messages;
        if (!string.IsNullOrEmpty(senderLogin))
        {
            // Ищем пользователя-отправителя
            var sender = db.Users.FirstOrDefault(u => u.Login == senderLogin);
            if (sender == null) return; // отправитель не найден
                                        // Фильтр: только сообщения от этого пользователя, содержащие query
            messages = db.Messages
                         .Where(m => m.SenderId == sender.Id && m.Content.Contains(query))
                         .OrderBy(m => m.Timestamp)
                         .ToList();
        }
        else
        {
            // Если SenderLogin не указан – ищем по всем сообщениям
            messages = db.Messages
                         .Where(m => m.Content.Contains(query))
                         .OrderBy(m => m.Timestamp)
                         .ToList();
        }

        // Формируем результат: перечисляем найденные сообщения
        var results = messages.Select(m => new
        {
            From = db.Users.Find(m.SenderId)?.Login,
            Text = m.Content,
            Timestamp = m.Timestamp
        }).ToList();

        var response = new
        {
            Type = "SearchMessagesResult",
            Messages = results  // список сообщений с полями From, Text, Timestamp
        };
        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _udpClient.SendAsync(bytes, bytes.Length, senderEP);
    }

}
