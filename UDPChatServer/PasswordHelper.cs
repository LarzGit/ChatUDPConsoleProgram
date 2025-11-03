using System;
using System.Security.Cryptography;
using System.Text;

namespace UdpChatServer
{
    public static class PasswordHelper
    {
        public static void HashPassword(string password, out string salt, out string hash)
        {
            // Генеруємо соль
            byte[] saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            salt = Convert.ToBase64String(saltBytes);

            // Використовуємо PBKDF2
            using var derive = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
            var key = derive.GetBytes(32);
            hash = Convert.ToBase64String(key);
        }

        public static bool VerifyPassword(string password, string salt, string storedHash)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(salt);
                using var derive = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
                var key = derive.GetBytes(32);
                var hash = Convert.ToBase64String(key);
                return hash == storedHash;
            }
            catch
            {
                return false;
            }
        }
    }
}
