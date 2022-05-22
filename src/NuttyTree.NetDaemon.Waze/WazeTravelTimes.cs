using NuttyTree.NetDaemon.Waze.Models;
using NuttyTree.NetDaemon.Waze.WazeApi;

namespace NuttyTree.NetDaemon.Waze
{
    internal class WazeTravelTimes : IWazeTravelTimes
    {
        private readonly IWazeApi wazeApi;

        public WazeTravelTimes(IWazeApi wazeApi)
        {
            this.wazeApi = wazeApi;
        }

        public async Task<AddressLocation?> GetAddressLocationFromAddressAsync(string? address)
        {
            if (address == null)
            {
                return null;
            }
            else
            {
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
                var offset = Convert.ToInt32((arriveTime - DateTime.Now).TotalMinutes);
                var route = await wazeApi.GetRouteAsync(fromLocation, toLocation, offset);
                var miles = (route.Response?.Results?.Select(s => s.Length).Sum() ?? 0) / 1609.0;  // Convert from meters to miles
                var minutes = (route.Response?.TotalRouteTime ?? 0) / 60.0; // Convert from seconds to minutes
                return new TravelTime(miles, minutes);
            }
        }
    }
}
