namespace NuttyTree.NetDaemon.ExternalServices.Waze.Models;

public sealed class TravelTime
{
    public TravelTime(double miles, double minutes)
    {
        Miles = miles;
        Minutes = minutes;
    }

    public double Miles { get; }

    public double Minutes { get; }
}
