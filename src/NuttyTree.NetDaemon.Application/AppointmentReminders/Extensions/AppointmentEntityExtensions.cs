using System.Diagnostics.CodeAnalysis;
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
                .FirstOrDefault(sl => sl[0].CaseInsensitiveEquals(valueName))?
                .Last();
    }

    public static bool TryGetOverrideValue(this AppointmentEntity appointment, string valueName, [MaybeNullWhen(false)] out string value)
    {
        value = GetOverrideValue(appointment, valueName);
        return value != null;
    }

    public static bool? GetBoolOverrideValue(this AppointmentEntity appointment, string valueName)
    {
        var stringValue = appointment.GetOverrideValue(valueName);
        return stringValue == null ? null : stringValue.CaseInsensitiveEquals("true") || stringValue.CaseInsensitiveEquals("yes");
    }

    public static bool TryGetBoolOverrideValue(this AppointmentEntity appointment, string valueName, out bool value)
    {
        var maybeValue = appointment.GetBoolOverrideValue(valueName);
        value = maybeValue ?? false;
        return maybeValue != null;
    }

    public static int? GetIntOverrideValue(this AppointmentEntity appointment, string valueName)
    {
        var stringValue = appointment.GetOverrideValue(valueName);
        return stringValue == null
            ? null
            : int.TryParse(stringValue, out var intValue)
                ? intValue
                : null;
    }

    public static bool TryGetIntOverrideValue(this AppointmentEntity appointment, string valueName, out int value)
    {
        var maybeValue = appointment.GetIntOverrideValue(valueName);
        value = maybeValue ?? 0;
        return maybeValue != null;
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
        if (appointment.Calendar == ScoutsCalendar)
        {
            appointment.Person = Mayson;
        }
        else if (appointment.TryGetOverrideValue(nameof(appointment.Person), out var person))
        {
            appointment.Person = person;
        }
        else if (appointment.Summary.StartsWith("chris'", StringComparison.OrdinalIgnoreCase))
        {
            appointment.Person = Chris;
            appointment.Summary = appointment.Summary.Replace("chris'", "his", StringComparison.OrdinalIgnoreCase);
        }
        else if (appointment.Summary.StartsWith("melissa's ", StringComparison.OrdinalIgnoreCase))
        {
            appointment.Person = Chris;
            appointment.Summary = appointment.Summary.Replace("melissa's", "her", StringComparison.OrdinalIgnoreCase);
        }
        else if (appointment.Summary.StartsWith("mayson's ", StringComparison.OrdinalIgnoreCase))
        {
            appointment.Person = Chris;
            appointment.Summary = appointment.Summary.Replace("mayson's", "his", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static LocationCoordinates? GetKnownLocationCoordinates(this AppointmentEntity appointment, AppointmentRemindersOptions options)
    {
        if (appointment.Calendar == FamilyCalendar &&
            (appointment.Location!.Replace(" ", string.Empty, StringComparison.Ordinal)
                !.Contains(options.HomeAddress!.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(appointment.Location, "Home", StringComparison.OrdinalIgnoreCase)))
        {
            return options.HomeLocation;
        }
        else if (appointment.Calendar == FamilyCalendar &&
            appointment.Location!.Replace(" ", string.Empty, StringComparison.Ordinal)
                !.Contains("RidgewoodChurch", StringComparison.OrdinalIgnoreCase))
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

        startReminder.SetDefaultAnnouncementTypes(120, 60, 30, 15, 0);
        startReminder.ArriveLeadMinutes ??= options.DefaultStartLeadMinutes;
        endReminder.SetDefaultAnnouncementTypes();
        endReminder.ArriveLeadMinutes ??= options.DefaultEndLeadMinutes;

        if (appointment.Calendar == FamilyCalendar)
        {
            if (appointment.TryGetOverrideValue("Reminders", out var reminders))
            {
                startReminder.AnnouncementTypes = reminders;
            }

            if (appointment.TryGetIntOverrideValue("LeadTime", out var leadTime))
            {
                startReminder.ArriveLeadMinutes = leadTime;
            }

            if (appointment.TryGetBoolOverrideValue("Priority", out var priority))
            {
                startReminder.Priority = priority;
            }

            if (appointment.TryGetOverrideValue("EndReminders", out var endReminders))
            {
                endReminder.AnnouncementTypes = endReminders;
            }

            if (appointment.TryGetIntOverrideValue("EndLeadTime", out var endLeadTime))
            {
                endReminder.ArriveLeadMinutes = endLeadTime;
            }

            if (appointment.TryGetBoolOverrideValue("EndPriority", out var endPriority))
            {
                endReminder.Priority = endPriority;
            }
        }

        if (appointment.Calendar == ScoutsCalendar && appointment.Summary.Contains("cancel", StringComparison.OrdinalIgnoreCase))
        {
            startReminder.Cancel();
            endReminder.Cancel();
        }

        startReminder.SetNextAnnouncementType();
        endReminder.SetNextAnnouncementType();

        startReminder.NextTravelTimeUpdate = startReminder.NextAnnouncementType == -1 ? null : DateTime.MinValue;
        endReminder.NextTravelTimeUpdate = endReminder.NextAnnouncementType == -1 ? null : DateTime.MinValue;
    }

    public static AppointmentReminderEntity GetStartReminder(this AppointmentEntity appointment)
        => appointment.Reminders.First(r => r.Type == ReminderType.Start);

    public static AppointmentReminderEntity GetEndReminder(this AppointmentEntity appointment)
        => appointment.Reminders.First(r => r.Type == ReminderType.End);
}
