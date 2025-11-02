using System.Security.Cryptography;
using System.Text;


namespace UdpChatServer
{
    public static class PasswordHelper
    {
        public static void HashPassword(string password, out string salt, out string hash)
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
            byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            100000,
            HashAlgorithmName.SHA512,
            32);


            salt = Convert.ToBase64String(saltBytes);
            hash = Convert.ToBase64String(hashBytes);
        }


        public static bool VerifyPassword(string password, string storedSalt, string storedHash)
        {
            byte[] saltBytes = Convert.FromBase64String(storedSalt);
            byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            saltBytes,
            100000,
            HashAlgorithmName.SHA512,
            32);


            return Convert.ToBase64String(hashBytes) == storedHash;
        }
    }
}