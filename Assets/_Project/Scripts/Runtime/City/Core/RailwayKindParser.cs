using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Maps an OSM <c>railway</c> tag to <see cref="RailwayKind"/> (ADR-001 v1.3).
    /// </summary>
    public static class RailwayKindParser
    {
        public static RailwayKind Parse(string railwayTag)
        {
            if (string.IsNullOrWhiteSpace(railwayTag))
            {
                return RailwayKind.Other;
            }

            switch (railwayTag.Trim().ToLowerInvariant())
            {
                case "rail":
                    return RailwayKind.Rail;
                case "light_rail":
                    return RailwayKind.LightRail;
                case "subway":
                    return RailwayKind.Subway;
                case "tram":
                    return RailwayKind.Tram;
                case "narrow_gauge":
                    return RailwayKind.NarrowGauge;
                default:
                    return RailwayKind.Other;
            }
        }
    }
}
