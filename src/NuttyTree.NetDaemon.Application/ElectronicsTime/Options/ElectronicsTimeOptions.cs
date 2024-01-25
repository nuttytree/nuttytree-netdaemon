using NuttyTree.NetDaemon.Application.ElectronicsTime.Models;

namespace NuttyTree.NetDaemon.Application.ElectronicsTime.Options;

internal sealed class ElectronicsTimeOptions
{
    public List<RecurringToDoListItem> ToDoListItems { get; set; } = new List<RecurringToDoListItem>();
}
