using System.Text.Json.Serialization;

namespace NuttyTree.NetDaemon.ExternalServices.Nominatim;

internal sealed class NominatimPlace
{
    // Nominatim returns the coordinates as strings
    [JsonPropertyName("lat")]
    public string? Latitude { get; set; }

    [JsonPropertyName("lon")]
    public string? Longitude { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
