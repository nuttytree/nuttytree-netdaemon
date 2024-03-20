using NuttyTree.NetDaemon.ExternalServices.Waze.Models;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Options;

internal sealed class AppointmentRemindersOptions
{
    public int AppointmentUpdatesSchedulePeriod { get; set; } = 60;

    public string? HomeAddress { get; set; }

    public LocationCoordinates? HomeLocation { get; set; }

    public string? IgnoreScoutsAppointmntRegex { get; set; }

    public int MaxReminderMiles { get; set; } = 100;

    public int DefaultStartLeadMinutes { get; set; } = 5;

    public int DefaultEndLeadMinutes { get; set; }

    public bool NotificationOfExceptions { get; set; } = true;
}
