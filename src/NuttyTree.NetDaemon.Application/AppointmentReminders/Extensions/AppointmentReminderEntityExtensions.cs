using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;
using static NuttyTree.NetDaemon.Application.AppointmentReminders.AppointmentConstants;

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
            .AddMinutes(-1 * reminder.ArriveLeadMinutes ?? 0);
    }

    public static void SetTravelTime(this AppointmentReminderEntity reminder, TravelTime? travelTime, int maxReminderMiles)
    {
        if (travelTime != null)
        {
            reminder.TravelMiles = travelTime.Miles;
            reminder.TravelMinutes = travelTime.Minutes;
            if (travelTime.Miles > maxReminderMiles)
            {
                reminder.Cancel();
            }
            else
            {
                reminder.NextTravelTimeUpdate = reminder.NextTravelTimeUpdate == DateTime.MinValue
                    ? reminder.GetArriveDateTime().AddHours(-4)
                    : DateTime.UtcNow.AddMinutes(5);
                reminder.NextAnnouncement = reminder.GetArriveDateTime()
                    .AddMinutes(-1 * travelTime.Minutes)
                    .AddMinutes(-1 * (int)reminder.NextAnnouncementType!);
            }
        }
    }

    public static string GetReminderMessage(this AppointmentReminderEntity reminder)
    {
        var isAtHome = reminder.GetLocationCoordinates().Equals(HomeLocation);
        var reminderMessage = string.Empty;

        if (reminder.Appointment.Person == null)
        {
            reminderMessage += "you";
            reminderMessage += isAtHome ? " have " : " need to leave for ";
        }
        else
        {
            reminderMessage += reminder.Appointment.Person;
            reminderMessage += isAtHome ? " has " : " needs to leave for ";
        }

        reminderMessage += reminder.Appointment.Summary;

        reminderMessage += reminder.NextAnnouncementType switch
        {
            NextAnnouncementType.TwoHours => $" in {(isAtHome ? null : "approximately")} 2 hours",
            NextAnnouncementType.OneHour => $" in {(isAtHome ? null : "approximately")} 1 hour",
            NextAnnouncementType.ThirtyMinutes => $" in {(isAtHome ? null : "approximately")} 30 minutes",
            NextAnnouncementType.FifteenMinutes => $" in {(isAtHome ? null : "approximately")} 15 minutes",
            NextAnnouncementType.Now => $" right now",
            _ => string.Empty,
        };

        return reminderMessage;
    }

    public static void SetNextAnnouncementTypeAndTime(this AppointmentReminderEntity reminder)
    {
        reminder.LastAnnouncement = DateTime.UtcNow;
        reminder.NextAnnouncementType = reminder.NextAnnouncementType switch
        {
            NextAnnouncementType.TwoHours => NextAnnouncementType.OneHour,
            NextAnnouncementType.OneHour => NextAnnouncementType.ThirtyMinutes,
            NextAnnouncementType.ThirtyMinutes => NextAnnouncementType.FifteenMinutes,
            NextAnnouncementType.FifteenMinutes => NextAnnouncementType.Now,
            _ => NextAnnouncementType.None,
        };
        reminder.NextAnnouncement = reminder.NextAnnouncementType == NextAnnouncementType.None
            ? null
            : reminder.GetArriveDateTime()
                .AddMinutes(-1 * reminder.TravelMinutes!.Value)
                .AddMinutes(-1 * (int)reminder.NextAnnouncementType!);
    }
}
