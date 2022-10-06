namespace NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi.Models;

internal class RouteCostinfo
{
    public int UnbiasedAstarCost { get; set; }

    public int TollAsSeconds { get; set; }

    public bool KeepForReordering { get; set; }
}
