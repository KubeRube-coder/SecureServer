using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class User
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string Login { get; set; }
        public string SteamId {  get; set; }
        public string DiscordId {  get; set; }
        public string Password {  get; set; }
        public bool Banned {  get; set; }
        public string ClaimedMods {  get; set; }
        public string? JwtSecretKey { get; set; }
        public string lastip { get; set; }
    }
}
