using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.Application.ElectronicsTime.Options;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddElectronicsTimeApp(this IServiceCollection services)
    {
        services.AddOptions<ElectronicsTimeOptions>()
            .BindConfiguration(nameof(ElectronicsTime));

        return services;
    }
}
