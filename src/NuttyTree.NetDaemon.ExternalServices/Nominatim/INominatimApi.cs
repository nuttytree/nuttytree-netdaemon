using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Nominatim;

// OpenStreetMap Nominatim geocoding: https://nominatim.org/release-docs/latest/api/Search/
// The public server's usage policy (https://operations.osmfoundation.org/policies/nominatim/) allows at most
// one request per second from a client that identifies itself with its own User-Agent, and asks that results are cached.
internal interface INominatimApi
{
    // viewbox ranks results near that area higher without excluding results outside it (bounded is off)
    [Get("/search?format=jsonv2&limit=5&countrycodes=us&accept-language=en")]
    Task<List<NominatimPlace>> SearchAsync(
        [AliasAs("q")] string query,
        [AliasAs("viewbox")] string? viewbox,
        CancellationToken cancellationToken = default);
}
