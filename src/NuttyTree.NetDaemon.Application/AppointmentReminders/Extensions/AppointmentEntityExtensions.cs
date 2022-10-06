using NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar.Models;
using NuttyTree.NetDaemon.ExternalServices.Waze.Models;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;
internal static class AppointmentEntityExtensions
{
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

    public static void SetLocationCoordinates(this AppointmentEntity appointment, LocationCoordinates coordinates)
    {
        appointment.Latitude = coordinates.Latitude;
        appointment.Longitude = coordinates.Longitude;
    }

    public static AppointmentReminderEntity GetStartReminder(this AppointmentEntity appointment)
        => appointment.Reminders.First(r => r.Type == ReminderType.Start);

    public static AppointmentReminderEntity GetEndReminder(this AppointmentEntity appointment)
        => appointment.Reminders.First(r => r.Type == ReminderType.End);
}
