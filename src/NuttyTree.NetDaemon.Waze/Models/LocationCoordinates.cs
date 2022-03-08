using System.Text.Json.Serialization;

namespace NuttyTree.NetDaemon.Waze.Models
{
    public class LocationCoordinates
    {
        public static LocationCoordinates Empty { get; } = new LocationCoordinates { Latitude = 0, Longitude = 0 };

        [JsonPropertyName("lat")]
        public double Latitude { get; set; }

        [JsonPropertyName("lon")]
        public double Longitude { get; set; }

        public override bool Equals(object? obj) => obj is LocationCoordinates coordinates && Latitude == coordinates.Latitude && Longitude == coordinates.Longitude;

        public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);
    }
}
