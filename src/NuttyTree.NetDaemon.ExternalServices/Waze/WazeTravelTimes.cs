using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NetDaemon.HassModel;
using NuttyTree.NetDaemon.ExternalServices.Nominatim;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.RateLimiting;

namespace NuttyTree.NetDaemon.ExternalServices.Waze;

internal sealed partial class WazeTravelTimes : IWazeTravelTimes
{
    // Waze blocks requests to its routing servers that do not come from a browser so routes are retrieved using the
    // Home Assistant Waze Travel Time integration, which works around this.
    private const string WazeRegion = "us";

    private const double DefaultTravelMinutes = 15;

    // Nominatim's usage policy allows at most one request per second
    private const string NominatimRateLimiterName = "Nominatim";

    // Half the size, in degrees, of the area around the search location that Nominatim ranks higher (roughly 70 miles)
    private const double SearchAreaDegrees = 1.0;

    private static readonly TimeSpan NominatimDelayBetweenRequests = TimeSpan.FromSeconds(1.1);

    private readonly INominatimApi nominatimApi;

    private readonly IHaContext haContext;

    private readonly IRateLimiter<WazeTravelTimes> rateLimiter;

    private readonly ILogger<WazeTravelTimes> logger;

    public WazeTravelTimes(
        INominatimApi nominatimApi,
        IHaContext haContext,
        IRateLimiter<WazeTravelTimes> rateLimiter,
        ILogger<WazeTravelTimes> logger)
    {
        this.nominatimApi = nominatimApi;
        this.haContext = haContext;
        this.rateLimiter = rateLimiter;
        this.logger = logger;
        rateLimiter.DefaultDelayBetweenTasks = TimeSpan.FromSeconds(15);
    }

    public async Task<AddressLocation?> GetAddressLocationFromAddressAsync(string? address, LocationCoordinates? nearLocation = null)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var viewbox = nearLocation == null ? null : FormatViewbox(nearLocation);
        foreach (var query in GetSearchQueries(address))
        {
            List<NominatimPlace> places;
            try
            {
                await rateLimiter.WaitAsync(NominatimRateLimiterName, NominatimDelayBetweenRequests);
                places = await nominatimApi.SearchAsync(query, viewbox);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Callers treat a missing location as "look it up again later", so don't let one failure stop them
                logger.LogWarning(ex, "Nominatim could not look up the location of {Address}", query);
                return null;
            }

            var location = ParseFirstLocation(places);
            if (location != null)
            {
                logger.LogInformation("Nominatim found {LocationName} for address {Address}", location.Name, address);
                return location;
            }
        }

        logger.LogWarning("Nominatim returned no locations for address {Address}", address);
        return null;
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

    // Nominatim's viewbox is <x1>,<y1>,<x2>,<y2>: two opposite corners as longitude,latitude
    private static string FormatViewbox(LocationCoordinates location)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{location.Longitude - SearchAreaDegrees:0.000000},{location.Latitude + SearchAreaDegrees:0.000000},{location.Longitude + SearchAreaDegrees:0.000000},{location.Latitude - SearchAreaDegrees:0.000000}");

    // Calendar locations often start with a place name Nominatim doesn't know ("Some Restaurant, 123 Main St, City, ST"),
    // so after the full location also try it from each part that starts with a street number
    private static IEnumerable<string> GetSearchQueries(string address)
    {
        var normalized = LineBreaks().Replace(address, ", ");
        var parts = normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var queries = new List<string> { string.Join(", ", parts) };
        for (var i = 1; i < parts.Length; i++)
        {
            if (char.IsDigit(parts[i][0]))
            {
                queries.Add(string.Join(", ", parts.Skip(i)));
            }
        }

        return queries.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static AddressLocation? ParseFirstLocation(List<NominatimPlace>? places)
    {
        foreach (var place in places ?? [])
        {
            if (double.TryParse(place.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
                && double.TryParse(place.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                return new AddressLocation
                {
                    Name = string.IsNullOrEmpty(place.Name) ? place.DisplayName : place.Name,
                    Location = new LocationCoordinates { Latitude = latitude, Longitude = longitude },
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

    [GeneratedRegex(@"\s*[\r\n]+\s*")]
    private static partial Regex LineBreaks();
}
