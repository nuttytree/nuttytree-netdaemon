using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Models;

public sealed class Appointment
{
    private string? id;

    public Appointment(string summary, string? description, string? location, DateTime start, DateTime end)
    {
        Summary = summary;
        Description = description;
        Location = location;
        Start = start.ToUniversalTime();
        End = end.ToUniversalTime();
    }

    public string Id => id ?? throw new InvalidOperationException("The calendar of this appointment is not set yet");

    public string Calendar { get; private set; } = string.Empty;

    public string Summary { get; }

    public string? Description { get; }

    public string? Location { get; }

    public DateTime Start { get; }

    public DateTime End { get; }

    public bool IsAllDay => Start.Hour == 0 && Start.Minute == 0 && End.Hour == 23 && End.Minute >= 55;

    internal void SetCalendar(string calendar)
    {
        if (id != null)
        {
            throw new InvalidOperationException("The calendar of the appointment can only be set once");
        }

        Calendar = calendar;

        var data = SHA256.HashData(Encoding.UTF8.GetBytes($"{Calendar}.{Summary}.{Location}.{Start:u}")).ToList();
        var stringBuilder = new StringBuilder();
        data.ForEach(b => stringBuilder.Append(b.ToString("x2", CultureInfo.InvariantCulture)));
        id = stringBuilder.ToString();
    }
}
