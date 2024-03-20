using System.Text.Json;
using NuttyTree.NetDaemon.Infrastructure.Extensions;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Models;

namespace NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Extensions;

public static class CalendarEntityExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    public static Task<IList<Appointment>> GetEventsAsync(this CalendarEntity entity, DateTime start, TimeSpan duration)
        => GetEventsAsync(entity, start, start.Add(duration));

    public static async Task<IList<Appointment>> GetEventsAsync(this CalendarEntity entity, DateTime? start = null, DateTime? end = null)
    {
        var response = await entity.HaContext.CallServiceWithResponseAsync(
            "calendar",
            "get_events",
            entity.ToServiceTarget(),
            new { start_date_time = $"{start ?? DateTime.Now:yyyy-MM-ddTH:mm:ssZ}", end_date_time = $"{end ?? DateTime.MaxValue:yyyy-MM-ddTH:mm:ssZ}" });
        var appointments = response == null
            ? new List<Appointment>()
            : JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, List<Appointment>>>>(response.Value, SerializerOptions) !.First().Value["events"];
        appointments.ForEach(a => a.SetCalendar(entity.EntityId));
        return appointments;
    }
}
