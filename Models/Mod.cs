using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class Mod
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Url {  get; set; }
    }
}
