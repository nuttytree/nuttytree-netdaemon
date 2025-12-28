using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi;

internal interface IWazeCoordinatesApi
{
    private const string BaseLatitude = "40.713";

    private const string BaseLongitude = "74.006";

    private const string Language = "eng";

    private const string Origin = "livemap";

    [Get($"/SearchServer/mozi?q={{address}}&lat={BaseLatitude}&lon=-{BaseLongitude}&lang={Language}&origin={Origin}")]
    Task<List<AddressLocation>> GetAddressLocationFromAddressAsync(string address, CancellationToken cancellationToken = default);
}
