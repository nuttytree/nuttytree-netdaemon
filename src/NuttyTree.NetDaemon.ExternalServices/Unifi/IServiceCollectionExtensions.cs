using System.Net.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.ExternalServices.Unifi.Options;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Unifi;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddUnifi(this IServiceCollection services)
    {
        services.AddOptions<UnifiOptions>()
            .BindConfiguration("Unifi")
            .ValidateDataAnnotations();

        services.AddSingleton<UnifiAuthHandler>();

        services.AddRefitClient<IUnifiApi>()
            .AddHttpMessageHandler<UnifiAuthHandler>()
            .ConfigureHttpClient((sp, c) => c.BaseAddress = sp.GetRequiredService<IOptionsMonitor<UnifiOptions>>().CurrentValue.Url)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
#pragma warning disable CA5359 // Do Not Disable Certificate Validation
                SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true }
#pragma warning restore CA5359 // Do Not Disable Certificate Validation
            });

        return services;
    }
}
