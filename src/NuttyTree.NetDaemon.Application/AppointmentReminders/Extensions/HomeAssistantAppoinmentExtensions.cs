using NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar.Models;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;

internal static class HomeAssistantAppoinmentExtensions
{
    public static DateTime GetStartDateTime(this HomeAssistantAppointment appointment)
        => (appointment.Start?.DateTime ?? appointment.Start?.Date ?? DateTime.MinValue).ToUniversalTime();

    public static DateTime? GetEndDateTime(this HomeAssistantAppointment appointment)
        => (appointment.End?.DateTime ?? appointment.End?.Date)?.ToUniversalTime();

    public static bool GetIsAllDay(this HomeAssistantAppointment appointment)
        => appointment.Start?.DateTime == null
            || (appointment.Start?.DateTime?.Hour == 0 && appointment.Start?.DateTime?.Minute == 0 && appointment.End?.DateTime?.Hour == 23 && appointment.End?.DateTime?.Minute >= 55);

    public static AppointmentEntity ToAppointmentEntity(this HomeAssistantAppointment appointment, string calendar)
        => new AppointmentEntity(
            appointment.Id,
            calendar,
            appointment.Summary!,
            appointment.Description,
            appointment.Location,
            appointment.GetStartDateTime(),
            appointment.GetEndDateTime(),
            appointment.GetIsAllDay())
        {
            Reminders = new List<AppointmentReminderEntity>
            {
                new AppointmentReminderEntity($"{appointment.Id}-Start", ReminderType.Start),
                new AppointmentReminderEntity($"{appointment.Id}-End", ReminderType.End),
            }
        };
}
