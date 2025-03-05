using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class ActiveToken
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string JwtToken { get; set; }
        public string Username { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}
