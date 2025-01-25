using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureServer.Models;
using SecureServer.Data;
using System.Security.Cryptography;
using System.Text;


namespace SecureServer.Controllers
{
    [Route("admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(ApplicationDbContext dbContext, ILogger<AdminController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // Маршрут для авторизации
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel loginModel)
        {
            if (loginModel == null || string.IsNullOrEmpty(loginModel.Username) || string.IsNullOrEmpty(loginModel.Password))
            {
                return BadRequest("Username and password are required.");
            }

            // Поиск пользователя по логину
            var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.Login == loginModel.Username);
            if (user == null)
            {
                return Unauthorized("Invalid username or password.");
            }

            // Проверка пароля
            if (!VerifyPassword(user.Password, loginModel.Password))
            {
                return Unauthorized("Invalid username or password.");
            }

            if (user.JwtSecretKey != null)
            {
                var redirectToAdminPanel = $"http://127.0.0.1:5000/admin/panel?token={user.JwtSecretKey}";
                return Ok(new
                {
                    message = $"Greetings {user.Login}",
                    redirectToAdminPanel
                });
            }

            return Unauthorized("You not registeret. Please login from app.");
        }

        // Метод для верификации пароля (с использованием хеширования)
        private bool VerifyPassword(string storedPassword, string enteredPassword)
        {
            using (var sha256 = SHA256.Create())
            {
                var enteredPasswordHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(enteredPassword)));
                return storedPassword == enteredPasswordHash;
            }
        }
    }

    [Route("admin")]
    public class AdminSite : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminSite(ApplicationDbContext dbContext, ILogger<AdminController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("panel")]
        public IActionResult Index()
        {
            // Извлекаем токен из параметров URL
            var token = Request.Query["token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized("Token is missing or invalid.");
            }

            var user = _dbContext.Users.SingleOrDefault(u => u.JwtSecretKey == token);

            if (user == null)
            {
                return Unauthorized("Invalid token.");
            }

            ViewBag.Token = token;
            return View(_dbContext);
        }
    }

}
