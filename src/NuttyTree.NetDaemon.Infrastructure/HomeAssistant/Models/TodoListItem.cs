namespace NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Models;

public sealed class TodoListItem
{
    public TodoListItem(Guid uid, string summary, string description, ToDoListItemStatus status, DateTime? due = null)
    {
        Uid = uid;
        Summary = summary;
        Description = description;
        Status = status;
        Due = due;
    }

    public Guid Uid { get; set; }

    public string Summary { get; set; }

    public string Description { get; set; }

    public ToDoListItemStatus Status { get; set; }

    public DateTime? Due { get; set; }
}
