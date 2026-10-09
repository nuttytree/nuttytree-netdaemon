using Microsoft.Extensions.Logging;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;
using NuttyTree.NetDaemon.Infrastructure.RateLimiting;

namespace NuttyTree.NetDaemon.ExternalServices.Waze;

internal sealed class WazeTravelTimes : IWazeTravelTimes
{
    private readonly IWazeCoordinatesApi wazeCoordinatesApi;

    private readonly IWazeRoutesApi wazeRoutesApi;

    private readonly IRateLimiter<WazeTravelTimes> rateLimiter;

    private readonly ILogger<WazeTravelTimes> logger;

    public WazeTravelTimes(
        IWazeCoordinatesApi wazeCoordinatesApi,
        IWazeRoutesApi wazeRoutesApi,
        IRateLimiter<WazeTravelTimes> rateLimiter,
        ILogger<WazeTravelTimes> logger)
    {
        this.wazeCoordinatesApi = wazeCoordinatesApi;
        this.wazeRoutesApi = wazeRoutesApi;
        this.rateLimiter = rateLimiter;
        this.logger = logger;
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
            var results = await wazeCoordinatesApi.GetAddressLocationFromAddressAsync(address);
            var location = results.FirstOrDefault();
            if (location == null)
            {
                logger.LogWarning("Waze returned no locations for address {Address}", address);
            }

            return location;
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
            var route = await wazeRoutesApi.GetRouteAsync(fromLocation, toLocation, offset);
            var meters = route.Response?.Results?.Select(s => s.Length).Sum() ?? 0;
            var seconds = route.Response?.TotalRouteTime ?? 0;
            if (meters <= 0 || seconds <= 0)
            {
                // Waze can return a successful response without a usable route, treat that as a failure instead of a zero length trip
                logger.LogWarning(
                    "Waze returned no usable route from {FromLocation} to {ToLocation}: {Meters} meters, {Seconds} seconds, response present: {HasResponse}",
                    $"{fromLocation.Latitude},{fromLocation.Longitude}",
                    $"{toLocation.Latitude},{toLocation.Longitude}",
                    meters,
                    seconds,
                    route.Response != null);
                return null;
            }

            var miles = meters / 1609.0;  // Convert from meters to miles
            var minutes = seconds / 60.0; // Convert from seconds to minutes
            logger.LogInformation(
                "Waze route from {FromLocation} to {ToLocation} is {Miles:0.0} miles and {Minutes:0.0} minutes",
                $"{fromLocation.Latitude},{fromLocation.Longitude}",
                $"{toLocation.Latitude},{toLocation.Longitude}",
                miles,
                minutes);
            return new TravelTime(miles, minutes);
        }
    }
}
