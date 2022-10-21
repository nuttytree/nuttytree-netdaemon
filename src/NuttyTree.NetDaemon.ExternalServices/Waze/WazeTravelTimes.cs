using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;

namespace NuttyTree.NetDaemon.ExternalServices.Waze;

internal sealed class WazeTravelTimes : IWazeTravelTimes
{
    private static readonly SemaphoreSlim RateLimiter = new SemaphoreSlim(1, 1);

    private static CancellationTokenSource? releaseRateLimiter;

    private readonly IWazeApi wazeApi;

    public WazeTravelTimes(IWazeApi wazeApi)
    {
        this.wazeApi = wazeApi;
    }

    public Task<AddressLocation?> GetAddressLocationFromAddressAsync(string? address)
    {
        return address == null ? Task.FromResult<AddressLocation?>(null) : RateLimitRequestAsync(async () =>
        {
            var results = await wazeApi.GetAddressLocationFromAddressAsync(address);
            return results.FirstOrDefault();
        });
    }

    public Task<TravelTime?> GetTravelTimeAsync(LocationCoordinates? fromLocation, LocationCoordinates? toLocation, DateTime arriveTime)
    {
        return fromLocation == null || toLocation == null ? Task.FromResult<TravelTime?>(null) : RateLimitRequestAsync<TravelTime?>(async () =>
        {
            var offset = Convert.ToInt32((arriveTime - DateTime.Now).TotalMinutes);
            var route = await wazeApi.GetRouteAsync(fromLocation, toLocation, offset);
            var miles = (route.Response?.Results?.Select(s => s.Length).Sum() ?? 0) / 1609.0;  // Convert from meters to miles
            var minutes = (route.Response?.TotalRouteTime ?? 0) / 60.0; // Convert from seconds to minutes
            return new TravelTime(miles, minutes);
        });
    }

    private async Task<T> RateLimitRequestAsync<T>(Func<Task<T>> request)
    {
        try
        {
            await RateLimiter.WaitAsync();
            return await request();
        }
        finally
        {
            releaseRateLimiter = new CancellationTokenSource(15000);
            releaseRateLimiter.Token.Register(() =>
            {
                RateLimiter.Release();
            });
        }
    }
}
