using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NuttyTree.NetDaemon.ExternalServices.Unifi.Models;
using NuttyTree.NetDaemon.ExternalServices.Unifi.Options;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Unifi;

internal sealed class UnifiAuthHandler(
    IMemoryCache cache,
    IOptionsMonitor<UnifiOptions> options)
        : DelegatingHandler
{
#pragma warning disable CA2213 // Disposable fields should be disposed
    private readonly IMemoryCache cache = cache;
#pragma warning restore CA2213 // Disposable fields should be disposed

    private readonly IOptionsMonitor<UnifiOptions> options = options;

    private readonly SystemTextJsonContentSerializer serializer = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await cache.GetOrCreateAsync("unifi_auth_token", e => GetTokenAsync(e, cancellationToken));

        request.Headers.Add("x-csrf-token", token?.CsrfToken);
        request.Headers.Add("Cookie", token?.Cookie);

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<UnifiToken> GetTokenAsync(ICacheEntry cacheEntry, CancellationToken cancellationToken)
    {
        var loginRequest = new LoginRequest { Username = options.CurrentValue.UserName, Password = options.CurrentValue.Password };
        using var loginRequestMessage = new HttpRequestMessage(HttpMethod.Post, new Uri(options.CurrentValue.Url!, "/api/auth/login"))
        {
            Content = serializer.ToHttpContent(loginRequest)
        };
        using var loginResponse = await base.SendAsync(loginRequestMessage, cancellationToken);
        loginResponse.EnsureSuccessStatusCode();

        var tokenExpires = loginResponse.Headers.TryGetValues("x-token-expire-time", out var expVal) && double.TryParse(expVal.First(), out var expMs)
            ? DateTime.UnixEpoch.AddMilliseconds(expMs)
            : DateTime.UtcNow;
        cacheEntry.AbsoluteExpiration = tokenExpires.AddMinutes(-1);

        return new UnifiToken(
            loginResponse.Headers.TryGetValues("x-csrf-token", out var tokenVal) ? tokenVal.First() : string.Empty,
            loginResponse.Headers.TryGetValues("Set-Cookie", out var cookieVal) ? cookieVal.First() : string.Empty);
    }

    private sealed class UnifiToken(string csrfToken, string cookie)
    {
        public string CsrfToken { get; } = csrfToken;

        public string Cookie { get; } = cookie;
    }
}
