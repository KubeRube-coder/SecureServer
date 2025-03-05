using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecureServer.Data;
using SecureServer.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SecureServer.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ApplicationDbContext context, ILogger<AuthController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _logger.LogInformation("AuthController initialized.");
        }

        [HttpPost("login/getFulldata")]
        public async Task<IActionResult> GetFullData([FromBody] LoginModel loginModel)
        {
            if (loginModel == null)
            {
                _logger.LogError("LoginModel is null.");
                return BadRequest("Invalid request.");
            }

            _logger.LogInformation("Login attempt for user: {Username}", loginModel.Username);

            try
            {
                var hashedPassword = await PasswordHasher.HashPasswordAsync(loginModel.Password);
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == loginModel.Username);
                if (user == null || user.Password != hashedPassword)
                {
                    _logger.LogWarning("Failed login attempt for username: {Username}.", loginModel.Username);
                    return Unauthorized();
                }

                _logger.LogInformation("User {Username} authenticated successfully.", user.Login);


                var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()
                                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                                ?? "No IP";

                if (ipAddress == "65.21.83.32") 
                {
                    _logger.LogWarning("[{user}] server get's a wrong IP: {ip}. Maybe delete this?", loginModel.Username, ipAddress);
                    return Unauthorized();
                };

                _logger.LogInformation("IP Address for user {Username}: {IpAddress}", user.Login, ipAddress);
                if (user.lastip != null && user.lastip != ipAddress)
                {
                    user.lastip = ipAddress;
                }

                var existingToken = await _context.ActiveTokens
                    .SingleOrDefaultAsync(t => t.Username == loginModel.Username);

                if (existingToken != null)
                {
                    _logger.LogInformation("Existing token found for user: {Username}. Updating token.", user.Login);
                    existingToken.ExpiryDate = DateTime.UtcNow.AddDays(60);

                    _logger.LogInformation("Token expiry date set to {ExpiryDate} for user: {Username}.", existingToken.ExpiryDate, user.Login);
                    _context.ActiveTokens.Update(existingToken);
                }
                else
                {
                    var token = GenerateJwtToken(loginModel.Username, ipAddress, $"RRWORKSHOP-{loginModel.Username}-");
                    _logger.LogInformation("JWT token generated for user: {Username}", user.Login);

                    _logger.LogInformation("No existing token found for user: {Username}. Creating new token.", user.Login);
                    var activeToken = new ActiveToken
                    {
                        JwtToken = token,
                        Username = loginModel.Username,
                        ExpiryDate = existingToken != null ? existingToken.ExpiryDate : DateTime.UtcNow.AddDays(1)
                    };

                    user.JwtSecretKey = token;
                    _context.ActiveTokens.Add(activeToken);
                    _logger.LogInformation("User {Username} data and token saved successfully.", user.Login);
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                var serveDB = await _context.Servers.SingleOrDefaultAsync(s => s.ip == ipAddress);

                if (serveDB == null)
                {
                    return Ok(new
                    {
                        token = existingToken.JwtToken,
                        username = user.Login,
                        steamid = user.SteamId,
                        lasip = user.lastip,
                        discordId = user.DiscordId,
                        role = user.role,
                        balance = user.balance
                    });
                }

                var claimedMods = serveDB.mods.Split(',')
                    .Select(id => int.Parse(id))
                    .ToList();

                var mods = await _context.Mods
                    .Where(m => claimedMods.Contains(m.Id))
                    .ToListAsync();

                var devsMods = await _context.moddevelopers.FirstOrDefaultAsync(md => md.nameOfMod == user.Login);
                var ListDevsMods = devsMods?.mods.Split(',')
                    .Select(id => int.Parse(id))
                    .ToList() ?? new List<int>();

                if (user.role == "modcreator" || user.role == "admin")
                {
                    var userMods = await _context.Mods
                        .Where(m => ListDevsMods.Contains(m.Id))
                        .ToListAsync();

                    mods.AddRange(userMods);
                }

                var modsDate = mods
                    .DistinctBy(m => m.Id)
                    .Select(mod => new ModUser
                    {
                        Id = mod.Id,
                        modsby = mod.modsby,
                        Name = mod.Name,
                        NameDWS = mod.NameDWS,
                        Description = mod.Description,
                        Url = mod.Url,
                        image_url = mod.image_url,
                        expires_date = _context.purchasesInfo
                            .Where(p => p.whoBuyed == user.Id && p.modId == mod.Id)
                            .Select(p => p.expires_date)
                            .FirstOrDefault()
                    })
                    .ToList();

                return Ok(new
                {
                    token = existingToken.JwtToken,
                    username = user.Login,
                    steamid = user.SteamId,
                    lasip = user.lastip,
                    discordId = user.DiscordId,
                    role = user.role,
                    balance = user.balance,
                    modsDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the login for user: {Username}.", loginModel.Username);
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }

        private string GenerateJwtToken(string username, string ipAddress, string secretKey)
        {
            secretKey = secretKey.Length >= 32
            ? secretKey.Substring(0, 32)
            : PadRightWithRandom(secretKey, 32);

            Console.WriteLine(secretKey);

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("sub", username),
                    new Claim("ip", ipAddress)
                }),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string PadRightWithRandom(string input, int totalLength)
        {
            var random = new Random();
            var characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var builder = new StringBuilder(input);

            while (builder.Length < totalLength)
            {
                builder.Append(characters[random.Next(characters.Length)]);
            }

            return builder.ToString();
        }
    }
}
