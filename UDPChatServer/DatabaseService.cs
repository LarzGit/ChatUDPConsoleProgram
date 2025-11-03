using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UdpChatServer.Models;

namespace UdpChatServer
{
    public class DatabaseService : IDisposable
    {
        private readonly AppDbContext _db;

        public DatabaseService()
        {
            _db = new AppDbContext();
            _db.Database.EnsureCreated(); // Створить таблиці, якщо їх немає
            Console.WriteLine("Таблиці бази даних ініціалізовані.");
        }

        // ===================== РЕЄСТРАЦІЯ =====================
        public async Task<(bool Success, string Message)> RegisterUserAsync(RegisterRequest req)
        {
            if (req == null)
                return (false, "Некоректні дані реєстрації.");
            if (string.IsNullOrWhiteSpace(req.Login) || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return (false, "Заповніть обов'язкові поля: логін, email, пароль.");
            if (req.Password != req.PasswordConfirm)
                return (false, "Паролі не співпадають.");
            if (req.Password.Length < 8)
                return (false, "Пароль повинен бути щонайменше 8 символів.");
            if (!req.Email.Contains("@") || req.Email.Length < 5)
                return (false, "Некоректний формат email.");
            if (await _db.Users.AnyAsync(u => u.Login == req.Login))
                return (false, "Логін вже зайнятий.");
            if (await _db.Users.AnyAsync(u => u.Email == req.Email))
                return (false, "Email вже використовується.");
            PasswordHelper.HashPassword(req.Password, out var salt, out var hash);
            var user = new User
            {
                Login = req.Login,
                Email = req.Email,
                Name = req.Name,
                Surname = req.Surname,
                PasswordHash = hash,
                Salt = salt,
                Birthday = req.Birthday,
                CreatedAt = DateTime.UtcNow,
                IsOnline = false
            };
            try
            {
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                Console.WriteLine($"Користувач {req.Login} успішно зареєстрований.");
                return (true, "Реєстрація пройшла успішно.");
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine("DB Update Exception при реєстрації: " + dbEx.Message);
                if (dbEx.InnerException != null)
                    Console.WriteLine("Inner: " + dbEx.InnerException.Message);
                var innerMsg = dbEx.InnerException?.Message ?? dbEx.Message;
                if (innerMsg.Contains("Duplicate entry"))
                {
                    if (innerMsg.Contains("Login") || innerMsg.Contains("login"))
                        return (false, "Логін вже зайнятий.");
                    if (innerMsg.Contains("Email") || innerMsg.Contains("email"))
                        return (false, "Email вже використовується.");
                    return (false, "Користувач з такими даними вже існує.");
                }
                return (false, "Помилка при записі в базу даних.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception при реєстрації: " + ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine("Inner: " + ex.InnerException.Message);
                return (false, "Виникла помилка на сервері під час реєстрації.");
            }
        }

        // ===================== LOGIN / LOGOUT =====================
        public async Task<User> LoginUserAsync(string login, string password)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == login);
            if (user == null) return null;
            if (!PasswordHelper.VerifyPassword(password, user.Salt, user.PasswordHash))
                return null;
            user.IsOnline = true;
            _db.LogEntries.Add(new LogEntry { UserId = user.Id, Event = "Login", Timestamp = DateTime.UtcNow });
            await _db.SaveChangesAsync();
            Console.WriteLine($"Користувач {login} увійшов.");
            return user;
        }

