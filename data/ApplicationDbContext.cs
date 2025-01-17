using Microsoft.EntityFrameworkCore;
using SecureServer.Models;

namespace SecureServer.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Mod> Mods { get; set; }
        public DbSet<Blacklist> Blacklist { get; set; }
        public DbSet<ActiveToken> ActiveTokens { get; set; }

    }
}
