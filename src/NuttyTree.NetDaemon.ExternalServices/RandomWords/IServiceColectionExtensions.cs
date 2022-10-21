using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.RandomWords;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddRandomWords(this IServiceCollection services)
    {
        services.AddRefitClient<IRandomWordApi>()
            .AddDefaultRetryPolicy()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://random-word-api.herokuapp.com");
            });

        return services;
    }
}
