using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using UdpChatServer.Models;
using System.Text.Json;


namespace UdpChatServer
{
    /// <summary>
    /// Простой UDP-сервер для обработки входящих сообщений
    /// </summary>
    public class UdpServer
    {
        private readonly UdpClient _udpClient;
        private readonly Dictionary<string, IPEndPoint> _clients = new Dictionary<string, IPEndPoint>();
        private ChatHandler _chatHandler;


        public UdpServer(int port)
        {
            // Открываем сокет на указанном порту для приема UDP-пакетов
            _udpClient = new UdpClient(port);
            _chatHandler = new ChatHandler(_clients, _udpClient);

        }

        /// <summary>
        /// Запустить сервер и обрабатывать сообщения бесконечно
        /// </summary>
        public async Task StartAsync()
        {
            while (true)
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);
                IPEndPoint remoteEP = result.RemoteEndPoint;

                Console.WriteLine($"Получено от {remoteEP}: {message}");

                try
                {
                    // 1️⃣ Получаем текст и определяем тип запроса
                    string json = Encoding.UTF8.GetString(result.Buffer);
                    Console.WriteLine($"Получено от {remoteEP}: {json}");

                    using var db = new AppDbContext();
                    var doc = JsonDocument.Parse(json);
                    var type = doc.RootElement.GetProperty("Type").GetString();

                    // 2️⃣ Обработка разных типов команд
                    if (type == "Register")
                    {
                        var data = JsonSerializer.Deserialize<RegisterRequest>(json);
                        var existing = db.Users.FirstOrDefault(u => u.Login == data.Login);
                        if (existing != null)
                        {
                            await SendResponse("RegisterResult", "Fail", "Логин уже занят", remoteEP);
                            continue;
                        }

                        PasswordHelper.HashPassword(data.Password, out var salt, out var hash);
                        var user = new User { Login = data.Login, Salt = salt, PasswordHash = hash, IsOnline = false };
                        db.Users.Add(user);
                        db.SaveChanges();

                        await SendResponse("RegisterResult", "Success", "Регистрация выполнена", remoteEP);
                    }
                    else if (type == "Login")
                    {
                        var data = JsonSerializer.Deserialize<LoginRequest>(json);
                        var user = db.Users.FirstOrDefault(u => u.Login == data.Login);
                        if (user == null || !PasswordHelper.VerifyPassword(data.Password, user.Salt, user.PasswordHash))
                        {
                            await SendResponse("LoginResult", "Fail", "Неверный логин или пароль", remoteEP);
                            continue;
                        }

                        user.IsOnline = true;
                        db.LogEntries.Add(new LogEntry { UserId = user.Id, Event = "Login", Timestamp = DateTime.Now });
                        db.SaveChanges();

                        _clients[remoteEP.ToString()] = remoteEP;
                        await SendResponse("LoginResult", "Success", "Вход выполнен", remoteEP);
                        // Оповещаем контакты, что пользователь вошёл
                        await _chatHandler.NotifyContactsOnline(user.Login);
                    }
                    else if (type == "AddContact")
                    {
                        var data = JsonSerializer.Deserialize<ContactRequest>(json);
                        var owner = db.Users.FirstOrDefault(u => u.Login == data.OwnerLogin);
                        var contact = db.Users.FirstOrDefault(u => u.Login == data.ContactLogin);
                        if (owner == null || contact == null)
                        {
                            await SendResponse("AddContactResult", "Fail", "Пользователь не найден", remoteEP);
                            continue;
                        }

                        db.Contacts.Add(new Contact { OwnerId = owner.Id, ContactUserId = contact.Id });
                        db.SaveChanges();
                        await SendResponse("AddContactResult", "Success", "Контакт добавлен", remoteEP);
                    }
                    else if (type == "RemoveContact")
                    {
                        var data = JsonSerializer.Deserialize<ContactRequest>(json);
                        var owner = db.Users.FirstOrDefault(u => u.Login == data.OwnerLogin);
                        var contact = db.Users.FirstOrDefault(u => u.Login == data.ContactLogin);
                        var link = db.Contacts.FirstOrDefault(c => c.OwnerId == owner.Id && c.ContactUserId == contact.Id);

                        if (link != null)
                        {
                            db.Contacts.Remove(link);
                            db.SaveChanges();
                            await SendResponse("RemoveContactResult", "Success", "Контакт удалён", remoteEP);
                        }
                        else
                        {
                            await SendResponse("RemoveContactResult", "Fail", "Контакт не найден", remoteEP);
                        }
                    }
                    else
                    {
                        // Передаём остальные команды (этапы 5–7) в отдельный обработчик ChatHandler
                        await _chatHandler.HandleIncomingMessage(message, remoteEP);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка обработки пакета: {ex.Message}");
                    await SendResponse("Error", "Fail", "Ошибка на сервере", result.RemoteEndPoint);
                }
            }

        }

        private async Task SendResponse(string type, string status, string message, IPEndPoint remoteEP)
        {
            var res = new JsonResponse { Type = type, Status = status, Message = message };
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(res));
            await _udpClient.SendAsync(bytes, bytes.Length, remoteEP);
        }
    }
}
