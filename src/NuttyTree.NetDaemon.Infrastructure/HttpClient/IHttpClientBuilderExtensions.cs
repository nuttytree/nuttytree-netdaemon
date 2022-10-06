using Microsoft.Extensions.Http;
using NuttyTree.NetDaemon.Infrastructure.HttpClient;

namespace Microsoft.Extensions.DependencyInjection;

public static class IHttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddDefaultRetryPolicy(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler(() => new PolicyHttpMessageHandler(HttpRetryPolicy.CreateDefaultPolicy()));
    }
}
