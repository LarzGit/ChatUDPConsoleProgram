using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using UdpChatServer.Models;
using Microsoft.EntityFrameworkCore;

namespace UdpChatServer
{
    public class UdpServer
    {
        private readonly UdpClient _udpClient;
        private readonly Dictionary<string, IPEndPoint> _clients = new Dictionary<string, IPEndPoint>();
        private readonly ChatHandler _chatHandler;
        private readonly DatabaseService _dbService;

        public UdpServer(int port)
        {
            _udpClient = new UdpClient(port);
            _dbService = new DatabaseService();
            _chatHandler = new ChatHandler(_clients, _udpClient, _dbService);
            Console.WriteLine($"UDP-сервер запущено на порту {port}");
        }

        public async Task StartAsync()
        {
            while (true)
            {
                IPEndPoint remoteEP = null;
                try
                {
                    var result = await _udpClient.ReceiveAsync();
                    remoteEP = result.RemoteEndPoint;
                    string message = Encoding.UTF8.GetString(result.Buffer);
                    Console.WriteLine($"Отримано від {remoteEP}: {message}");
                    using var doc = JsonDocument.Parse(message);
                    var type = doc.RootElement.GetProperty("Type").GetString();
                    switch (type)
                    {
                        case "Register":
                            var regReq = JsonSerializer.Deserialize<RegisterRequest>(message);
                            var (ok, msg) = await _dbService.RegisterUserAsync(regReq);
                            await SendResponse("RegisterResult", ok ? "Success" : "Fail", msg, remoteEP);
                            break;
                        case "Login":
                            var loginReq = JsonSerializer.Deserialize<LoginRequest>(message);
                            var user = await _dbService.LoginUserAsync(loginReq.Login, loginReq.Password);
                            if (user == null)
                            {
                                await SendResponse("LoginResult", "Fail", "Невірний логін або пароль", remoteEP);
                            }
                            else
                            {
                                _clients[loginReq.Login] = remoteEP;
                                Console.WriteLine($"Online users: {string.Join(", ", _clients.Keys)}");
                                await SendResponse("LoginResult", "Success", "Вхід успішний", remoteEP);
                                await _chatHandler.NotifyContactsOnline(loginReq.Login);
                            }
                            break;
                        case "Logout":
                            var logoutReq = JsonSerializer.Deserialize<LoginRequest>(message);
                            await _dbService.LogoutUserAsync(logoutReq.Login);
                            _clients.Remove(logoutReq.Login);
                            Console.WriteLine($"Online users: {string.Join(", ", _clients.Keys)}");
                            await _chatHandler.NotifyContactsOffline(logoutReq.Login);
                            await SendResponse("LogoutResult", "Success", "Вихід виконано", remoteEP);
                            break;
                        case "AddContact":
                            var addReq = JsonSerializer.Deserialize<ContactRequest>(message);
                            Console.WriteLine($"Processing AddContact for {addReq.OwnerLogin} -> {addReq.ContactLogin}");
                            var addRes = await _dbService.AddContactAsync(addReq.OwnerLogin, addReq.ContactLogin);
                            await SendResponse("AddContactResult", addRes.Success ? "Success" : "Fail", addRes.Message, remoteEP);
                            break;
                        case "RemoveContact":
                            var remReq = JsonSerializer.Deserialize<ContactRequest>(message);
                            Console.WriteLine($"Processing RemoveContact for {remReq.OwnerLogin} -> {remReq.ContactLogin}");
                            var remRes = await _dbService.RemoveContactAsync(remReq.OwnerLogin, remReq.ContactLogin);
                            await SendResponse("RemoveContactResult", remRes.Success ? "Success" : "Fail", remRes.Message, remoteEP);
                            break;
                        case "GetContacts":
                            var getReq = JsonSerializer.Deserialize<ContactRequest>(message);
                            Console.WriteLine($"Processing GetContacts for {getReq.OwnerLogin}");
                            var contacts = await _dbService.GetContactsAsync(getReq.OwnerLogin);
                            await SendResponse("GetContactsResult", "Success", "", remoteEP, new { Contacts = contacts });
                            break;
                        case "GetBlacklist": // Нова обробка
                            var blackReq = JsonSerializer.Deserialize<ContactRequest>(message);
                            Console.WriteLine($"Processing GetBlacklist for {blackReq.OwnerLogin}");
                            var blacklist = await _dbService.GetBlackListAsync(blackReq.OwnerLogin);
                            await SendResponse("GetBlacklistResult", "Success", "", remoteEP, new { Blacklist = blacklist });
                            break;
                        case "GetHistory":
                            var messages = await _dbService.GetAllMessagesAsync();
                            var history = new
                            {
                                Messages = messages.Take(100).Select(async m => new
                                {
                                    From = (await _dbService.GetUserByIdAsync(m.SenderId))?.Login ?? "Unknown",
                                    Content = m.Content,
                                    Timestamp = m.Timestamp
                                }).Select(t => t.Result).ToList()
                            };
                            await SendResponse("HistoryResult", "Success", "", remoteEP, history);
                            break;
                        default:
                            await _chatHandler.HandleIncomingMessage(message, remoteEP);
                            break;
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"UDP помилка: {ex.Message}");
                    if (remoteEP != null)
                        await SendResponse("Error", "Fail", "Сервер перевантажений", remoteEP);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Помилка обробки пакету: {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    if (remoteEP != null)
                        await SendResponse("Error", "Fail", "Помилка на сервері", remoteEP);
                }
            }
        }

        private async Task SendResponse(string type, string status, string message, IPEndPoint remoteEP, object data = null)
        {
            var res = new { Type = type, Status = status, Message = message, Data = data };
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(res));
            await _udpClient.SendAsync(bytes, bytes.Length, remoteEP);
        }
    }

    public class ContactRequest
    {
        public string Type { get; set; }
        public string OwnerLogin { get; set; }
        public string ContactLogin { get; set; }
    }

    public class LoginRequest
    {
        public string Type { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
    }

    public class RegisterRequest
    {
        public string Type { get; set; }
        public string Login { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Password { get; set; }
        public string PasswordConfirm { get; set; }
        public DateTime? Birthday { get; set; }
    }
}