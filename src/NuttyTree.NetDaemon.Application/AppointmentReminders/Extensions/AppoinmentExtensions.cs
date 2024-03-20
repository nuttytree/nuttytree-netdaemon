using System.Text.RegularExpressions;
using NuttyTree.NetDaemon.Infrastructure.Database.Entities;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Models;

namespace NuttyTree.NetDaemon.Application.AppointmentReminders.Extensions;

internal static partial class AppoinmentExtensions
{
    private static readonly Regex TagWhiteSpaceRegex = GetTagWhiteSpaceRegex();

    private static readonly Regex LineBreakRegex = GetLineBreakRegex();

    private static readonly Regex StripFormattingRegex = GetStripFormattingRegex();

    public static AppointmentEntity ToAppointmentEntity(this Appointment appointment)
        => new (
            appointment.Id,
            appointment.Calendar,
            appointment.Summary!,
            HtmlToPlainText(appointment.Description),
            appointment.Location,
            appointment.Start,
            appointment.End,
            appointment.IsAllDay)
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

    [GeneratedRegex(@"(>|$)(\W|\n|\r)+<", RegexOptions.Multiline)]
    private static partial Regex GetTagWhiteSpaceRegex();

    [GeneratedRegex(@"<(br|BR)\s{0,1}\/{0,1}>", RegexOptions.Multiline)]
    private static partial Regex GetLineBreakRegex();

    [GeneratedRegex(@"<[^>]*(>|$)", RegexOptions.Multiline)]
    private static partial Regex GetStripFormattingRegex();
}
