namespace NuttyTree.NetDaemon.Application.Announcements.Models;

internal record AnnouncementRequest(
    string message,
    string type = nameof(AnnouncementType.General),
    string priority = nameof(AnnouncementPriority.Information),
    string? person = null);
