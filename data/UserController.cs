using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureServer.Data;
using SecureServer.Models;

namespace SecureServer.Controllers
{
    [ApiController]
    [Route("api/token")]
    public class tokenController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public tokenController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("valid/{token}")]
        public async Task<IActionResult> HasValidToken(string token)
        {
            bool valid = false;

            var tokenFBD = await _context.ActiveTokens.SingleOrDefaultAsync(t => t.JwtToken == token);
            if (tokenFBD == null) return NotFound();

            if (tokenFBD.ExpiryDate > DateTime.UtcNow) valid = true;

            return Ok(new
            {
                tokenFBD.ExpiryDate,
                valid
            });
        }
    }

    [ApiController]
    [Route("api/mods")]
    public class modsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public modsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("public")]
        public async Task<IActionResult> GetModsDetails()
        {
            var mods = await _context.Mods.ToListAsync();
            return Ok(mods);
        }

        [HttpGet("public/{id}")]
        public async Task<IActionResult> GetSingleMod(int id)
        {
            var mod = await _context.Mods.SingleOrDefaultAsync(u => u.Id == id);
            if (mod == null) return NotFound();

            return Ok(mod);
        }

        [HttpGet("public/GetsMods/{ids}")]
        public async Task<IActionResult> GetModsByIds(string ids)
        {
            var idlist = ids.Split(',').Select(int.Parse).ToList();

            var mods = await _context.Mods
                .Where(s => idlist.Contains(s.Id))
                .ToListAsync();
            if (!mods.Any()) return NotFound();

            return Ok(mods);
        }

        [HttpGet("private/GetsMods/user/{username}&{token}")]
        public async Task<IActionResult> GetModsByUser(string username, string token )
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            var modsIds = user.ClaimedMods.Split(',').Select(int.Parse).ToList();

            var mods = await _context.Mods
                .Where(m => modsIds.Contains(m.Id))
                .ToListAsync();

            return Ok(mods);
        }

        [HttpGet("public/GetsMods/GetPrems")]
        public async Task<IActionResult> GetPremMods()
        {
            var premMods = await _context.premmods.ToListAsync();

            var premModsByList = premMods.Select(pm => pm.Id).Distinct().ToList();

            var filteredMods = await _context.Mods
                .Where(m => premModsByList.Contains(m.Id))
                .ToListAsync();

            return Ok(filteredMods);
        }

        [HttpGet("public/GetsMods/prem/{id}")]
        public async Task<IActionResult> GetPremModById(int id)
        {
            var premMods = await _context.premmods.ToListAsync();

            bool premModExists = premMods.Any(p =>
                p.mods.Split(',').Select(int.Parse).Contains(id)
            );

            if (!premModExists) return NotFound();

            var mod = await _context.Mods.SingleOrDefaultAsync(m => m.Id == id);
            if (mod == null) return NotFound();

            return Ok(mod);
        }

        [HttpGet("public/GetsMods/premIDs/{ids}")]
        public async Task<IActionResult> GetPremModsByIds(string ids)
        {
            var idList = ids.Split(',').Select(int.Parse).ToList();

            var premMods = await _context.premmods.ToListAsync();

            var premModsIds = premMods
                .SelectMany(p => p.mods.Split(',').Select(int.Parse))
                .Distinct()
                .ToList();

            var validIds = idList.Where(id => premModsIds.Contains(id)).ToList();

            if (!validIds.Any()) return NotFound();

            var mods = await _context.Mods
                .Where(m => validIds.Contains(m.Id))
                .ToListAsync();

            return Ok(mods);
        }


        [HttpGet("public/getDevs")]
        public async Task<IActionResult> GetDevs()
        {
            var dev = await _context.moddevelopers.ToListAsync();
            return Ok(dev);
        }

        [HttpGet("public/getDev/{id}")]
        public async Task<IActionResult> GetDevById(int id)
        {
            var dev = await _context.moddevelopers.SingleOrDefaultAsync(u => u.Id == id);
            if (dev == null) return NotFound();

            return Ok(dev);
        }

        [HttpGet("public/getDevs/{ids}")]
        public async Task<IActionResult> GetDevsByIds(string ids)
        {
            var idlist = ids.Split(',').Select(int.Parse).ToList();

            var devs = await _context.moddevelopers
                .Where(s => idlist.Contains(s.Id))
                .ToListAsync();

            if (!devs.Any()) return NotFound();

            return Ok(devs);
        }


        [HttpGet("private/getMods/forServers/{servers}")]
        public async Task<IActionResult> GetModsFromServers(string servers)
        {
            if (string.IsNullOrEmpty(servers))
                return BadRequest("Servers is null!");

            var servNames = servers.Split(',').Select(s => s.Trim()).ToList();
            var result = new Dictionary<string, object>();

            foreach (var server in servNames)
            {
                var serverName = await _context.Servers
                    .SingleOrDefaultAsync(s => s.name == server);

                if (serverName == null)
                {
                    Console.WriteLine($"Server {server} not found.");
                    continue;
                }

                Console.WriteLine($"Server: {server}, Mods Raw: {serverName.mods}");

                var modsIds = string.IsNullOrEmpty(serverName.mods)
                    ? new List<int>()
                    : serverName.mods.Split(',').Select(int.Parse).ToList();

                Console.WriteLine($"Mods IDs for {server}: {string.Join(", ", modsIds)}");

                var mods = await _context.Mods
                    .Where(m => modsIds.Contains(m.Id))
                    .ToListAsync();

                Console.WriteLine($"Found {mods.Count} mods for {server}");

                result[server] = new
                {
                    mods = mods
                };
            }

            return Ok(result);
        }

    }

    [ApiController]
    [Route("api/server")]
    public class serverController: ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public class ServerDto
        {
            public string Name { get; set; }
            public string Ip { get; set; }
            public string Mods { get; set; }
        }

        public class ServerEDto
        {
            public string Name { get; set; }
            public string Ip { get; set; }
            public string NewName { get; set; }
            public string NewIp { get; set; }
        }
        

        public serverController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("private/{username}&{token}")]
        public async Task<IActionResult> GetServerOfUser(string username, string token)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();
            if (token != user.JwtSecretKey) return Unauthorized();
            var serversUser = await _context.Servers
                .Where(s => s.owner_id == user.Id)
                .ToListAsync();

            if (serversUser == null) return NotFound();

            return Ok(serversUser.Select(s => new
            {
                s.mods,
                s.name,
                s.ip
            }));
        }

        [HttpPost("private/addServer/{username}&{token}")]
        public async Task<IActionResult> AddnewServer(string username, string token, [FromBody] ServerDto serverDto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();
            if (user.JwtSecretKey != token) return Unauthorized();

            var newServer = new Servers
            {
                name = serverDto.Name,
                ip = serverDto.Ip,
                owner_id = user.Id,
                mods = serverDto.Mods
            };

            _context.Servers.Add(newServer);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Сервер успешно добавлен", serverId = newServer.id, serverName = newServer.name });
        }

        [HttpPost("private/removeServer/{username}&{token}")]
        public async Task<IActionResult> RemoveServer(string username, string token, [FromBody] ServerDto serverDto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();
            if (user.JwtSecretKey != token) return Unauthorized();

            var server = await _context.Servers.SingleOrDefaultAsync(s => (s.name == serverDto.Name || s.ip == serverDto.Ip));
            if (server == null) return NotFound();

            _context.Servers.Remove(server);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Сервер успешно удален" });
        }

        [HttpPost("private/editServer/{username}&{token}")]
        public async Task<IActionResult> EditServer(string username, string token, [FromBody] ServerEDto serverDto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();
            if (user.JwtSecretKey != token) return Unauthorized();

            var server = await _context.Servers.SingleOrDefaultAsync(s => (s.name == serverDto.Name || s.ip == serverDto.Ip));
            if (server == null) return NotFound();

            server.ip = serverDto.Ip;
            server.name = serverDto.Name;

            await _context.SaveChangesAsync();

            return Ok(server);
        }
    }

    [ApiController]
    [Route("api/ip")]
    public class IpController : ControllerBase
    {
        [HttpGet("get")]
        public async Task<IActionResult> GetMyIP()
        {
            string ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "No IP";

            return Ok(ip);

        }
    }

    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{username}&{token}")]
        public async Task<IActionResult> GetUserDetails(string username, string token)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            if (token != user.JwtSecretKey) return Unauthorized();

            return Ok(new
            {
                user.SteamId,
                user.DiscordId,
                user.ClaimedMods
            });
        }

        [HttpGet("getMods/{username}")]
        public async Task<IActionResult> GetModsOfUser(string username)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();
            
            var modsIds = user.ClaimedMods.Split(',').Select(int.Parse).ToList();

            var NamesOfMods = await _context.Mods
                .Where(m => modsIds.Contains(m.Id))
                .Select(m => m.NameDWS)
                .ToListAsync();

            return Ok(string.Join(",", NamesOfMods));
        }
    }
}
