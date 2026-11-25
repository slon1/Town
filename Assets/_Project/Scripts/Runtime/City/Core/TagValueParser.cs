using System;
using System.Globalization;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Parses OSM measure/tag values per ADR-001 §4.
    /// </summary>
    public static class TagValueParser
    {
        public static bool TryParseMeters(string raw, out float meters)
        {
            meters = 0;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string s = raw.Trim();
            if (s.EndsWith("m", StringComparison.OrdinalIgnoreCase) ||
                s.EndsWith("м", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(0, s.Length - 1).Trim();
            }

            s = s.Replace(',', '.');
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return false;
            }

            meters = (float)value;
            return true;
        }

        public static bool TryParseInt(string raw, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseDouble(string raw, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string s = raw.Trim().Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static int ParseLayer(OsmTagList tags)
        {
            if (tags == null)
            {
                return 0;
            }

            return TryParseInt(tags.Get("layer"), out int layer) ? layer : 0;
        }

        public static bool IsBridge(OsmTagList tags)
        {
            string v = tags?.Get("bridge");
            if (string.IsNullOrEmpty(v) || string.Equals(v, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public static void ReadTunnelFlags(OsmTagList tags, out bool isTunnel, out bool isBuildingPassage)
        {
            isTunnel = false;
            isBuildingPassage = false;
            string v = tags?.Get("tunnel");
            if (string.IsNullOrEmpty(v) || string.Equals(v, "no", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(v, "building_passage", StringComparison.OrdinalIgnoreCase))
            {
                isBuildingPassage = true;
                return;
            }

            // yes, culvert, and other non-no values
            isTunnel = true;
        }

        public static SidewalkMode ParseSidewalkMode(OsmTagList tags)
        {
            string v = tags?.Get("sidewalk");
            if (string.IsNullOrEmpty(v))
            {
                return SidewalkMode.None;
            }

            if (string.Equals(v, "both", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return SidewalkMode.Both;
            }

            if (string.Equals(v, "left", StringComparison.OrdinalIgnoreCase))
            {
                return SidewalkMode.Left;
            }

            if (string.Equals(v, "right", StringComparison.OrdinalIgnoreCase))
            {
                return SidewalkMode.Right;
            }

            if (string.Equals(v, "no", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "none", StringComparison.OrdinalIgnoreCase))
            {
                return SidewalkMode.None;
            }

            return SidewalkMode.Unknown;
        }

        public static float ResolveRoadWidth(OsmTagList tags, HighwayClass cls, CityBakeConfig config)
        {
            if (TryParseMeters(tags?.Get("width"), out float w))
            {
                return w;
            }

            if (TryParseDouble(tags?.Get("lanes"), out double lanes) && lanes > 0)
            {
                return (float)(lanes * config.metersPerLane);
            }

            string key = HighwayClassToWidthKey(cls);
            return config.GetRoadWidth(key, config.GetRoadWidth("default", 6f));
        }

        public static float ResolveWalkwayWidth(OsmTagList tags, WalkwayKind kind, CityBakeConfig config)
        {
            if (TryParseMeters(tags?.Get("width"), out float w))
            {
                return w;
            }

            string key = WalkwayKindToWidthKey(kind);
            return config.GetWalkwayWidth(key, 2f);
        }

        public static float ResolveBuildingHeight(OsmTagList tags, CityBakeConfig config, out bool usedDefault)
        {
            usedDefault = false;
            if (TryParseMeters(tags?.Get("height"), out float h) && h > 0)
            {
                return Math.Max(h, config.minBuildingHeight);
            }

            double levels = 0;
            bool hasLevels = TryParseDouble(tags?.Get("building:levels"), out levels);
            double roofLevels = 0;
            bool hasRoof = config.useRoofLevels && TryParseDouble(tags?.Get("roof:levels"), out roofLevels);
            if (hasLevels || hasRoof)
            {
                float fromLevels = (float)(levels * config.metersPerLevel + roofLevels * config.roofLevelMeters);
                return Math.Max(fromLevels, config.minBuildingHeight);
            }

            usedDefault = true;
            return Math.Max(config.defaultBuildingHeight, config.minBuildingHeight);
        }

        static string HighwayClassToWidthKey(HighwayClass cls)
        {
            switch (cls)
            {
                case HighwayClass.Primary: return "primary";
                case HighwayClass.Secondary: return "secondary";
                case HighwayClass.Tertiary: return "tertiary";
                case HighwayClass.Residential: return "residential";
                case HighwayClass.LivingStreet: return "living_street";
                case HighwayClass.Service: return "service";
                case HighwayClass.Link: return "link";
                default: return "default";
            }
        }

        static string WalkwayKindToWidthKey(WalkwayKind kind)
        {
            switch (kind)
            {
                case WalkwayKind.Footway: return "footway";
                case WalkwayKind.Sidewalk: return "sidewalk";
                case WalkwayKind.Crossing: return "crossing";
                case WalkwayKind.Path: return "path";
                case WalkwayKind.Pedestrian: return "pedestrian";
                case WalkwayKind.Steps: return "steps";
                case WalkwayKind.Cycleway: return "cycleway";
                default: return "footway";
            }
        }
    }
}
