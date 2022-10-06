using NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar.Models;
using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar;

public interface IHomeAssistantCalendarApi
{
    [Get("/api/calendars/calendar.{calendar}?start={startDateTime}&end={endDateTime}")]
    Task<List<HomeAssistantAppointment>> GetAppointmentsAsync(
        string calendar,
        [Query(Format = "yyyy-MM-ddTH:mm:ssZ")] DateTime startDateTime,
        [Query(Format = "yyyy-MM-ddTH:mm:ssZ")] DateTime endDateTime,
        CancellationToken cancellationToken = default);
}
