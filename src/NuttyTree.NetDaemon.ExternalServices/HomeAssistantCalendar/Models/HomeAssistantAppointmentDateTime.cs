namespace NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar.Models;

public sealed class HomeAssistantAppointmentDateTime
{
    public DateTime? Date { get; set; }

    public DateTime? DateTime { get; set; }

    public string? TimeZone { get; set; }

    public DateTime? ToUTC()
    {
        if (DateTime != null)
        {
            return DateTime.Value.ToUniversalTime();
        }
        else if (Date != null)
        {
            if (Date.Value.Kind == DateTimeKind.Unspecified)
            {
                return new DateTime(Date.Value.Year, Date.Value.Month, Date.Value.Day, 0, 0, 0, DateTimeKind.Local).ToUniversalTime();
            }
            else
            {
                return Date.Value.ToUniversalTime();
            }
        }
        else
        {
            return null;
        }
    }
}
