using System.Text.Json.Serialization;

namespace NuttyTree.NetDaemon.ExternalServices.Frigate.Models;

public sealed class CreateEventResponse
{
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    public string? Message { get; set; }

    public bool Success { get; set; }
}
