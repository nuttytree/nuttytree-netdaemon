using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetDaemon.Client.Settings;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.HomeAssistantWebhook;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddHomeAssistantWebhooks(this IServiceCollection services)
    {
        services.AddRefitClient<IHomeAssistantWebhookApi>()
            .AddDefaultRetryPolicy()
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<HomeAssistantSettings>>().Value;
                client.BaseAddress = new UriBuilder(settings.Ssl ? "https" : "http", settings.Host, settings.Port).Uri;
            });

        return services;
    }
}
