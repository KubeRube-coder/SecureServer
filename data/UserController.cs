using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureServer.Data;
using SecureServer.Models;

namespace SecureServer.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{username}")]
        public async Task<IActionResult> GetUserDetails(string username)
        {
            var user = await _context.Users.FindAsync(username);
            if (user == null) return NotFound();

            return Ok(new
            {
                user.SteamId,
                user.DiscordId,
                user.ClaimedMods
            });
        }
    }
}
