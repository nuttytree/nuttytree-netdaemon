using System.Text.RegularExpressions;
using NuttyTree.NetDaemon.ExternalServices.HomeAssistantCalendar.Models;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;

internal static class HomeAssistantAppoinmentExtensions
{
    private static readonly Regex TagWhiteSpaceRegex = new (@"(>|$)(\W|\n|\r)+<", RegexOptions.Multiline);

    private static readonly Regex LineBreakRegex = new (@"<(br|BR)\s{0,1}\/{0,1}>", RegexOptions.Multiline);

    private static readonly Regex StripFormattingRegex = new (@"<[^>]*(>|$)", RegexOptions.Multiline);

    public static DateTime GetStartDateTime(this HomeAssistantAppointment appointment)
        => (appointment.Start?.DateTime ?? appointment.Start?.Date ?? DateTime.MinValue).ToUniversalTime();

    public static DateTime? GetEndDateTime(this HomeAssistantAppointment appointment)
        => (appointment.End?.DateTime ?? appointment.End?.Date)?.ToUniversalTime();

    public static bool GetIsAllDay(this HomeAssistantAppointment appointment)
        => appointment.Start?.DateTime == null
            || (appointment.Start?.DateTime?.Hour == 0 && appointment.Start?.DateTime?.Minute == 0 && appointment.End?.DateTime?.Hour == 23 && appointment.End?.DateTime?.Minute >= 55);

    public static AppointmentEntity ToAppointmentEntity(this HomeAssistantAppointment appointment, string calendar)
        => new (
            appointment.Id,
            calendar,
            appointment.Summary!,
            HtmlToPlainText(appointment.Description),
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

    private static string HtmlToPlainText(string? html)
    {
        var text = html ?? string.Empty;
        text = System.Net.WebUtility.HtmlDecode(text);
        text = TagWhiteSpaceRegex.Replace(text, "><");
        text = LineBreakRegex.Replace(text, Environment.NewLine);
        text = StripFormattingRegex.Replace(text, string.Empty);
        return text;
    }
}
