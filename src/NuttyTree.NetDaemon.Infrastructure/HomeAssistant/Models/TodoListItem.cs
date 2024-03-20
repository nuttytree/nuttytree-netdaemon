using System.Text.Json.Serialization;

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

    public Guid Uid { get; }

    public string Summary { get; }

    public string Description { get; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ToDoListItemStatus Status { get; }

    public DateTime? Due { get; }
}
