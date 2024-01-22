using System.Text.Json;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant;
using NuttyTree.NetDaemon.Infrastructure.HomeAssistant.Models;

namespace NuttyTree.NetDaemon.Infrastructure.Extensions;

public static class TodoEntityExtensions
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<TodoListItem> AddItemAsync(
        this TodoEntity entity,
        string item,
        object? dueDate = null,
        object? dueDatetime = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var itemIsCreated = new TaskCompletionSource();
        using var stateChange = entity.StateChanges().Subscribe(_ => itemIsCreated.TrySetResult());
        entity.AddItem(item, dueDate, dueDatetime, description);
        await itemIsCreated.Task.WaitAsync(cancellationToken);
        var updatedItems = await entity.GetItemsAsync("needs_action");
        return updatedItems.First(u => u.Summary == item);
    }

    public static async Task<ICollection<TodoListItem>> GetItemsAsync(this TodoEntity entity, string? status = null)
    {
        var response = await entity.HaContext.CallServiceWithResponseAsync(
            "todo",
            "get_items",
            entity.ToServiceTarget(),
            new { status });
        return response == null
            ? Array.Empty<TodoListItem>()
            : JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, ICollection<TodoListItem>>>>(response.Value, SerializerOptions) !.First().Value["items"];
    }
}
