using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi.Models;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;

internal interface IWazeRoutesApi
{
    [Get($"/RoutingManager/routingRequest?from=x: {{from.Longitude}} y: {{from.Latitude}}&to=x: {{to.Longitude}} y: {{to.Latitude}}&at={{arriveAtOffsetMinutes}}&arriveAt=true&returnJSON=true&nPaths=1")]
    Task<RouteResponse> GetRouteAsync(LocationCoordinates from, LocationCoordinates to, int arriveAtOffsetMinutes);
}
