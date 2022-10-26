using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;
using NuttyTree.NetDaemon.Infrastructure.RateLimiting;

namespace NuttyTree.NetDaemon.ExternalServices.Waze;

internal sealed class WazeTravelTimes : IWazeTravelTimes
{
    private readonly IWazeApi wazeApi;

    private readonly IRateLimiter<WazeTravelTimes> rateLimiter;

    public WazeTravelTimes(IWazeApi wazeApi, IRateLimiter<WazeTravelTimes> rateLimiter)
    {
        this.wazeApi = wazeApi;
        this.rateLimiter = rateLimiter;
        rateLimiter.DefaultDelayBetweenTasks = TimeSpan.FromSeconds(15);
    }

    public async Task<AddressLocation?> GetAddressLocationFromAddressAsync(string? address)
    {
        if (address == null)
        {
            return null;
        }
        else
        {
            await rateLimiter.WaitAsync();
            var results = await wazeApi.GetAddressLocationFromAddressAsync(address);
            return results.FirstOrDefault();
        }
    }

    public async Task<TravelTime?> GetTravelTimeAsync(LocationCoordinates? fromLocation, LocationCoordinates? toLocation, DateTime arriveTime)
    {
        if (fromLocation == null || toLocation == null)
        {
            return null;
        }
        else
        {
            await rateLimiter.WaitAsync();
            var offset = Convert.ToInt32((arriveTime - DateTime.Now).TotalMinutes);
            var route = await wazeApi.GetRouteAsync(fromLocation, toLocation, offset);
            var miles = (route.Response?.Results?.Select(s => s.Length).Sum() ?? 0) / 1609.0;  // Convert from meters to miles
            var minutes = (route.Response?.TotalRouteTime ?? 0) / 60.0; // Convert from seconds to minutes
            return new TravelTime(miles, minutes);
        }
    }
}