        public async Task LogoutUserAsync(string login)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == login);
            if (user == null) return;
            user.IsOnline = false;
            _db.LogEntries.Add(new LogEntry { UserId = user.Id, Event = "Logout", Timestamp = DateTime.UtcNow });
            await _db.SaveChangesAsync();
            Console.WriteLine($"Користувач {login} вийшов.");
        }

        public async Task<User> GetUserByLoginAsync(string login)
        {
            return await _db.Users.FirstOrDefaultAsync(u => u.Login == login);
        }

        public async Task<User> GetUserByIdAsync(int id)
        {
            return await _db.Users.FindAsync(id);
        }

        // ===================== CONTACTS =====================
        public async Task<(bool Success, string Message)> AddContactAsync(string ownerLogin, string contactLogin)
        {
            var owner = await GetUserByLoginAsync(ownerLogin);
            var contact = await GetUserByLoginAsync(contactLogin);
            if (owner == null || contact == null)
                return (false, "Користувач або контакт не знайдені.");
            if (await IsBlockedAsync(owner.Id, contact.Id))
                return (false, "Цей користувач у твоєму чорному списку.");
            if (await _db.Contacts.AnyAsync(c => c.OwnerId == owner.Id && c.ContactUserId == contact.Id))
                return (false, "Контакт уже існує.");
            _db.Contacts.Add(new Contact { OwnerId = owner.Id, ContactUserId = contact.Id });
            try
            {
                await _db.SaveChangesAsync();
                Console.WriteLine($"Контакт {contactLogin} додано до {ownerLogin}.");
                return (true, "Контакт додано.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при додаванні контакту: {ex.Message}");
                return (false, "Помилка при додаванні контакту.");
            }
        }

        public async Task<(bool Success, string Message)> RemoveContactAsync(string ownerLogin, string contactLogin)
        {
            var owner = await GetUserByLoginAsync(ownerLogin);
            var contact = await GetUserByLoginAsync(contactLogin);
            if (owner == null || contact == null)
                return (false, "Користувач або контакт не знайдені.");
            var entry = await _db.Contacts.FirstOrDefaultAsync(c => c.OwnerId == owner.Id && c.ContactUserId == contact.Id);
            if (entry == null)
                return (false, "Контакт не знайдено.");
            _db.Contacts.Remove(entry);
            try
            {
                await _db.SaveChangesAsync();
                Console.WriteLine($"Контакт {contactLogin} видалено з {ownerLogin}.");
                return (true, "Контакт видалено.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка при видаленні контакту: {ex.Message}");
                return (false, "Помилка при видаленні контакту.");
            }
        }

        public async Task<List<string>> GetContactsAsync(string ownerLogin)
        {
            var owner = await GetUserByLoginAsync(ownerLogin);
            if (owner == null) return new List<string>();
            var ids = await _db.Contacts
                .Where(c => c.OwnerId == owner.Id)
                .Select(c => c.ContactUserId)
                .ToListAsync();
            var contacts = await _db.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => u.Login)
                .ToListAsync();
            Console.WriteLine($"Контакти для {ownerLogin}: {string.Join(", ", contacts)}");
            return contacts;
        }

        // ===================== BLACKLIST =====================
        public async Task AddToBlacklistAsync(string ownerLogin, string blockedLogin)
        {
            var owner = await GetUserByLoginAsync(ownerLogin);
            var blocked = await GetUserByLoginAsync(blockedLogin);
            if (owner == null || blocked == null)
            {
                Console.WriteLine($"Помилка: користувач {ownerLogin} або {blockedLogin} не знайдений.");
                return;
            }
            if (!await _db.BlacklistEntries.AnyAsync(b => b.OwnerId == owner.Id && b.BlockedUserId == blocked.Id))
            {
                _db.BlacklistEntries.Add(new BlacklistEntry { OwnerId = owner.Id, BlockedUserId = blocked.Id });
                try
                {
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"Користувач {blockedLogin} додано в чорний список {ownerLogin}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Помилка при додаванні в чорний список: {ex.Message}");
                }
            }
        }

        public async Task RemoveFromBlacklistAsync(string ownerLogin, string blockedLogin)
        {
            var owner = await GetUserByLoginAsync(ownerLogin);
            var blocked = await GetUserByLoginAsync(blockedLogin);
            if (owner == null || blocked == null) return;
            var entry = await _db.BlacklistEntries.FirstOrDefaultAsync(b => b.OwnerId == owner.Id && b.BlockedUserId == blocked.Id);
            if (entry != null)
            {
                _db.BlacklistEntries.Remove(entry);
                try
                {
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"Користувач {blockedLogin} видалено з чорного списку {ownerLogin}.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Помилка при видаленні з чорного списку: {ex.Message}");
                }
            }
        }

        public async Task<bool> IsBlockedAsync(int user1Id, int user2Id)
        {
            return await _db.BlacklistEntries.AnyAsync(b =>
                (b.OwnerId == user1Id && b.BlockedUserId == user2Id) ||
                (b.OwnerId == user2Id && b.BlockedUserId == user1Id));
        }

        public async Task<List<string>> GetBlackListAsync(string ownerLogin)
        {
            var owner = await GetUserByLoginAsync(ownerLogin);
            if (owner == null) return new List<string>();
            var ids = await _db.BlacklistEntries
                .Where(b => b.OwnerId == owner.Id)
                .Select(b => b.BlockedUserId)
                .ToListAsync();
            var blacklisted = await _db.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => u.Login)
                .ToListAsync();
            Console.WriteLine($"Чорний список для {ownerLogin}: {string.Join(", ", blacklisted)}");
            return blacklisted;
        }

        // ===================== MESSAGES =====================
        public async Task AddMessageAsync(int senderId, int receiverId, string content, DateTime timestamp)
        {
            var isBlocked = await IsBlockedAsync(senderId, receiverId);
            if (isBlocked)
            {
                Console.WriteLine($"Повідомлення від {senderId} до {receiverId} заблоковане.");
                return;
            }
            _db.Messages.Add(new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                Timestamp = timestamp
            });
            await _db.SaveChangesAsync();
            Console.WriteLine($"Повідомлення додано від {senderId} до {receiverId}.");
        }

        public async Task<List<Message>> GetMessagesAsync(string login1, string login2)
        {
            var u1 = await GetUserByLoginAsync(login1);
            var u2 = await GetUserByLoginAsync(login2);
            if (u1 == null || u2 == null) return new List<Message>();
            return await _db.Messages
                .Where(m => (m.SenderId == u1.Id && m.ReceiverId == u2.Id) || (m.SenderId == u2.Id && m.ReceiverId == u1.Id))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        // ===================== ВСІ ПОВІДОМЛЕННЯ ДЛЯ ГРУПОВОГО ЧАТУ =====================
        public async Task<List<Message>> GetAllMessagesAsync()
        {
            return await _db.Messages
                .Where(m => m.ReceiverId == 0)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        public void Dispose()
        {
            _db?.Dispose();
        }
    }
}