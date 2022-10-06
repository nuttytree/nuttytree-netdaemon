namespace NuttyTree.NetDaemon.Application.Announcements.Models;

internal record AnnouncementRequest(string message, string priority = nameof(AnnouncementPriority.Information), string? person = null);
