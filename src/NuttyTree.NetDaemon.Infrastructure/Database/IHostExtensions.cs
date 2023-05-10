using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NuttyTree.NetDaemon.Infrastructure.Database;

public static class IHostExtensions
{
    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        var logger = host.Services.GetRequiredService<ILogger<NuttyDbContext>>();
        logger.LogInformation("Starting the database migration");

        var dataFolder = host.Services.GetRequiredService<IConfiguration>().GetValue<string>("DataFolder");
        if (!Path.Exists(dataFolder))
        {
            Directory.CreateDirectory(dataFolder!);
        }

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<NuttyDbContext>().Database.MigrateAsync();

        logger.LogInformation("Completed the database migration");
    }
}
