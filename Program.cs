using Microsoft.EntityFrameworkCore;
using SecureServer.data;
using SecureServer.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: true);

string secretKey = builder.Configuration["Jwt:SecretKey"];

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"), // Подключаемся к бд
        new MySqlServerVersion(new Version(8, 0, 32))
    )
);

builder.Host.UseSerilog();

builder.Services.AddRazorPages();
builder.Services.AddControllers();  // Прослушиваем контроллеры
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");

var app = builder.Build();

app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
    );

app.UseExceptionHandler("/error");  // Если выйдет ошибка, то клиенту не отправит ошибку, а текст ниже

app.Map("/error", (HttpContext context) =>
{
    Console.WriteLine(context.Request);
    var response = new { Message = "It seems there was an error. Don't worry, we'll fix it soon. Or contact us: https://discord.gg/qqXKhxAYAE" };
    return Results.Problem(response.Message, statusCode: 500);
});

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
    Console.WriteLine("----------------------NEW-BLOCK----------------------");
    Console.WriteLine(context.Request.Path + $"\nRequest Body {body}");
    Console.WriteLine();
    context.Request.Body.Position = 0;
    await next.Invoke();
    Console.WriteLine("----------------------END-BLOCK----------------------");
});

app.UseMiddleware<TokenValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.UseStaticFiles();
app.MapControllers();
app.MapHealthChecks("/health");

app.Urls.Add("http://0.0.0.0:5000");

app.Run();
