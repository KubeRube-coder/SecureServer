using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureServer.data;
using SecureServer.Data;
using SecureServer.Models;
using System.Collections.Generic;
using static SecureServer.Controllers.modsController;

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

        [HttpGet("private/GetsMods/user/{username}")]
        public async Task<IActionResult> GetModsByUser(string username)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            var allMods = new List<int>();

            var PremMods = await _context.subscription.SingleOrDefaultAsync(u => u.login == user.Login);
            if (PremMods != null)
            {
                var modsPremIds = PremMods.subscriptionMods.Split(',').Select(int.Parse).ToList();

                allMods.AddRange(modsPremIds);
            }

            var modsIds = user.ClaimedMods.Split(',').Select(int.Parse).ToList();

            allMods.AddRange(modsIds);

            var mods = await _context.Mods
                .Where(m => allMods.Contains(m.Id))
                .ToListAsync();

            return Ok(mods);
        }

        [HttpGet("public/GetsMods/GetPrems")]
        public async Task<IActionResult> GetPremMods()
        {
            var premMods = await _context.premmods.ToListAsync();

            var premModsByList = premMods
                .SelectMany(pm => pm.mods?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => int.TryParse(m, out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => new { ModId = id.Value, ModsBy = pm.modsby, Price = pm.premPrice })
                    ?? Enumerable.Empty<dynamic>())
                .ToList();

            var modIds = premModsByList.Select(x => x.ModId).ToList();

            var mods = await _context.Mods
                .Where(m => modIds.Contains(m.Id))
                .ToListAsync();

            var groupedMods = premModsByList
                .GroupBy(p => new { p.ModsBy, p.Price })
                .Select(group => new
                {
                    modName = group.Key.ModsBy,
                    price = group.Key.Price,
                    Mods = mods.Where(m => group.Select(g => g.ModId).Contains(m.Id))
                               .Select(m => new
                               {
                                   m.Id,
                                   modsBy = m.modsby,
                                   Name = m.Id,
                                   NameDWS = m.NameDWS,
                                   Description = m.Description,
                                   Price = m.price,
                                   Url = m.Url,
                                   image_url = m.image_url
                               }).ToList()
                })
                .ToList();

            return Ok(groupedMods);
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

                if (serverName == null) continue;

                var modsIds = string.IsNullOrEmpty(serverName.mods)
                    ? new List<int>()
                    : serverName.mods.Split(',').Select(int.Parse).ToList();

                var mods = await _context.Mods
                    .Where(m => modsIds.Contains(m.Id))
                    .ToListAsync();

                var modsDate = mods.Select(mod => new ModUser
                {
                    Id = mod.Id,
                    modsby = mod.modsby,
                    Name = mod.Name,
                    NameDWS = mod.NameDWS,
                    Description = mod.Description,
                    Url = mod.Url,
                    image_url = mod.image_url,
                    expires_date = _context.purchasesInfo
                        .Where(p => p.whoBuyed == serverName.owner_id && p.modId == mod.Id)
                        .Select(p => p.expires_date)
                        .FirstOrDefault()
                }).ToList();

                result[server] = new
                {
                    mods = modsDate
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

        public class MassiveType
        {
            public string usernameNew { get; set; }
            public string serverportNew { get; set; }
            public string[] modNamesNew { get; set; }
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

        [HttpPost("GetServerName")]
        public async Task<IActionResult> ValidMods([FromBody] MassiveType TypeMods)
        {
            if (TypeMods == null) return BadRequest("Data is null");

            foreach(string mod in TypeMods.modNamesNew)
            {
                var modEx = await _context.Mods.FirstOrDefaultAsync(m => m.NameDWS == mod);
                if (modEx != null)
                {
                    var Webhook = await _context.webhooks.FirstOrDefaultAsync(w => w.NameMod == modEx.NameDWS);
                    if (Webhook != null)
                    {
                        string newMessage = $"❌ Сервер был запущен БЕЗ подтвержденного мода {Webhook.NameMod}!\n"
                            + $"👤 Логин:  {TypeMods.usernameNew}\n"
                            + $"🌍 IP Запуска:  {TypeMods.serverportNew}";

                        var responseDiscord = DiscordSender.SendToDiscord(newMessage, "DWS Guard", "📢ЗАПУСК МОДА НЕ ПОДТВЕРЖДЕН!", 0x00CC0033, "https://i.postimg.cc/4Npvzxp4/Logo-DWSPng.png", Webhook.Discord_web);

                        if (responseDiscord.Result)
                        {
                            Console.WriteLine("Sucessfull sending to Discord message about " + Webhook.NameMod);
                        }
                    }
                    
                }
            }

            return Ok();
        }

        [HttpPost("private/addServer/{username}&{token}")]
        public async Task<IActionResult> AddnewServer(string username, string token, [FromBody] ServerDto serverDto)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();
            if (user.JwtSecretKey != token) return Unauthorized();

            var existingServer = await _context.Servers
                .Where(s => s.owner_id == user.Id && s.name == serverDto.Name)
                .FirstOrDefaultAsync();

            if (existingServer != null) return BadRequest("Сервер с таким названием уже существует!");

            var serverCount = await _context.Servers
                .Where(s => s.owner_id == user.Id)
                .CountAsync();

            if (serverCount >= 6) return BadRequest("Maximum of servers!");

            var newServer = new Servers
            {
                name = serverDto.Name,
                ip = serverDto.Ip,
                owner_id = user.Id,
                mods = "0"
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
        public class BoughtData
        {
            public string ServerIp { get; set; }
            public int[] Mods { get; set; }
        }

        public class PremData
        {
            public string NameOfDeveloperName { get; set; }
        }

        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserController> _logger;

        public UserController(ApplicationDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger = logger;
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

        [HttpGet("balance")]
        public async Task<IActionResult> GetWallet()
        {
            if (Request.Headers.TryGetValue("Username", out var username))
            {
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username.First());
                if (user == null) return NotFound();
                return Ok(user.balance);
            }

            return BadRequest();
        }

        [HttpGet("getMods/{username}")]
        public async Task<IActionResult> GetModsOfUser(string username)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return NotFound();

            var modsIds = await _context.Servers.FirstOrDefaultAsync(s => s.owner_id == user.Id);

            var devsMods = await _context.moddevelopers.FirstOrDefaultAsync(md => md.nameOfMod == user.Login);
            var ListDevsMods = devsMods?.mods.Split(',')
                .Select(id => int.Parse(id))
                .ToList() ?? new List<int>();

            var allModsNames = new List<string>();

            var PremMods = await _context.subscription.SingleOrDefaultAsync(u => u.login == user.Login);
            if (PremMods != null && PremMods.subActive == true)
            {
                var modsPremIds = PremMods.subscriptionMods.Split(',').Select(int.Parse).ToList();
                var modsPrem = await _context.Mods.Where(m => modsPremIds.Contains(m.Id)).Select(m => m.NameDWS).ToListAsync();

                if (modsPrem != null) allModsNames.AddRange(modsPrem);
            }

            if ((user.role == "modcreator" || user.role == "admin") && ListDevsMods.Any())
            {
                var userModsNames = await _context.Mods
                    .Where(m => ListDevsMods.Contains(m.Id))
                    .Select(m => m.NameDWS)
                    .ToListAsync();

                allModsNames.AddRange(userModsNames);
            }

            if (modsIds != null)
            {
                var modsIdsFromServers = modsIds.mods.Split(',')
                    .Select(int.Parse)
                    .ToList();

                var serverModsNames = await _context.Mods
                    .Where(m => modsIdsFromServers.Contains(m.Id))
                    .Select(m => m.NameDWS)
                    .ToListAsync();

                allModsNames.AddRange(serverModsNames);
            }

            if (allModsNames.Count() == 0)
            {
                return Ok("NOT FOUND");
            } else
            {
                return Ok(string.Join(",", allModsNames.Distinct()));
            }
        }

        [HttpGet("GetPurchaseHistory")]
        public async Task<IActionResult> GetPurcasheHistory()
        {
            if (Request.Headers.TryGetValue("Username", out var username))
            {
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username.First());
                if (user == null) return NotFound();

                var mods = await _context.Mods.ToListAsync();
                var servers = await _context.Servers.ToListAsync();

                var purchasesUser = await _context.purchasesInfo
                    .Where(u => u.whoBuyed == user.Id)
                    .ToListAsync();

                if (purchasesUser != null)
                {
                    var result = purchasesUser.Select(s =>
                    {
                        var mod = mods.SingleOrDefault(m => m.Id == s.modId);
                        if (mod == null)
                        {
                            return null;
                        }

                        var server = servers.SingleOrDefault(ser => ser.id == s.serverId);
                        if (server == null)
                        {
                            return null;
                        }

                        return new
                        {
                            id = s.Id,
                            modsby = mod.modsby,
                            name = mod.Name,
                            server = server.name,
                            bought_date = s.date,
                            expires_date = s.expires_date
                        };
                    }).Where(x => x != null);

                    return Ok(result);
                }
            }

            return BadRequest("Uncorrect headers!");
        }

        [HttpGet("GetMySubscriptions")]
        public async Task<IActionResult> getMySubscriptions()
        {
            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username is null");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("User not found");

            var subscription = await _context.subscription.Where(s => s.login == user.Login).ToListAsync();
            if (subscription == null) return BadRequest("You don't have subscriptions");

            var NewSub = subscription.Select(s =>
            {
                var userMod = _context.moddevelopers.FirstOrDefaultAsync(md => md.mods == s.subscriptionMods);

                return new
                {
                    id = s.Id,
                    subscriptionMods = userMod.Result?.nameOfMod,
                    BuyWhenExpires = s.BuyWhenExpires,
                    expireData = s.expireData
                };
            });

            return Ok(NewSub);
        }

        [HttpPost("SetBoughtSub/{id}")]
        public async Task<IActionResult> SetSubscription(int id)
        {
            if (id == 0) return BadRequest("Uncorrect id");

            var username = Request.Headers["UserName"].FirstOrDefault();
            if (username == null) return BadRequest("Username was not provided");

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("Uncorrect username");

            var subscription = await _context.subscription.SingleOrDefaultAsync(sub => sub.login == user.Login && sub.Id == id);
            if (subscription == null) return BadRequest("Uncorrect id. Subscription not found");

            subscription.BuyWhenExpires = !subscription.BuyWhenExpires;
            await _context.SaveChangesAsync();

            return Ok(new {message = ("now subscription is " + subscription.BuyWhenExpires) });
        }

        [HttpPost("buy/mods")]
        public async Task<IActionResult> HandleBoughtMods([FromBody] BoughtData Data)
        {
            if (Data == null) return BadRequest("Data is null");

            var server = await _context.Servers.SingleOrDefaultAsync(s => s.ip == Data.ServerIp);
            if (server == null) return NotFound("Server was not found");

            var username = Request.Headers["UserName"].FirstOrDefault();

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("User not found");

            if (server.owner_id != user.Id) return BadRequest();

            var mods = await _context.Mods.Where(m => Data.Mods.Contains(m.Id)).ToListAsync();
            if (mods.Count() == 0) return NotFound("Mods not found");

            float price = 0;

            foreach (var mod in mods)
            {
                price += mod.price;
            }

            _logger.LogInformation(price.ToString());

            if (price > 0 && price <= user.balance)
            {
                var ServerModsIds = server.mods.Split(',').Select(id => int.Parse(id.Trim())).ToList();
                
                foreach (var mod in mods)
                {
                    if (!ServerModsIds.Contains(mod.Id))
                    {
                        ServerModsIds.Add(mod.Id);
                        var purchases = new purchasesInfo
                        {
                            whoBuyed = user.Id,
                            modId = mod.Id,
                            serverId = server.id,
                            date = DateTime.Now,
                            expires_date = DateTime.Now.AddDays(5000),
                        };

                        _context.purchasesInfo.Add(purchases);
                    } else if (ServerModsIds.Contains(mod.Id))
                    {
                        price -= mod.price;
                    }
                }

                user.balance -= price;

                ServerModsIds.Sort();

                server.mods = string.Join(",", ServerModsIds);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Successfull bought"
                });
            }

            return BadRequest("Not enought money");
        }

        [HttpPost("buy/premium")]
        public async Task<IActionResult> BuyPremium([FromBody] PremData Data)
        {
            if (Data == null) return BadRequest("Data is null");

            var username = Request.Headers["UserName"].FirstOrDefault();

            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == username);
            if (user == null) return BadRequest("User not found");

            var moddeveloper = await _context.moddevelopers.FirstOrDefaultAsync(md => md.nameOfMod == Data.NameOfDeveloperName);
            if (moddeveloper == null) return BadRequest("Developer was not found");

            var SubscriptionsMods = await _context.premmods.FirstOrDefaultAsync(sm => sm.modsby == moddeveloper.modsby);
            if (SubscriptionsMods == null) return NotFound("You don't have any subscriptions");

            float price = SubscriptionsMods.premPrice;

            if (price > 0 && price <= user.balance)
            {
                var subscription = new subscription
                {
                    login = user.Login,
                    steamid = user.SteamId,
                    subscriptionMods = SubscriptionsMods.mods,
                    subActive = true,
                    expireData = DateTime.UtcNow.AddDays(30),
                };

                user.balance -= price; 

                _context.subscription.Add(subscription);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Successfull bought"
                });
            }

            return BadRequest("Not enought money");
        }
    }
}
