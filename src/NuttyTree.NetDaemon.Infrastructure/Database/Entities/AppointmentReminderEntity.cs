namespace NuttyTree.NetDaemon.Infrastructure.Database.Entities;

public sealed class AppointmentReminderEntity
{
    public AppointmentReminderEntity(string id, ReminderType type)
    {
        Id = id;
        Type = type;
    }

    public string Id { get; set; }

    public ReminderType Type { get; set; }

    public int? ArriveLeadMinutes { get; set; }

    public string? AnnouncementTypes { get; set; }

    public AppointmentAnnouncementType? NextAnnouncementType { get; set; }

    public DateTime? NextAnnouncement { get; set; }

    public DateTime? LastAnnouncement { get; set; }

    public double? TravelMiles { get; set; }

    public double? TravelMinutes { get; set; }

    public DateTime? NextTravelTimeUpdate { get; set; }

    public AppointmentEntity Appointment { get; set; } = null!;
}
