using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi.Models;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;

internal interface IWazeApi
{
    private const string BaseLatitude = "40.713";

    private const string BaseLongitude = "74.006";

    private const string Language = "eng";

    private const string Origin = "livemap";

    [Get($"/SearchServer/mozi?q={{address}}&lat={BaseLatitude}&lon=-{BaseLongitude}&lang={Language}&origin={Origin}")]
    Task<List<AddressLocation>> GetAddressLocationFromAddressAsync(string address, CancellationToken cancellationToken = default);

    [Get($"/RoutingManager/routingRequest?from=x: {{from.Longitude}} y: {{from.Latitude}}&to=x: {{to.Longitude}} y: {{to.Latitude}}&at={{arriveAtOffsetMinutes}}&arriveAt=true&returnJSON=true&nPaths=1")]
    Task<RouteResponse> GetRouteAsync(LocationCoordinates from, LocationCoordinates to, int arriveAtOffsetMinutes);
}
