namespace NuttyTree.NetDaemon.Application.Announcements.Models;

internal sealed class Announcement
{
    public Announcement(
        string message,
        AnnouncementType type,
        AnnouncementPriority priority,
        string? person)
    {
        Message = message;
        Type = type;
        Priority = priority;
        Person = person;
    }

    public DateTime QueuedAt { get; } = DateTime.UtcNow;

    public TaskCompletionSource IsComplete { get; } = new TaskCompletionSource();

    public string Message { get; }

    public AnnouncementType Type { get; }

    public AnnouncementPriority Priority { get; }

    public string? Person { get; }
}
