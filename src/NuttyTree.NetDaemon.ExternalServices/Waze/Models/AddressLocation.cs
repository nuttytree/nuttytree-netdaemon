namespace NuttyTree.NetDaemon.ExternalServices.Waze.Models;

public class AddressLocation
{
    public object? Bounds { get; set; }

    public string? BusinessName { get; set; }

    public string? City { get; set; }

    public string? CountryName { get; set; }

    public LocationCoordinates Location { get; set; } = new LocationCoordinates();

    public string? Name { get; set; }

    public string? Number { get; set; }

    public string? Provider { get; set; }

    public int SegmentId { get; set; }

    public string? State { get; set; }

    public string? StateName { get; set; }

    public string? Street { get; set; }

    public int StreetId { get; set; }
}
