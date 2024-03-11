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
                var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetValue<string>("DatabaseConnection");
                optionsBuilder.UseSqlServer(connectionString);
            });
    }
}
