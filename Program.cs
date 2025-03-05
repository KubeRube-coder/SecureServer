using Microsoft.EntityFrameworkCore;
using SecureServer.data;
using SecureServer.Data;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: true);

string secretKey = builder.Configuration["Jwt:SecretKey"];

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .Filter.ByExcluding(log => log.Properties.ContainsKey("SourceContext") &&
                               log.Properties["SourceContext"].ToString().Contains("Microsoft.EntityFrameworkCore"))
    .CreateLogger();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"), // Подключаемся к бд
        new MySqlServerVersion(new Version(8, 0, 32))
    )
);

builder.Host.UseSerilog();

builder.Services.AddHostedService<DailyTaskRefresher>();
builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
    );

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("It seems there was an error. Contact us: https://discord.gg/qqXKhxAYAE");
    });
});

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
    Console.WriteLine("----------------------NEW-BLOCK----------------------");
    Console.WriteLine(context.Request.Path + $"\nRequest Body {body}");
    context.Request.Body.Position = 0;
    await next.Invoke();
    Console.WriteLine("----------------------END-BLOCK----------------------");
});

app.UseMiddleware<TokenValidationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.MapControllers();
app.MapHealthChecks("/health");

app.Urls.Add("http://0.0.0.0:5000");

app.Run();
