namespace NuttyTree.NetDaemon.Infrastructure.Database.Entities;

public sealed class AppointmentEntity
{
    public AppointmentEntity(
        string id,
        string calendar,
        string summary,
        string? description,
        string? location,
        DateTime startDateTime,
        DateTime? endDateTime,
        bool isAllDay)
    {
        Id = id;
        Calendar = calendar;
        Summary = summary;
        Description = description;
        Location = location;
        StartDateTime = startDateTime;
        EndDateTime = endDateTime;
        IsAllDay = isAllDay;
    }

    public string Id { get; set; }

    public string Calendar { get; set; }

    public string Summary { get; set; }

    public string? Description { get; set; }

    public string? Location { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime? EndDateTime { get; set; }

    public bool IsAllDay { get; set; }

    public string? Person { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public List<AppointmentReminderEntity> Reminders { get; set; } = new List<AppointmentReminderEntity>();
}
