using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NuttyTree.NetDaemon.Infrastructure.Database;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        return services
            .AddDbContext<NuttyDbContext>((serviceProvider, optionsBuilder) =>
            {
                var dataFolder = serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("DataFolder");
                var dataFile = Path.Join(dataFolder, "netdaemon.db");
                optionsBuilder.UseSqlite($"Data Source={dataFile}");
            });
    }
}
