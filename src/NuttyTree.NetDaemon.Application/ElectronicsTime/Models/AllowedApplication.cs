using System.ComponentModel.DataAnnotations;
using NuttyTree.NetDaemon.Application.ElectronicsTime.gRPC;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.Models;

internal sealed class AllowedApplication
{
    [Required]
    public string? Name { get; set; }

    public IList<string> AllowedWindowTitles { get; set; } = new List<string>();

    public IList<string> DeniedWindowTitles { get; set; } = new List<string>();

    public bool RequiresTime { get; set; }

    public ApplicationAllowType AllowType { get; set; }

    public IList<ApplicationLocation> AllowedLocations { get; set; } = new List<ApplicationLocation>();
}
