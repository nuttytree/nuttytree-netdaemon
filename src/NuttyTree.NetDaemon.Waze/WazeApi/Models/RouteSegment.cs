namespace NuttyTree.NetDaemon.Waze.WazeApi.Models
{
    internal class RouteSegment
    {
        public RoutePath? Path { get; set; }

        public int Street { get; set; }

        public List<int>? AltStreets { get; set; }

        public int Distance { get; set; }

        public int Length { get; set; }

        public int CrossTime { get; set; }

        public int CrossTimeWithoutRealTime { get; set; }

        public object? Tiles { get; set; }

        public object? ClientIds { get; set; }

        public bool KnownDirection { get; set; }

        public int Penalty { get; set; }

        public int RoadType { get; set; }

        public bool IsToll { get; set; }

        public bool UseHovLane { get; set; }

        public int Attributes { get; set; }

        public string? Lane { get; set; }

        public object? LaneType { get; set; }

        public object? Areas { get; set; }

        public object? RequiredPermits { get; set; }

        public string? AvoidStatus { get; set; }

        public object? ClientLaneSet { get; set; }

        public object? RoadSign { get; set; }

        public bool IsInvalid { get; set; }

        public bool IsBlocked { get; set; }

        public object? Detour { get; set; }

        public int MergeOffset { get; set; }

        public object? AdditionalInstruction { get; set; }

        public object? NaiveRoute { get; set; }

        public int DetourSavings { get; set; }

        public object? DetourSavingsNoRt { get; set; }

        public object? DetourRoute { get; set; }

        public object? NaiveRouteFullResult { get; set; }

        public object? DetourRouteFullResult { get; set; }

        public object? Instruction { get; set; }
    }
}
