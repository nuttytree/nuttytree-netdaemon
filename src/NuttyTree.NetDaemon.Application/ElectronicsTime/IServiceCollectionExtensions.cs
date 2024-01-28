using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Options;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddElectronicsTimeApp(this IServiceCollection services)
    {
        services.AddOptions<ElectronicsTimeOptions>()
            .BindConfiguration(nameof(ElectronicsTime));

        services.AddGrpc();

        return services;
    }
}
