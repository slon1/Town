using System;
using System.Collections.Generic;

namespace ZombieCity.City.Core
{
    [Serializable]
    public class OsmTagList
    {
        public static readonly OsmTagList Empty = new OsmTagList();

        public string[] keys = Array.Empty<string>();
        public string[] values = Array.Empty<string>();

        public int Count => keys != null ? keys.Length : 0;

        public string Get(string key)
        {
            if (keys == null || values == null || key == null)
            {
                return null;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == key)
                {
                    return i < values.Length ? values[i] : null;
                }
            }

            return null;
        }

        public bool Has(string key)
        {
            return Get(key) != null;
        }

        public static OsmTagList FromLists(List<string> keys, List<string> values)
        {
            if (keys == null || keys.Count == 0)
            {
                return Empty;
            }

            return new OsmTagList
            {
                keys = keys.ToArray(),
                values = values != null ? values.ToArray() : Array.Empty<string>()
            };
        }
    }

    public sealed class OsmNodeData
    {
        public long Id;
        public double Lat;
        public double Lon;
        public OsmTagList Tags = new OsmTagList();
    }

    public sealed class OsmWayData
    {
        public long Id;
        public long[] NodeRefs = Array.Empty<long>();
        public OsmTagList Tags = new OsmTagList();
    }

    public sealed class OsmRelationMember
    {
        public string Type;
        public long Ref;
        public string Role;
    }

    public sealed class OsmRelationData
    {
        public long Id;
        public OsmRelationMember[] Members = Array.Empty<OsmRelationMember>();
        public OsmTagList Tags = new OsmTagList();
    }

    public sealed class OsmParseStats
    {
        public int NodeCount;
        public int WayCount;
        public int RelationCount;
        public int MultipolygonCount;
        public int SkippedInvisible;
        public int MissingNodeRefs;
        public int MissingWayMemberRefs;
        public int WarningUnclosedMultipolygons;
        public int WarningIncompleteMultipolygons;
        public bool HasBounds;
        public bool BoundsComputedFromNodes;
        public double MinLat;
        public double MinLon;
        public double MaxLat;
        public double MaxLon;
        public double ElapsedMs;
    }

    /// <summary>
    /// Streaming-parse result (intermediate; not the final CityModel).
    /// </summary>
    public sealed class OsmDocument
    {
        public OsmParseStats Stats = new OsmParseStats();
        public Dictionary<long, OsmNodeData> Nodes = new Dictionary<long, OsmNodeData>();
        public Dictionary<long, OsmWayData> Ways = new Dictionary<long, OsmWayData>();
        public List<OsmRelationData> Multipolygons = new List<OsmRelationData>();

        public LocalEquirectangularProjection CreateProjection()
        {
            return LocalEquirectangularProjection.FromBBox(
                Stats.MinLat, Stats.MinLon, Stats.MaxLat, Stats.MaxLon);
        }

        /// <summary>
        /// Resolves way node refs to projected points; skips missing nodes and counts warnings.
        /// </summary>
        public Vec2d[] ProjectWayPoints(OsmWayData way, LocalEquirectangularProjection projection)
        {
            if (way == null || way.NodeRefs == null || way.NodeRefs.Length == 0)
            {
                return Array.Empty<Vec2d>();
            }

            var list = new List<Vec2d>(way.NodeRefs.Length);
            for (int i = 0; i < way.NodeRefs.Length; i++)
            {
                long refId = way.NodeRefs[i];
                if (!Nodes.TryGetValue(refId, out OsmNodeData node))
                {
                    Stats.MissingNodeRefs++;
                    continue;
                }

                list.Add(projection.Project(node.Lat, node.Lon));
            }

            return list.ToArray();
        }

        /// <summary>
        /// Counts missing way members on multipolygon relations (aggregates into stats).
        /// </summary>
        public void CountMissingWayMembers()
        {
            for (int r = 0; r < Multipolygons.Count; r++)
            {
                OsmRelationData rel = Multipolygons[r];
                if (rel.Members == null)
                {
                    continue;
                }

                for (int m = 0; m < rel.Members.Length; m++)
                {
                    OsmRelationMember member = rel.Members[m];
                    if (member == null)
                    {
                        continue;
                    }

                    if (!string.Equals(member.Type, "way", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!Ways.ContainsKey(member.Ref))
                    {
                        Stats.MissingWayMemberRefs++;
                    }
                }
            }
        }
    }
}
