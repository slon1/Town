using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Serializable key/value pair for width tables (Unity cannot serialize dictionaries).
    /// </summary>
    [Serializable]
    public class FloatByKey
    {
        public string key;
        public float value;

        public FloatByKey()
        {
        }

        public FloatByKey(string key, float value)
        {
            this.key = key;
            this.value = value;
        }
    }

    /// <summary>
    /// Pure bake/import settings used by Core (no UnityEngine types).
    /// Defaults match ADR-001. CanonicalBytes is implemented in TASK-001/1B.
    /// </summary>
    [Serializable]
    public class CityBakeConfig
    {
        public float metersPerLane = 3f;
        public float defaultBuildingHeight = 12f;
        public float metersPerLevel = 3f;
        public float roofLevelMeters = 1.5f;
        public bool useRoofLevels = true;
        public float minBuildingHeight = 3f;
        public float waterwayWidth = 4f;

        public float cellSize = 0.5f;
        public float sidewalkWidth = 2f;
        public float sidewalkSearchRadius = 4f;
        public float barrierWidth = 0.5f;
        public float crosswalkWidthFallback = 3f;

        public bool useCropBBox;
        public double cropMinLat;
        public double cropMinLon;
        public double cropMaxLat;
        public double cropMaxLon;

        public FloatByKey[] roadWidths = CreateDefaultRoadWidths();
        public FloatByKey[] walkwayWidths = CreateDefaultWalkwayWidths();

        public string[] highwayRoadTags =
        {
            "motorway", "trunk", "primary", "secondary", "tertiary", "unclassified",
            "residential", "living_street", "service",
            "motorway_link", "trunk_link", "primary_link", "secondary_link", "tertiary_link"
        };

        public string[] highwayWalkwayTags =
        {
            "footway", "path", "pedestrian", "steps", "cycleway", "track"
        };

        public string[] barrierTags =
        {
            "wall", "fence", "hedge", "retaining_wall", "guard_rail", "city_wall"
        };

        public string[] railwayTags =
        {
            "rail", "light_rail", "subway", "tram", "narrow_gauge"
        };

        public string[] landuseTags =
        {
            "grass", "forest", "meadow", "village_green", "recreation_ground"
        };

        public string[] leisureTags =
        {
            "park", "garden", "playground", "pitch"
        };

        public string[] naturalTags =
        {
            "wood", "scrub", "grassland", "water"
        };

        /// <summary>
        /// Stub for 1A. Real FNV-1a canonical bytes come in 1B.
        /// </summary>
        public byte[] CanonicalBytes()
        {
            return Array.Empty<byte>();
        }

        public float GetRoadWidth(string key, float fallback = 6f)
        {
            return FindWidth(roadWidths, key, fallback);
        }

        public float GetWalkwayWidth(string key, float fallback = 2f)
        {
            return FindWidth(walkwayWidths, key, fallback);
        }

        static float FindWidth(FloatByKey[] entries, string key, float fallback)
        {
            if (entries == null || key == null)
            {
                return fallback;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                FloatByKey entry = entries[i];
                if (entry != null && entry.key == key)
                {
                    return entry.value;
                }
            }

            return fallback;
        }

        static FloatByKey[] CreateDefaultRoadWidths()
        {
            return new[]
            {
                new FloatByKey("primary", 10f),
                new FloatByKey("secondary", 9f),
                new FloatByKey("tertiary", 8f),
                new FloatByKey("residential", 6.5f),
                new FloatByKey("living_street", 5f),
                new FloatByKey("service", 4f),
                new FloatByKey("link", 5f),
                new FloatByKey("default", 6f)
            };
        }

        static FloatByKey[] CreateDefaultWalkwayWidths()
        {
            return new[]
            {
                new FloatByKey("footway", 2f),
                new FloatByKey("sidewalk", 2f),
                new FloatByKey("crossing", 3f),
                new FloatByKey("path", 1.5f),
                new FloatByKey("pedestrian", 4f),
                new FloatByKey("steps", 2f),
                new FloatByKey("cycleway", 2f),
                new FloatByKey("track", 3f)
            };
        }
    }
}
