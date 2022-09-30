using System.Text.Json.Serialization;

namespace NuttyTree.NetDaemon.ExternalServices.Waze.Models
{
    public class LocationCoordinates : IEquatable<LocationCoordinates>
    {
        public static LocationCoordinates Empty { get; } = new LocationCoordinates { Latitude = 0, Longitude = 0 };

        [JsonPropertyName("lat")]
        public double Latitude { get; set; }

        [JsonPropertyName("lon")]
        public double Longitude { get; set; }

        public bool Equals(LocationCoordinates? other) => other?.Latitude == Latitude && other?.Longitude == Longitude;

        public override bool Equals(object? obj) => Equals(obj as LocationCoordinates);

        public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);
    }
}
