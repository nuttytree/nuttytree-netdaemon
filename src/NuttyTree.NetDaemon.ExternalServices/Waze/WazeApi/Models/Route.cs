namespace NuttyTree.NetDaemon.ExternalServices.Waze.WazeApi.Models;

internal class Route
{
    public List<RouteSegment>? Results { get; set; }

    public List<string>? StreetNames { get; set; }

    public object? TileIds { get; set; }

    public object? TileUpdateTimes { get; set; }

    public object? Geom { get; set; }

    public float FromFraction { get; set; }

    public float ToFraction { get; set; }

    public bool SameFromSegment { get; set; }

    public bool SameToSegment { get; set; }

    public object? AstarPoints { get; set; }

    public object? WayPointIndexes { get; set; }

    public object? WayPointSegmentIndexes { get; set; }

    public object? WayPointFractions { get; set; }

    public int TollMeters { get; set; }

    public int PreferedRouteId { get; set; }

    public bool IsInvalid { get; set; }

    public bool IsBlocked { get; set; }

    public string? ServerUniqueId { get; set; }

    public bool DisplayRoute { get; set; }

    public int AstarVisited { get; set; }

    public string? AstarResult { get; set; }

    public object? AstarData { get; set; }

    public bool IsRestricted { get; set; }

    public string? AvoidStatus { get; set; }

    public object? DueToOverride { get; set; }

    public bool PassesThroughDangerArea { get; set; }

    public int DistanceFromSource { get; set; }

    public int DistanceFromTarget { get; set; }

    public int MinPassengers { get; set; }

    public int HovIndex { get; set; }

    public object? TimeZone { get; set; }

    public string? AlternativeRouteUuid { get; set; }

    public List<string>? RouteType { get; set; }

    public List<object>? RouteAttr { get; set; }

    public int AstarCost { get; set; }

    public object? ReorderChoice { get; set; }

    public int TotalRouteTime { get; set; }

    public List<object>? LaneTypes { get; set; }

    public object? PreferredStoppingPoints { get; set; }

    public List<object>? Areas { get; set; }

    public List<object>? RequiredPermits { get; set; }

    public List<object>? EtaHistograms { get; set; }

    public object? EntryPoint { get; set; }

    public object? ShortRouteName { get; set; }

    public float TollPrice { get; set; }

    public object? Costs { get; set; }

    public object? Penalties { get; set; }

    public bool IsInvalidForPrivateVehicle { get; set; }

    public RouteCostinfo? CostInfo { get; set; }

    public string? RouteName { get; set; }

    public int TotalRouteTimeWithoutMl { get; set; }

    public object? SegGeoms { get; set; }

    public object? RouteNameStreetIds { get; set; }

    public bool Open { get; set; }
}
