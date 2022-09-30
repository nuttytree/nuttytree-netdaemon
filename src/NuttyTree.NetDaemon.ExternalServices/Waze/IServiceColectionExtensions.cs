using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Waze
{
    public static class IServiceColectionExtensions
    {
        public static IServiceCollection AddWaze(this IServiceCollection services)
        {
            services.AddRefitClient<IWazeApi>()
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri("https://www.waze.com");
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
                });

            return services.AddTransient<IWazeTravelTimes, WazeTravelTimes>();
        }
    }
}
