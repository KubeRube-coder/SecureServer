﻿using Newtonsoft.Json;
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

    public static class DiscordSender
    {
        public static async Task<bool> SendToDiscord(string message, string usernameV, string title, int color, string avatarUrl, string webhookUrl)
        {
            if (string.IsNullOrEmpty(webhookUrl))
            {
                Console.WriteLine($"[DWS Server] Ошибка: WebHook пуст!");
                return false;
            }

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var json = new
                    {
                        username = usernameV,
                        avatar_url = avatarUrl,
                        embeds = new[]
                        {
                            new
                            {
                                title = title,
                                description = message,
                                color = color,
                                footer = new { text = "DayZWorkShopApp" },
                                timestamp = DateTime.UtcNow.ToString("o")
                            }
                        }
                    };

                    string jsonContent = JsonConvert.SerializeObject(json);
                    StringContent httpContent = new StringContent(jsonContent, new UTF8Encoding(false), "application/json");

                    HttpResponseMessage response = await client.PostAsync(webhookUrl, httpContent);

                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DWS Server] Ошибка отправки в Discord: {ex.Message}");
                return false;
            }
        }
    }

}
