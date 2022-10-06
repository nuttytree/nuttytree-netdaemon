using NuttyTree.NetDaemon.ExternalServices.Waze.Models;

namespace NuttyTree.NetDaemon.ExternalServices.Waze;

public interface IWazeTravelTimes
{
    Task<AddressLocation?> GetAddressLocationFromAddressAsync(string? address);

    Task<TravelTime?> GetTravelTimeAsync(LocationCoordinates? fromLocation, LocationCoordinates? toLocation, DateTime arriveTime);
}
