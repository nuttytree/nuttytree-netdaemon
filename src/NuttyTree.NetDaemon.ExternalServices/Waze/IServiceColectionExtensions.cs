using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Waze;

public static class IServiceColectionExtensions
{
    public static IServiceCollection AddWaze(this IServiceCollection services)
    {
        services.AddRefitGeneratedClient<IWazeCoordinatesApi>()
            .AddDefaultRetryPolicy()
            .ConfigureHttpClient(client =>
            {
                client.BaseAddress = new Uri("https://gapi.waze.com");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
                client.DefaultRequestHeaders.Referrer = new Uri("https://www.waze.com/");
            });

        return services.AddTransient<IWazeTravelTimes, WazeTravelTimes>();
    }
}
