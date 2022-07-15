using NuttyTree.NetDaemon.ExternalServices.RandomWords;
using Refit;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class IServiceColectionExtensions
    {
        public static IServiceCollection AddRandomWords(this IServiceCollection services)
        {
            services.AddRefitClient<IRandomWordApi>()
                .ConfigureHttpClient(client =>
                {
                    client.BaseAddress = new Uri("https://random-word-api.herokuapp.com");
                });

            return services;
        }
    }
}
