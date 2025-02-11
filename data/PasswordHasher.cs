using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SecureServer.Controllers
{
    public static class PasswordHasher
    {
        public static async Task<string> HashPasswordAsync(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] data = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = await Task.Run(() => sha256.ComputeHash(data));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }

}
