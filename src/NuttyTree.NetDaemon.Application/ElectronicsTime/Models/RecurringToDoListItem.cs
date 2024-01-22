namespace NuttyTree.NetDaemon.Application.ElectronicsTime.Models;

internal sealed class RecurringToDoListItem
{
    public string Name { get; set; } = string.Empty;

    public RecurringToDoListItemType RecurringToDoListItemType { get; set; }

    public TimeOnly StartAt { get; set; }

    public DayOfWeek WeeklyDayOfWeek { get; set; }

    public int DaysBetween { get; set; }

    public string? TriggerSensor { get; set; }

    public string? TriggerFromState { get; set; }

    public string? TriggerToState { get; set; }

    public TimeSpan? ExpiresAfter { get; set; }

    public int MinutesEarned { get; set; }

    public DateTime? NextOccurrence { get; set; }
}
