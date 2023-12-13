using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Frigate;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddFrigate(this IServiceCollection services)
    {
        services.AddRefitClient<IFrigateApi>()
            .AddDefaultRetryPolicy()
            .ConfigureHttpClient((serviceProvider, client) =>
            {
                var url = serviceProvider.GetRequiredService<IConfiguration>().GetValue<Uri>("FrigateUrl");
                client.BaseAddress = url;
            });

        return services;
    }
}
