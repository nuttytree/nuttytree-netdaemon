using NuttyTree.NetDaemon.Application.ElectronicsTime.Models;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.Options;

internal sealed class ElectronicsTimeOptions
{
    public IList<RecurringToDoListItem> ToDoListItems { get; set; } = [];

    public string AdminPassword { get; set; } = string.Empty;

    public string WebhookId { get; set; } = string.Empty;

    public IList<AllowedApplication> Applications { get; set; } = [];
}
