namespace NuttyTree.NetDaemon.Waze.Models
{
    public class TravelTime
    {
        public TravelTime(double miles, double minutes)
        {
            Miles = miles;
            Minutes = minutes;
        }

        public double Miles { get; }

        public double Minutes { get; }
    }
}
