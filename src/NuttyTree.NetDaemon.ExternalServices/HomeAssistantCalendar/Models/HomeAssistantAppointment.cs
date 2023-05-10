using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar.Models;

public sealed class HomeAssistantAppointment
{
    private string? id;

    public string Id => GetId();

    public string? Summary { get; set; }

    public string? Description { get; set; }

    public string? Location { get; set; }

    public HomeAssistantAppointmentDateTime? Start { get; set; }

    public HomeAssistantAppointmentDateTime? End { get; set; }

    private string GetId()
    {
        if (id == null)
        {
            var data = SHA256.HashData(Encoding.UTF8.GetBytes($"{Summary}.{Location}.{Start?.DateTime ?? Start?.Date}")).ToList();
            var stringBuilder = new StringBuilder();
            data.ForEach(b => stringBuilder.Append(b.ToString("x2", CultureInfo.InvariantCulture)));
            id = stringBuilder.ToString();
        }

        return id!;
    }
}
