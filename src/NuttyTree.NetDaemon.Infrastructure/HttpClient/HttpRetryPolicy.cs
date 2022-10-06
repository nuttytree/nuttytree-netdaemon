using System.Net;
using Polly;

namespace NuttyTree.NetDaemon.Infrastructure.HttpClient;

internal static class HttpRetryPolicy
{
    internal static IAsyncPolicy<HttpResponseMessage> CreateDefaultPolicy() => Policy.WrapAsync(
            Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                .WaitAndRetryAsync(4, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))),
            Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout)
                .AdvancedCircuitBreakerAsync(0.9, TimeSpan.FromSeconds(60), 30, TimeSpan.FromSeconds(20)));
}
