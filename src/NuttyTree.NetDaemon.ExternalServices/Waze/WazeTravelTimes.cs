using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;
using NuttyTree.NetDaemon.Infrastructure.RateLimiting;

namespace NuttyTree.NetDaemon.ExternalServices.Waze;

internal sealed class WazeTravelTimes : IWazeTravelTimes
{
    // Waze blocks requests to its routing servers that do not come from a browser so routes are retrieved using the
    // Home Assistant Waze Travel Time integration, which works around this.
    private const string WazeRegion = "us";

    private const double DefaultTravelMinutes = 15;

    // The search center Waze uses to rank address matches when one isn't provided
    private static readonly LocationCoordinates DefaultSearchLocation = new() { Latitude = 40.713, Longitude = -74.006 };

    private readonly IWazeCoordinatesApi wazeCoordinatesApi;

    private readonly IHaContext haContext;

    private readonly IRateLimiter<WazeTravelTimes> rateLimiter;

    private readonly ILogger<WazeTravelTimes> logger;

    public WazeTravelTimes(
        IWazeCoordinatesApi wazeCoordinatesApi,
        IHaContext haContext,
        IRateLimiter<WazeTravelTimes> rateLimiter,
        ILogger<WazeTravelTimes> logger)
    {
        this.wazeCoordinatesApi = wazeCoordinatesApi;
        this.haContext = haContext;
        this.rateLimiter = rateLimiter;
        this.logger = logger;
        rateLimiter.DefaultDelayBetweenTasks = TimeSpan.FromSeconds(15);
    }

    public async Task<AddressLocation?> GetAddressLocationFromAddressAsync(string? address, LocationCoordinates? nearLocation = null)
    {
        if (address == null)
        {
            return null;
        }

        await rateLimiter.WaitAsync();
        var suggestions = await wazeCoordinatesApi.GetAddressSuggestionsAsync(address, FormatCoordinates(nearLocation ?? DefaultSearchLocation));
        var location = ParseFirstLocation(suggestions);
        if (location == null)
        {
            logger.LogWarning("Waze returned no locations for address {Address}", address);
        }

        return location;
    }

    public async Task<TravelTime?> GetTravelTimeAsync(
        LocationCoordinates? fromLocation,
        LocationCoordinates? toLocation,
        DateTime arriveTime,
        double? expectedTravelMinutes = null)
    {
        if (fromLocation == null || toLocation == null)
        {
            return null;
        }

        await rateLimiter.WaitAsync();

        // The Home Assistant action calculates the route for leaving a number of minutes from now so
        // leave early enough to arrive at the requested time using the expected travel time
        var leaveInMinutes = (int)Math.Max(0, Math.Round((arriveTime - DateTime.Now).TotalMinutes - (expectedTravelMinutes ?? DefaultTravelMinutes)));

        JsonElement? response;
        try
        {
            response = await haContext.CallServiceWithResponseAsync(
                "waze_travel_time",
                "get_travel_times",
                null,
                new
                {
                    origin = FormatCoordinates(fromLocation),
                    destination = FormatCoordinates(toLocation),
                    region = WazeRegion,
                    units = "imperial",
                    realtime = false,
                    time_delta = new { minutes = leaveInMinutes },
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Home Assistant could not get the Waze travel time from {FromLocation} to {ToLocation}", FormatCoordinates(fromLocation), FormatCoordinates(toLocation));
            return null;
        }

        var route = GetFastestRoute(response);
        if (route == null)
        {
            logger.LogWarning(
                "Home Assistant returned no usable Waze route from {FromLocation} to {ToLocation}: {Response}",
                FormatCoordinates(fromLocation),
                FormatCoordinates(toLocation),
                response?.ToString());
            return null;
        }

        logger.LogInformation(
            "Waze route from {FromLocation} to {ToLocation} leaving in {LeaveInMinutes} minutes is {Miles:0.0} miles and {Minutes:0.0} minutes",
            FormatCoordinates(fromLocation),
            FormatCoordinates(toLocation),
            leaveInMinutes,
            route.Miles,
            route.Minutes);
        return route;
    }

    private static string FormatCoordinates(LocationCoordinates location)
        => string.Create(CultureInfo.InvariantCulture, $"{location.Latitude:0.000000},{location.Longitude:0.000000}");

    private static AddressLocation? ParseFirstLocation(JsonElement suggestions)
    {
        if (suggestions.ValueKind != JsonValueKind.Array
            || suggestions.GetArrayLength() < 2
            || suggestions[1].ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var suggestion in suggestions[1].EnumerateArray())
        {
            if (suggestion.ValueKind != JsonValueKind.Array
                || suggestion.GetArrayLength() < 4
                || suggestion[3].ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var place = suggestion[3];
            if (place.TryGetProperty("v", out var provider)
                && provider.ValueKind == JsonValueKind.String
                && provider.GetString()!.StartsWith("advertisement.poi-", StringComparison.Ordinal))
            {
                continue;
            }

            if (place.TryGetProperty("y", out var latitude)
                && latitude.ValueKind == JsonValueKind.Number
                && place.TryGetProperty("x", out var longitude)
                && longitude.ValueKind == JsonValueKind.Number)
            {
                return new AddressLocation
                {
                    Name = suggestion[0].ValueKind == JsonValueKind.String ? suggestion[0].GetString() : null,
                    Location = new LocationCoordinates { Latitude = latitude.GetDouble(), Longitude = longitude.GetDouble() },
                };
            }
        }

        return null;
    }

    private static TravelTime? GetFastestRoute(JsonElement? response)
    {
        if (response?.ValueKind != JsonValueKind.Object
            || !response.Value.TryGetProperty("routes", out var routes)
            || routes.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        TravelTime? fastest = null;
        foreach (var route in routes.EnumerateArray())
        {
            // Duration is in minutes and distance is in miles (imperial units are requested)
            if (route.ValueKind == JsonValueKind.Object
                && route.TryGetProperty("duration", out var duration)
                && duration.ValueKind == JsonValueKind.Number
                && route.TryGetProperty("distance", out var distance)
                && distance.ValueKind == JsonValueKind.Number
                && duration.GetDouble() > 0
                && distance.GetDouble() > 0
                && (fastest == null || duration.GetDouble() < fastest.Minutes))
            {
                fastest = new TravelTime(distance.GetDouble(), duration.GetDouble());
            }
        }

        return fastest;
    }
}
