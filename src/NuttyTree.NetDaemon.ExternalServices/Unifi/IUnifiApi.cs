using NuttyTree.NetDaemon.ExternalServices.Unifi.Models;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.Unifi;

public interface IUnifiApi
{
    [Get("/proxy/network/api/s/default/rest/wlanconf")]
    Task<ApiResponse<UnifResponse<WirelessNetwork>>> GetWirelessNetworksAsync(CancellationToken cancellationToken = default);

    Task UpdateWirelessNetworkPassphraseAsync(string id, string passphrase, CancellationToken cancellationToken = default)
        => UpdateWirelessNetworkAsync(id, new { x_passphrase = passphrase }, cancellationToken);

    [Put("/proxy/network/api/s/default/rest/wlanconf/{id}")]
    internal Task UpdateWirelessNetworkAsync(string id, object updatedValues, CancellationToken cancellationToken);
}
