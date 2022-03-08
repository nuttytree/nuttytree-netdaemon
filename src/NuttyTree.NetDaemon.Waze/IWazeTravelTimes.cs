using NuttyTree.NetDaemon.Waze.Models;

namespace NuttyTree.NetDaemon.Waze
{
    public interface IWazeTravelTimes
    {
        Task<AddressLocation?> GetAddressLocationFromAddressAsync(string? address);

        Task<TravelTime?> GetTravelTimeAsync(LocationCoordinates? fromLocation, LocationCoordinates? toLocation, DateTime arriveTime);
    }
}
