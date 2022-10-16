using NuttyTree.NetDaemon.Application.AppointmentReminders.Options;
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
        => reminder.Appointment.GetLocationCoordinates();

    public static DateTime GetArriveDateTime(this AppointmentReminderEntity reminder)
    {
        return (reminder.Type == ReminderType.Start ? reminder.Appointment.StartDateTime : reminder.Appointment.EndDateTime ?? DateTime.MaxValue)
            .AddMinutes(-1 * reminder.ArriveLeadMinutes ?? 0);
    }

    public static void SetTravelTime(this AppointmentReminderEntity reminder, TravelTime? travelTime, AppointmentRemindersOptions options)
    {
        if (travelTime != null)
        {
            reminder.TravelMiles = travelTime.Miles;
            reminder.TravelMinutes = travelTime.Minutes;
            if (travelTime.Miles > options.MaxReminderMiles)
            {
                reminder.Cancel();
            }
            else
            {
                var arriveDateTime = reminder.GetArriveDateTime();
                reminder.NextTravelTimeUpdate = reminder.TravelMiles == 0
                    ? null
                    : arriveDateTime.AddHours(-4) > DateTime.UtcNow
                        ? arriveDateTime.AddHours(-4)
                        : DateTime.UtcNow.AddMinutes(5);
                reminder.SetNextAnnouncementDateTime();
            }
        }
    }

    public static string GetReminderMessage(this AppointmentReminderEntity reminder, AppointmentRemindersOptions options)
    {
        var isAtHome = reminder.GetLocationCoordinates().Equals(options.HomeLocation);
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

    public static void UpdateNextAnnouncementTypeAndTime(this AppointmentReminderEntity reminder)
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
        reminder.SetNextAnnouncementDateTime();
    }

    private static void SetNextAnnouncementDateTime(this AppointmentReminderEntity reminder)
    {
        reminder.NextAnnouncement = reminder.NextAnnouncementType == NextAnnouncementType.None
            ? null
            : reminder.GetArriveDateTime()
                .AddMinutes(-1 * reminder.TravelMinutes!.Value)
                .AddMinutes(-1 * (int)reminder.NextAnnouncementType!);
    }
}
