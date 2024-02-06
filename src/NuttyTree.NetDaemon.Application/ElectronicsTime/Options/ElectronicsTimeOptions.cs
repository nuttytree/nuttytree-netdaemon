using NuttyTree.NetDaemon.Application.ElectronicsTime.Models;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.Options;

internal sealed class ElectronicsTimeOptions
{
    public IList<RecurringToDoListItem> ToDoListItems { get; set; } = new List<RecurringToDoListItem>();

    public IList<AllowedApplication> Applications { get; set; } = new List<AllowedApplication>();
}
