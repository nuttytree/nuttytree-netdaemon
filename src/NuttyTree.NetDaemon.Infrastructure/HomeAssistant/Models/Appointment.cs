using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Models;

public sealed class Appointment
{
    private string? id;

    public string Id => GetId();

    public string Calendar { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Location { get; set; }

    public DateTime Start { get; set; } = DateTime.MinValue;

    public DateTime End { get; set; } = DateTime.MaxValue;

    private string GetId()
    {
        if (id == null)
        {
            var data = SHA256.HashData(Encoding.UTF8.GetBytes($"{Summary}.{Location}.{Start}.{End}")).ToList();
            var stringBuilder = new StringBuilder();
            data.ForEach(b => stringBuilder.Append(b.ToString("x2", CultureInfo.InvariantCulture)));
            id = stringBuilder.ToString();
        }

        return id!;
    }
}
