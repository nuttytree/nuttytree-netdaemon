using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.Application.GuestPasswordUpdate.Options;

namespace NuttyTree.NetDaemon.Application.GuestPasswordUpdate;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddGuestPasswordUpdateApp(this IServiceCollection services)
    {
        services.AddOptions<GuestPasswordUpdateOptions>()
            .BindConfiguration(nameof(GuestPasswordUpdate))
            .ValidateDataAnnotations();

        return services;
    }
}
