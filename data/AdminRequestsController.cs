using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureServer.Data;
using SecureServer.Models;

namespace SecureServer.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminRequestsController : ControllerBase
    {
        public class TypeBody // for gets respone from query
        {
            public string type { get; set; }
        }

        public class RegisterUserType
        {
            public string login { get; set; }
            public string password { get; set; }
            public string? SteamId { get; set; }
            public string? DiscordId { get; set; }
        }

        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;
        public AdminRequestsController(ApplicationDbContext context, ILogger<AuthController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("users/search={query}")]
        public async Task<IActionResult> GetUserByQuery(string query, [FromBody] TypeBody bodyType)
        {
            if (bodyType == null) return NotFound();

            _logger.LogInformation(query);
            _logger.LogInformation(bodyType.type);

            switch (bodyType.type) { 
                case "SteamId":
                    var userSteam = await _context.Users
                        .Where(u => u.SteamId == query)
                        .ToListAsync();
                    return Ok(userSteam);

                case "DiscordId":
                    var userDiscord = await _context.Users
                        .Where(u => u.DiscordId == query)
                        .ToListAsync();
                    return Ok(userDiscord);

                default:
                    return NotFound();
            }
        }

        [HttpGet("users")] //deprected
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users.ToListAsync();
            if (users == null) return NotFound();

            return Ok(users);
        }

        [HttpGet("UserMods/{users}")]
        public async Task<IActionResult> GetModsByUsers(string users)
        {
            var userIds = users.Split(',').ToList();

            var usersData = await _context.Users
                .Where(u => userIds.Contains(u.Login))
                .ToListAsync();

            var usersMods = usersData.Select(u => new
            {
                username = u.Login,
                mods = _context.Mods
                    .Where(m => u.ClaimedMods.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse).Contains(m.Id))
                    .ToList()
            });

            return Ok(usersMods);
        }

        [HttpGet("userServers/{users}")]
        public async Task<IActionResult> GetServersByUsers(string users)
        {
            var userIds = users.Split(',').ToList();

            var usersData = await _context.Users
                .Where(u => userIds.Contains(u.Login))
                .ToListAsync();

            var usersServers = usersData.Select(u => new
            {
                username = u.Login,
                servers = _context.Servers
                    .Where(s => s.owner_id == u.Id)
                    .ToListAsync()
            });

            return Ok(usersServers);
        }

        [HttpGet("GetMods")]
        public async Task<IActionResult> GetMods()
        {
            var mods = await _context.Mods.ToListAsync();
            if (mods == null) return NotFound();
            return Ok(mods);
        }

        [HttpGet("GetServerMods/{ids}")]
        public async Task<IActionResult> GetServerMods(string ids)
        {
            var serversIds = ids.Split(",").Select(int.Parse).ToList();

            var serverMods = await _context.Servers
                .Where(s => serversIds.Contains(s.id))
                .ToListAsync();

            var result = serverMods.Select(s => new
            {
                serverId = s.id,
                mods = _context.Mods
                    .Where(m => s.mods.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .Contains(m.Id))
                    .ToList()
            }).ToList();

            return Ok(result);
        }

        [HttpGet("mods/getPendings")]
        public async Task<IActionResult> GetPendingsMods()
        {
            var pendingMods = await _context.pendingMods.ToListAsync();

            return Ok(pendingMods);
        }

        [HttpPost("mods/{id}/approve")]
        public async Task<IActionResult> ApproveMod(int id, [FromBody] TypeBody bodytype)
        {
            var pendingMod = await _context.pendingMods.SingleOrDefaultAsync(p => p.Id == id);
            if (pendingMod == null) return NotFound();

            var modDev = await _context.moddevelopers.SingleOrDefaultAsync(md => md.nameOfMod == pendingMod.Developer);

            var newMod = new Mod
            {
                modsby = modDev != null ? modDev.modsby : "unsorted", // Укажи корректное значение
                Name = pendingMod.Name,
                NameDWS = pendingMod.NameDWS,
                Description = pendingMod.Description,
                price = pendingMod.price,
                Url = pendingMod.Url,
                image_url = pendingMod.image_url
            };

            _context.Mods.Add(newMod);
            _context.pendingMods.Remove(pendingMod);
            await _context.SaveChangesAsync();

            if (modDev != null && !string.IsNullOrEmpty(modDev.mods))
            {
                var modDevIds = modDev.mods.Split(',').Select(id => int.Parse(id.Trim())).ToList();
                if (!modDevIds.Contains(newMod.Id))
                {
                    modDevIds.Add(newMod.Id);
                }

                modDevIds.Sort();

                modDev.mods = string.Join(",", modDevIds);
            } else if (modDev != null)
            {
                modDev.mods = newMod.Id.ToString();
            }

            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("mods/{id}/decline")]
        public async Task<IActionResult> DeclineMod(int id, [FromBody] TypeBody bodyType)
        {
            var mod = await _context.Mods.SingleOrDefaultAsync(m => m.Id == id);
            if (mod == null) return NotFound();

            var moddevel = await _context.moddevelopers.FirstOrDefaultAsync(m => m.modsby == mod.modsby);

            var PendingMod = new PendingMod
            {
                Developer = moddevel != null ? moddevel.nameOfMod : "unsorted",
                Name = mod.Name,
                NameDWS = mod.NameDWS,
                Description = mod.Description,
                price = mod.price,
                Url = mod.Url,
                image_url = mod.image_url,
                refused = bodyType.type,
                prem = false
            };

            _context.pendingMods.Add(PendingMod);
            _context.Mods.Remove(mod);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("mods/{id}/reject")]
        public async Task<IActionResult> RejectMod(int id, [FromBody] TypeBody bodytype)
        {
            var pendingMod = await _context.pendingMods.SingleOrDefaultAsync(p => p.Id == id);
            if (pendingMod == null) return NotFound();

            pendingMod.refused = bodytype.type;

            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPut("mods/{id}")]
        public async Task<IActionResult> UpdateMod(int id, [FromBody] Mod updatedMod)
        {
            var mod = await _context.Mods.SingleOrDefaultAsync(m => m.Id == id);
            if (mod == null) return NotFound();

            mod = updatedMod;
            await _context.SaveChangesAsync();

            return Ok(mod);
        }

        [HttpDelete("mods/{id}")]
        public async Task<IActionResult> DeleteMod(int id)
        {
            var mod = await _context.Mods.SingleOrDefaultAsync(m => m.Id == id);
            if (mod == null) return NotFound();

            _context.Mods.Remove(mod);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("mods/register")]
        public async Task<IActionResult> RegisterMod([FromBody] PendingMod providedMod)
        {
            if (providedMod == null) return BadRequest("Data can't be null");

            var newMod = new PendingMod
            {
                Developer = providedMod.Developer,
                Name = providedMod.Name,
                NameDWS = providedMod.NameDWS,
                Description = providedMod.Description,
                price = providedMod.price,
                Url = providedMod.Url,
                image_url = providedMod.image_url,
                refused = null,
                prem = providedMod.prem,
            };

            _context.pendingMods.Add(newMod);
            await _context.SaveChangesAsync();

            return Ok(newMod);
        }

        [HttpPost("users/register")]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUserType RegistrateForm)
        {
            if (RegistrateForm == null) return NotFound();

            var newUser = new User
            {
                Login = RegistrateForm.login,
                SteamId = RegistrateForm.SteamId != null ? RegistrateForm.SteamId : "" ,
                DiscordId = RegistrateForm.DiscordId != null ? RegistrateForm.DiscordId : "",
                Password = await PasswordHasher.HashPasswordAsync(RegistrateForm.password),
                JwtSecretKey = "not provided",
                lastip = "not provided",
                Banned = false,
                ClaimedMods = "0",
                role = "user",
                balance = 0
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(newUser);
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> RemoveUser(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            var tokens = await _context.ActiveTokens.FirstOrDefaultAsync(u => u.Id == user.Id);

            var servers = await _context.Servers.Where(s => s.owner_id == user.Id).ToListAsync();

            _context.Users.Remove(user);
            if (tokens != null) _context.ActiveTokens.Remove(tokens);
            if (servers != null)
            {
                foreach (var server in servers)
                {
                    _context.Servers.Remove(server);
                }
            }

            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("users/{id}/password")]
        public async Task<IActionResult> ResetPassword(int id, [FromBody] TypeBody bodyType)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            if (bodyType == null) return BadRequest();

            user.Password = await PasswordHasher.HashPasswordAsync(bodyType.type);

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
