namespace NuttyTree.NetDaemon.Waze.WazeApi.Models
{
    internal class RoutePath
    {
        public int SegmentId { get; set; }

        public int NodeId { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public bool Direction { get; set; }
    }
}
