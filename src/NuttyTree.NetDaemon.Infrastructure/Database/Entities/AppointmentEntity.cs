using System.ComponentModel.DataAnnotations;

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

    [MaxLength(100)]
    public string Id { get; set; }

    [MaxLength(100)]
    public string Calendar { get; set; }

    [MaxLength(100)]
    public string Summary { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    public DateTime StartDateTime { get; set; }

    public DateTime? EndDateTime { get; set; }

    public bool IsAllDay { get; set; }

    [MaxLength(20)]
    public string? Person { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public ICollection<AppointmentReminderEntity> Reminders { get; set; } = new List<AppointmentReminderEntity>();
}
