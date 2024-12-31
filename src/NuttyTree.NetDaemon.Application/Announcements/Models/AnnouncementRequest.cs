using System.Diagnostics.CodeAnalysis;

namespace NuttyTree.NetDaemon.Application.Announcements.Models;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Must match HA exactly")]
internal record AnnouncementRequest(
    string message,
    string type = nameof(AnnouncementType.General),
    string priority = nameof(AnnouncementPriority.Information),
    string? person = null);
