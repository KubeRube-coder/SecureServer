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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
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

                var subscription = await _context.subscription.SingleOrDefaultAsync(s => s.login == loginModel.Username);
                bool subHas = false;

                if (subscription != null)
                {
                    _logger.LogInformation("User {Username} found in subscription database.", user.Login);
                    subHas = subscription.subActive;
                }
                else
                {
                    _logger.LogInformation("User {Username} not found in subscription database. Creating new entry.", user.Login);
                    var SubData = new subscription
                    {
                        login = user.Login,
                        steamid = user.SteamId,
                        subActive = false,
                    };
                    _context.subscription.Add(SubData);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Subscription record created for user: {Username}.", user.Login);
                }

                var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()
                                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                                ?? "No IP";

                if (ipAddress == "65.21.83.32") return Unauthorized();

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
                    existingToken.ExpiryDate = subHas ? subscription.expireData : existingToken.ExpiryDate;

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
                        ExpiryDate = existingToken != null ? existingToken.ExpiryDate : DateTime.UtcNow.AddMinutes(5)
                    };
                    _context.ActiveTokens.Add(activeToken);
                    _logger.LogInformation("User {Username} data and token saved successfully.", user.Login);
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                return Ok(new { token = existingToken.JwtToken, username = user.Login, steamid = user.SteamId, lasip = user.lastip, discordId = user.DiscordId});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the login for user: {Username}.", loginModel.Username);
                return StatusCode(500, "An error occurred while processing your request.");
            }
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

                var subscription = await _context.subscription.SingleOrDefaultAsync(s => s.login == loginModel.Username);
                bool subHas = false;

                if (subscription != null)
                {
                    _logger.LogInformation("User {Username} found in subscription database.", user.Login);
                    subHas = subscription.subActive;
                }
                else
                {
                    _logger.LogInformation("User {Username} not found in subscription database. Creating new entry.", user.Login);
                    var SubData = new subscription
                    {
                        login = user.Login,
                        steamid = user.SteamId,
                        subActive = false,
                    };
                    _context.subscription.Add(SubData);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Subscription record created for user: {Username}.", user.Login);
                }

                var ipAddress = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()
                                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                                ?? "No IP";

                if (ipAddress == "65.21.83.32") return Unauthorized();

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
                    existingToken.ExpiryDate = subHas ? subscription.expireData : existingToken.ExpiryDate;

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
                        ExpiryDate = existingToken != null ? existingToken.ExpiryDate : DateTime.UtcNow.AddMinutes(5)
                    };
                    _context.ActiveTokens.Add(activeToken);
                    _logger.LogInformation("User {Username} data and token saved successfully.", user.Login);
                }

                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                var serveDB = await _context.Servers.SingleOrDefaultAsync(s => s.ip == ipAddress);

                if (serveDB == null) return Ok(new { token = existingToken.JwtToken, username = user.Login, steamid = user.SteamId, lasip = user.lastip, discordId = user.DiscordId });

                if (serveDB.owner_id != user.Id) return Unauthorized();

                var claimedMods = serveDB.mods.Split(',')
                    .Select(id => int.Parse(id))
                    .ToList();

                var mods = await _context.Mods
                    .Where(m => claimedMods.Contains(m.Id))
                    .ToListAsync();

                return Ok(new { token = existingToken.JwtToken, username = user.Login, steamid = user.SteamId, lasip = user.lastip, discordId = user.DiscordId, mods});
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
