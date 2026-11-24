using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.ExternalServices.Nominatim;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Waze;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddWaze(this IServiceCollection services)
    {
        // Addresses are geocoded with OpenStreetMap Nominatim because Waze's address search no longer accepts
        // requests that don't come from a browser. Nominatim requires a User-Agent that identifies the application.
        services.AddRefitGeneratedClient<INominatimApi>()
            .AddDefaultRetryPolicy()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://nominatim.openstreetmap.org");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NuttyTree.NetDaemon", "1.0"));
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/nuttytree/nuttytree-netdaemon)"));
            });

        return services.AddTransient<IWazeTravelTimes, WazeTravelTimes>();
    }
}
