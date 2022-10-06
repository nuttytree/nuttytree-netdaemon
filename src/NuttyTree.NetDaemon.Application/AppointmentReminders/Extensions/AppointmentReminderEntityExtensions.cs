using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;

internal static class AppointmentReminderEntityExtensions
{
    public static void Cancel(this AppointmentReminderEntity reminder)
    {
        reminder.NextAnnouncementType = NextAnnouncementType.None;
        reminder.NextAnnouncement = null;
        reminder.NextTravelTimeUpdate = null;
    }

    public static LocationCoordinates GetLocationCoordinates(this AppointmentReminderEntity reminder)
    {
        return new LocationCoordinates
        {
            Latitude = reminder.Appointment.Latitude ?? 0,
            Longitude = reminder.Appointment.Longitude ?? 0,
        };
    }

    public static DateTime GetArriveDateTime(this AppointmentReminderEntity reminder)
    {
        return (reminder.Type == ReminderType.Start ? reminder.Appointment.StartDateTime : reminder.Appointment.EndDateTime ?? DateTime.MaxValue)
            .AddMinutes(-1 * AppointmentConstants.AppointmentLeadTimeMinutes);
    }

    public static void SetTravelTime(this AppointmentReminderEntity reminder, TravelTime? travelTime)
    {
        if (travelTime != null)
        {
            reminder.TravelMiles = travelTime.Miles;
            reminder.TravelMinutes = travelTime.Minutes;
            reminder.NextTravelTimeUpdate = reminder.NextTravelTimeUpdate == DateTime.MinValue
                ? reminder.GetArriveDateTime().AddHours(-4)
                : DateTime.UtcNow.AddMinutes(5);
            reminder.NextAnnouncement = reminder.GetArriveDateTime()
                .AddMinutes(-1 * travelTime.Minutes)
                .AddMinutes(-1 * (int)reminder.NextAnnouncementType!);
        }
    }
}
