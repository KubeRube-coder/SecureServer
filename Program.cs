using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using SecureServer.data;
using SecureServer.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)         // Загружаем настройки
    .AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: true);  // Загружаем ключ

string secretKey = builder.Configuration["Jwt:SecretKey"];

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"), // Подключаемся к бд
        new MySqlServerVersion(new Version(8, 0, 32))
    )
);

builder.Services.AddControllers();  // Прослушиваем контроллеры
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");

var app = builder.Build();

app.UseExceptionHandler("/error");  // Если выйдет ошибка, то клиенту не отправит ошибку, а текст ниже

app.Map("/error", (HttpContext context) =>
{
    var response = new { Message = "It seems there was an error. Don't worry, we'll fix it soon." };
    return Results.Problem(response.Message, statusCode: 500);
});

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
    Console.WriteLine($"Request Body: {body}");
    context.Request.Body.Position = 0;
    await next.Invoke();
});

app.UseMiddleware<TokenValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Urls.Add("http://0.0.0.0:5000");

app.Run();
