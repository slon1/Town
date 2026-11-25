using System;

namespace ZombieCity.City.Core
{
    [Serializable]
    public class CityModelMeta
    {
        public string sourceName;
        public double bboxMinLat;
        public double bboxMinLon;
        public double bboxMaxLat;
        public double bboxMaxLon;
        public double lat0;
        public double lon0;
        public double sizeXM;
        public double sizeZM;
        public ulong settingsHash;
        public ulong dataHash;
    }

    [Serializable]
    public class Road
    {
        public long osmId;
        public HighwayClass highwayClass;
        public float widthM;
        public int layer;
        public bool isBridge;
        public bool isTunnel;
        public bool isBuildingPassage;
        public bool isShared;
        public SidewalkMode sidewalkMode;
        public Vec2d[] points;
    }

    [Serializable]
    public class Walkway
    {
        public long osmId;
        public WalkwayKind kind;
        public float widthM;
        public int layer;
        public bool isBridge;
        public bool isTunnel;
        public bool isBuildingPassage;
        public Vec2d[] points;
    }

    /// <summary>
    /// Polygon with holes: rings are concatenated in <see cref="points"/>;
    /// <see cref="ringStart"/>[i] is the start index of ring i (0 = outer CCW, rest = holes CW).
    /// Ring i ends at ringStart[i+1] or points.Length.
    /// </summary>
    [Serializable]
    public class Area
    {
        public long osmId;
        public AreaKind kind;
        public float heightM;
        public float minHeightM;
        public string buildingType;
        public Vec2d[] points;
        public int[] ringStart;
    }

    [Serializable]
    public class Barrier
    {
        public long osmId;
        public BarrierKind kind;
        public Vec2d[] points;
    }

    [Serializable]
    public class Railway
    {
        public long osmId;
        public RailwayKind kind;
        public bool isBridge;
        public bool isTunnel;
        public int layer;
        public Vec2d[] points;
    }

    [Serializable]
    public class Waterway
    {
        public long osmId;
        public WaterwayKind kind;
        public bool isTunnel;
        public Vec2d[] points;
    }

    [Serializable]
    public class PointFeature
    {
        public long osmId;
        public PointKind kind;
        public Vec2d position;
    }

    [Serializable]
    public class CityModelStats
    {
        public int roadCount;
        public int walkwayCount;
        public int areaCount;
        public int barrierCount;
        public int railwayCount;
        public int waterwayCount;
        public int pointFeatureCount;
        public int buildingCount;
        public int buildingWithHolesCount;
        public int bridgeCount;
        public int tunnelCount;
        public int crossingCount;
        public double roadLengthM;
        public double walkwayLengthM;
        public float buildingHeightAvgM;
        public float buildingHeightMaxM;
        public int warningMissingRefs;
        public int warningUnclosedMultipolygons;
        public int warningBuildingDefaultHeight;
        public int warningBuildingPartsIgnored;
        public int warningIncompleteMultipolygons;
    }

    /// <summary>
    /// Flat serializable city vector model (ADR-001 §4).
    /// </summary>
    [Serializable]
    public class CityModel
    {
        public CityModelMeta meta = new CityModelMeta();
        public Road[] roads = Array.Empty<Road>();
        public Walkway[] walkways = Array.Empty<Walkway>();
        public Area[] areas = Array.Empty<Area>();
        public Barrier[] barriers = Array.Empty<Barrier>();
        public Railway[] railways = Array.Empty<Railway>();
        public Waterway[] waterways = Array.Empty<Waterway>();
        public PointFeature[] pointFeatures = Array.Empty<PointFeature>();
        public CityModelStats stats = new CityModelStats();
    }
}
