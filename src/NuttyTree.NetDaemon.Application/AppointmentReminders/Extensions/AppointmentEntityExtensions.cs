using NuttyTree.NetDaemon.Application.AppointmentReminders.Options;
using NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;
using static NuttyTree.NetDaemon.Application.AppointmentReminders.AppointmentConstants;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;
internal static class AppointmentEntityExtensions
{
    public static string? GetOverrideValue(this AppointmentEntity appointment, string valueName)
    {
        return appointment.Description?
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Where(sl => sl.Length == 2)
                .Select(sl => KeyValuePair.Create(sl[0], sl[1]))
                .FirstOrDefault(kv => kv.Key.CaseInsensitiveEquals(valueName))
                .Value;
    }

    public static bool TryGetOverrideValue(this AppointmentEntity appointment, string valueName, out string? value)
    {
        value = appointment.Description?
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(sl => sl.Length == 2)
            .Select(sl => KeyValuePair.Create(sl[0], sl[1]))
            .FirstOrDefault(kv => kv.Key.CaseInsensitiveEquals(valueName))
            .Value;
        return value != null;
    }

    public static bool HasChanged(this AppointmentEntity existing, HomeAssistantAppointment updated)
    {
        return
            existing.Id == updated.Id
            && (existing.Description != updated.Description || existing.EndDateTime != updated.GetEndDateTime() || existing.IsAllDay != updated.GetIsAllDay());
    }

    public static AppointmentEntity Update(this AppointmentEntity existing, HomeAssistantAppointment updated)
    {
        if (existing.Id == updated.Id)
        {
            existing.Description = updated.Description;
            existing.EndDateTime = updated.GetEndDateTime();
            existing.IsAllDay = updated.GetIsAllDay();
        }

        return existing;
    }

    public static void SetAppointmentPerson(this AppointmentEntity appointment)
    {
        appointment.Person = (appointment.Calendar == FamilyCalendar ? appointment.GetOverrideValue(nameof(appointment.Person)) : null) ?? appointment.Person;
        appointment.Person ??= appointment.Calendar == ScoutsCalendar ? Mayson : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.StartsWith("chris' ", StringComparison.OrdinalIgnoreCase) ? Chris : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.StartsWith("melissa's ", StringComparison.OrdinalIgnoreCase) ? Melissa : null;
        appointment.Person ??= appointment.Calendar == FamilyCalendar && appointment.Summary.StartsWith("mayson's ", StringComparison.OrdinalIgnoreCase) ? Mayson : null;
    }

    public static LocationCoordinates? GetKnownLocationCoordinates(this AppointmentEntity appointment, AppointmentRemindersOptions options)
    {
        if (appointment.Calendar == FamilyCalendar &&
            (appointment.Location!.Replace(" ", string.Empty, StringComparison.Ordinal)?
                .Contains(options.HomeAddress!.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase) == true
                    || string.Equals(appointment.Location, "Home", StringComparison.OrdinalIgnoreCase)))
        {
            return options.HomeLocation;
        }
        else if (appointment.Calendar == FamilyCalendar &&
            appointment.Location!.Replace(" ", string.Empty, StringComparison.Ordinal)?.Contains("RidgewoodChurch", StringComparison.OrdinalIgnoreCase) == true)
        {
            return RidgewoodChurchLocation;
        }
        else if (appointment.Calendar == ScoutsCalendar &&
            (appointment.Location!.Contains("Various Sites in EP", StringComparison.OrdinalIgnoreCase)
            || appointment.Location.Contains("Other or TBD", StringComparison.OrdinalIgnoreCase)
            || appointment.Location.Contains("Pax Christi", StringComparison.OrdinalIgnoreCase)))
        {
            // If any these values are in the location odds are it will be at the Church or somewhere close so drive time will be similar
            return DefaultScoutsLocation;
        }
        else if (appointment.Calendar == ScoutsCalendar &&
            (appointment.Location!.Contains("Zoom", StringComparison.OrdinalIgnoreCase)
            || appointment.Location.Contains("Online", StringComparison.OrdinalIgnoreCase)))
        {
            // If it is online it will be at home
            return options.HomeLocation;
        }

        return null;
    }

    public static void SetLocationCoordinates(this AppointmentEntity appointment, LocationCoordinates coordinates)
    {
        appointment.Latitude = coordinates.Latitude;
        appointment.Longitude = coordinates.Longitude;
    }

    public static LocationCoordinates GetLocationCoordinates(this AppointmentEntity appointment)
    {
        return new LocationCoordinates
        {
            Latitude = appointment.Latitude ?? 0,
            Longitude = appointment.Longitude ?? 0,
        };
    }

    public static void SetAppointmentReminderOptions(this AppointmentEntity appointment, AppointmentRemindersOptions options)
    {
        var startReminder = appointment.GetStartReminder();
        var endReminder = appointment.GetEndReminder();

        if (appointment.Person == Mayson)
        {
            // We are going to work on reducing announcements before we introduce more announcements
            //endReminder.SetDefaultAnnouncementTypes(AnnouncementType.FifteenMinutes, AnnouncementType.Now);
        }

        if (appointment.GetLocationCoordinates().Equals(options.HomeLocation))
        {
            startReminder.ArriveLeadMinutes ??= 0;
        }

        startReminder.SetDefaultAnnouncementTypes(AnnouncementType.TwoHours, AnnouncementType.OneHour, AnnouncementType.ThirtyMinutes, AnnouncementType.FifteenMinutes, AnnouncementType.Now);
        startReminder.ArriveLeadMinutes ??= options.DefaultStartLeadMinutes;
        endReminder.SetDefaultAnnouncementTypes();
        endReminder.ArriveLeadMinutes ??= options.DefaultEndLeadMinutes;

        if (appointment.Calendar == FamilyCalendar)
        {
            if (appointment.TryGetOverrideValue("Reminders", out var reminders))
            {
                startReminder.AnnouncementTypes = reminders;
            }

            if (appointment.TryGetOverrideValue("LeadTime", out var leadTimeString) && int.TryParse(leadTimeString, out var leadTime))
            {
                startReminder.ArriveLeadMinutes = leadTime;
            }

            if (appointment.TryGetOverrideValue("EndReminders", out var endReminders))
            {
                endReminder.AnnouncementTypes = endReminders;
            }

            if (appointment.TryGetOverrideValue("EndLeadTime", out var endLeadTimeString) && int.TryParse(endLeadTimeString, out var endLeadTime))
            {
                endReminder.ArriveLeadMinutes = endLeadTime;
            }
        }

        if (appointment.Calendar == ScoutsCalendar && appointment.Summary.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            startReminder.Cancel();
            endReminder.Cancel();
        }

        startReminder.SetNextAnnouncementType();
        endReminder.SetNextAnnouncementType();

        startReminder.NextTravelTimeUpdate = startReminder.NextAnnouncementType == AnnouncementType.None ? null : DateTime.MinValue;
        endReminder.NextTravelTimeUpdate = endReminder.NextAnnouncementType == AnnouncementType.None ? null : DateTime.MinValue;
    }

    public static AppointmentReminderEntity GetStartReminder(this AppointmentEntity appointment)
        => appointment.Reminders.First(r => r.Type == ReminderType.Start);

    public static AppointmentReminderEntity GetEndReminder(this AppointmentEntity appointment)
        => appointment.Reminders.First(r => r.Type == ReminderType.End);
}
