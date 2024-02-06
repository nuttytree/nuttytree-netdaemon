using NuttyTree.NetDaemon.Application.ElectronicsTime.gRPC;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.Models;

internal sealed class AllowedLocation
{
    public string? Location { get; set; }

    public ApplicationAllowType AllowType { get; set; }
}
