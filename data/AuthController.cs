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

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == loginModel.Username);

            if (user == null || user.Password != loginModel.Password)
                return Unauthorized();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var token = GenerateJwtToken(loginModel.Username, ipAddress, string.Format("RRWORKSHOP-{0}-", loginModel.Username));

            user.JwtSecretKey = token;
            user.lastip = ipAddress;

            var existingToken = await _context.ActiveTokens
                .SingleOrDefaultAsync(t => t.Username == loginModel.Username);

            if (existingToken != null)
            {
                existingToken.JwtToken = token;
                existingToken.ExpiryDate = DateTime.UtcNow.AddMinutes(30);
                _context.ActiveTokens.Update(existingToken);
            }
            else
            {
                var activeToken = new ActiveToken
                {
                    JwtToken = token,
                    Username = loginModel.Username,
                    ExpiryDate = DateTime.UtcNow.AddMinutes(30)
                };
                _context.ActiveTokens.Add(activeToken);
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            string username = user.Login;
            string steamid = user.SteamId;
            string lasip = user.lastip;

            return Ok(new { token, username, steamid, lasip});
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
