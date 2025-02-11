using System.ComponentModel.DataAnnotations;

namespace SecureServer.Models
{
    public class Mod
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string modsby { get; set; }
        public string Name { get; set; }
        public string NameDWS { get; set; }
        public string Description { get; set; }
        public string Url {  get; set; }
        public string image_url { get; set; }
    }

    public class premmods
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int Id { get; set; }
        public string modsby { get; set; }
        public string mods { get; set; }
    }
}
