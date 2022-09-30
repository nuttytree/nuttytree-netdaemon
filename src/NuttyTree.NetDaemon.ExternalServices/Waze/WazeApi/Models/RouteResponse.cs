namespace NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi.Models
{
    internal class RouteResponse
    {
        public Route? Response { get; set; }

        public object? Coords { get; set; }

        public object? SegCoords { get; set; }
    }
}
