namespace ZombieCity.City.Core
{
    public enum HighwayClass
    {
        Motorway = 0,
        Trunk = 1,
        Primary = 2,
        Secondary = 3,
        Tertiary = 4,
        Unclassified = 5,
        Residential = 6,
        LivingStreet = 7,
        Service = 8,
        Link = 9,
        Other = 10
    }

    public enum WalkwayKind
    {
        Footway = 0,
        Sidewalk = 1,
        Crossing = 2,
        Path = 3,
        Steps = 4,
        Cycleway = 5,
        Pedestrian = 6
    }

    public enum AreaKind
    {
        Building = 0,
        Water = 1,
        Green = 2,
        PedestrianArea = 3,
        Other = 4
    }

    public enum BarrierKind
    {
        Wall = 0,
        Fence = 1,
        Hedge = 2,
        Other = 3
    }

    public enum RailwayKind
    {
        Rail = 0,
        LightRail = 1,
        Subway = 2,
        Tram = 3,
        NarrowGauge = 4,
        Other = 5
    }

    public enum WaterwayKind
    {
        Canal = 0,
        River = 1,
        Stream = 2,
        Other = 3
    }

    public enum PointKind
    {
        CrossingNode = 0,
        TrafficSignal = 1,
        Gate = 2,
        Entrance = 3
    }

    public enum SidewalkMode
    {
        None = 0,
        Left = 1,
        Right = 2,
        Both = 3,
        Unknown = 4
    }
}
