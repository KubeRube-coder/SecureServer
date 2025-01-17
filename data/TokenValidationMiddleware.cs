using Microsoft.EntityFrameworkCore;
using SecureServer.Data;
using SecureServer.Models;

namespace SecureServer.data
{
    public class TokenValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public TokenValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            var path = context.Request.Path.Value;

            if (path != null && ((path.StartsWith("/api/auth/login") || (path.StartsWith("/api/check/status")))))   // Тут мы не обрабатываем случаи, когда пытаемся залогиниться
            {
                await _next(context);
                return;
            }

            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();   // Получаем данные где есть указано Authorization
            var username = context.Request.Headers["UserName"].FirstOrDefault();                        // и UserName

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username) || !await ValidateTokenAsync(token, username, serviceProvider)) // Если какие-то данные не введены, отклоняем запрос
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized from validation");
                return;
            }

            await _next(context);
        }

        private async Task<bool> ValidateTokenAsync(string token, string username, IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username))
                return false;

            using var scope = serviceProvider.CreateScope();    //Способ, чтобы получить бд
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();   // получаем бд

            var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Login == username);    // получаем данные с бд из таблицы Users
            var date = await dbContext.ActiveTokens.SingleOrDefaultAsync(u => u.Username == username);  // получаем данные с бд из таблицы ActiveTokens

            if (date.ExpiryDate < DateTime.UtcNow)  // Если токен истёк, то не даём получить данные
            {
                return false;
            }

            if (user == null || user.Banned == true)    // Если игрок забанен, либо не найден, то так же не отдаём запрос
                return false;

            return user.JwtSecretKey == token;
        }
    }
}
