using NuttyTree.NetDaemon.Application.AppointmentReminders.HomeAssistant.Models;
using Refit;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.HomeAssistant
{
    internal interface IHomeAssistantCalendarApi
    {
        [Get("/api/calendars/calendar.{calendar}?start={start}&end={end}")]
        Task<List<HomeAssistantAppointment>> GetAppointmentsAsync(
            string calendar,
            [Query(Format = "yyyy-MM-ddTH:mm:ssZ")] DateTime start,
            [Query(Format = "yyyy-MM-ddTH:mm:ssZ")] DateTime end,
            CancellationToken cancellationToken = default);
    }
}
