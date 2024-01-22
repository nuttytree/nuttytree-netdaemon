namespace NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Models;

public sealed class TodoListItem
{
    public TodoListItem(Guid uid, string summary, string description, string status, DateTime? due = null)
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

    public string Status { get; set; }

    public DateTime? Due { get; set; }
}
