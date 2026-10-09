using System.Text.Json;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;

internal interface IWazeCoordinatesApi
{
    // The response is a heterogeneous JSON array so it is parsed by hand: [query, [[name, 0, [], { place }], ...], { ... }]
    [Get("/autocomplete/q?q={address}&sll={searchLocation}&e=NA&c=wd&exp=8&gxy=1&lang=en")]
    Task<JsonElement> GetAddressSuggestionsAsync(string address, string searchLocation, CancellationToken cancellationToken = default);
}
