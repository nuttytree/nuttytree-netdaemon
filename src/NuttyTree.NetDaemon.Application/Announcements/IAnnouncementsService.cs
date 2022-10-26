using NuttyTree.NetDaemon.Application.Announcements.Models;

namespace NuttyTree.NetDaemon.Application.Announcements;

public interface IAnnouncementsService
{
    void DisableAnnouncements(int minutes);

    void EnableAnnouncements();

    void SendAnnouncement(
        string message,
        AnnouncementType type = AnnouncementType.General,
        AnnouncementPriority priority = AnnouncementPriority.Information,
        string? person = null);

    Task SendAnnouncementAsync(
        string message,
        AnnouncementType type = AnnouncementType.General,
        AnnouncementPriority priority = AnnouncementPriority.Information,
        string? person = null,
        CancellationToken cancellationToken = default);
}
