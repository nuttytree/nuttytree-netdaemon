using Microsoft.Extensions.DependencyInjection;

namespace NuttyTree.NetDaemon.Infrastructure.RateLimiting;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddRateLimiter(this IServiceCollection services)
    {
        services?.Add(ServiceDescriptor.Singleton(typeof(IRateLimiter<>), typeof(RateLimiter<>)));
        return services ?? throw new ArgumentNullException(nameof(services));
    }
}
