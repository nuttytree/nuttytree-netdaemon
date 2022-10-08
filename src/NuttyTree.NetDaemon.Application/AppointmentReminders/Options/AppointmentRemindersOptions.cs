namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Options;

internal class AppointmentRemindersOptions
{
    public int AppointmentUpdatesSchedulePeriod { get; set; } = 30;

    public int CoordinateUpdatesSchedulePeriod { get; set; } = 10;

    public int CreateRemindersSchedulePeriod { get; set; } = 10;

    public int TravelTimeUpdatesSchedulePeriod { get; set; } = 10;

    public int AnnounceRemindersSchedulePeriod { get; set; } = 10;

    public int MaxReminderMiles { get; set; } = 100;

    public int DefaultArriveLeadMinutes { get; set; } = 5;

    public bool NotificationOfExceptions { get; set; } = true;
}
