using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SecureServer.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class DailyTaskRefresher : BackgroundService
{
    private readonly ILogger<DailyTaskRefresher> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DailyTaskRefresher(ILogger<DailyTaskRefresher> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Daily Task works! Executing refresh database");

                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    _logger.LogInformation("Starting refreshing Subscriptions");
                    await HandleSubscription(context);

                    _logger.LogInformation("Starting refreshing Mods");
                    await HandleMods(context);
                }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error when processing Task");
            }
        }
    }

    private async Task HandleSubscription(ApplicationDbContext context)
    {
        var subscriptions = await context.subscription.Where(s => s.subActive == true).ToListAsync();

        foreach (var subscription in subscriptions)
        {
            if (subscription.BuyWhenExpires == true && subscription.expireData.Date < DateTime.UtcNow.Date)
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Login == subscription.login);
                if (user != null)
                {
                    var price = await context.premmods.FirstOrDefaultAsync(u => u.mods == subscription.subscriptionMods);
                    if (price != null)
                    {
                        user.balance -= price.premPrice;
                    }
                }
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Sucesfully refresh DB Subscriptions");
    }

    private async Task HandleMods(ApplicationDbContext context)
    {
        var purchases = await context.purchasesInfo.ToListAsync();

        foreach (var purchase in purchases)
        {
            if (purchase.expires_date.Date < DateTime.UtcNow.Date)
            {
                var server = await context.Servers.FirstOrDefaultAsync(s => s.id == purchase.serverId);
                if (server != null)
                {
                    var ServerModsIds = server.mods.Split(',')
                        .Select(id => int.Parse(id.Trim()))
                        .ToList();

                    if (ServerModsIds.Remove(purchase.modId))
                    {
                        server.mods = string.Join(',', ServerModsIds);
                    }
                }

                var user = await context.Users.FirstOrDefaultAsync(u => u.Id == purchase.whoBuyed);
                if (user != null)
                {
                    var UserModsIds = user.ClaimedMods.Split(',')
                        .Select(id => int.Parse(id.Trim()))
                        .ToList();

                    if (UserModsIds.Remove(purchase.modId))
                    {
                        user.ClaimedMods = string.Join(",", UserModsIds);
                    }
                }
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("Sucessfully refresh Mods");
    }
}
