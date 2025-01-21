using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using SecureServer.Data;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Выполняем простой запрос, чтобы проверить подключение
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return HealthCheckResult.Healthy("База данных доступна.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("База данных недоступна.", ex);
        }
    }
}
