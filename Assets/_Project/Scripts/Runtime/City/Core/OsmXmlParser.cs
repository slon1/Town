using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Xml;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Streaming OSM XML 0.6 parser (XmlReader). No XDocument.
    /// </summary>
    public static class OsmXmlParser
    {
        public static OsmDocument ParseFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path is null or empty.", nameof(path));
            }

            using (var stream = File.OpenRead(path))
            {
                return Parse(stream);
            }
        }

        public static OsmDocument Parse(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var sw = Stopwatch.StartNew();
            var doc = new OsmDocument();
            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element)
                    {
                        continue;
                    }

                    switch (reader.Name)
                    {
                        case "osm":
                            ValidateOsmVersion(reader);
                            break;
                        case "bounds":
                            ReadBounds(reader, doc);
                            break;
                        case "node":
                            ReadNode(reader, doc);
                            break;
                        case "way":
                            ReadWay(reader, doc);
                            break;
                        case "relation":
                            ReadRelation(reader, doc);
                            break;
                    }
                }
            }

            if (!doc.Stats.HasBounds)
            {
                ComputeBoundsFromNodes(doc);
            }

            doc.CountMissingWayMembers();
            sw.Stop();
            doc.Stats.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            return doc;
        }

        public static OsmDocument ParseXml(string xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)))
            {
                return Parse(stream);
            }
        }

        static void ValidateOsmVersion(XmlReader reader)
        {
            string version = reader.GetAttribute("version");
            if (!string.IsNullOrEmpty(version) && version != "0.6")
            {
                // Tolerant: continue; classification stages assume 0.6 semantics.
            }
        }

        static void ReadBounds(XmlReader reader, OsmDocument doc)
        {
            doc.Stats.HasBounds = true;
            doc.Stats.MinLat = ParseDouble(reader.GetAttribute("minlat"));
            doc.Stats.MinLon = ParseDouble(reader.GetAttribute("minlon"));
            doc.Stats.MaxLat = ParseDouble(reader.GetAttribute("maxlat"));
            doc.Stats.MaxLon = ParseDouble(reader.GetAttribute("maxlon"));
        }

        static void ReadNode(XmlReader reader, OsmDocument doc)
        {
            if (IsInvisible(reader))
            {
                doc.Stats.SkippedInvisible++;
                if (!reader.IsEmptyElement)
                {
                    reader.Skip();
                }

                return;
            }

            long id = ParseInt64(reader.GetAttribute("id"));
            double lat = ParseDouble(reader.GetAttribute("lat"));
            double lon = ParseDouble(reader.GetAttribute("lon"));
            OsmTagList tags = OsmTagList.Empty;
            if (!reader.IsEmptyElement)
            {
                tags = ReadChildTags(reader);
            }

            var node = new OsmNodeData
            {
                Id = id,
                Lat = lat,
                Lon = lon,
                Tags = tags
            };
            doc.Nodes[id] = node;
            doc.Stats.NodeCount++;
        }

        static void ReadWay(XmlReader reader, OsmDocument doc)
        {
            if (IsInvisible(reader))
            {
                doc.Stats.SkippedInvisible++;
                if (!reader.IsEmptyElement)
                {
                    reader.Skip();
                }

                return;
            }

            long id = ParseInt64(reader.GetAttribute("id"));
            var nodeRefs = new List<long>(32);
            var tagKeys = new List<string>(8);
            var tagValues = new List<string>(8);

            if (!reader.IsEmptyElement)
            {
                int depth = reader.Depth;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                    {
                        break;
                    }

                    if (reader.NodeType != XmlNodeType.Element)
                    {
                        continue;
                    }

                    if (reader.Name == "nd")
                    {
                        nodeRefs.Add(ParseInt64(reader.GetAttribute("ref")));
                    }
                    else if (reader.Name == "tag")
                    {
                        tagKeys.Add(reader.GetAttribute("k") ?? string.Empty);
                        tagValues.Add(reader.GetAttribute("v") ?? string.Empty);
                    }
                }
            }

            var way = new OsmWayData
            {
                Id = id,
                NodeRefs = nodeRefs.ToArray(),
                Tags = OsmTagList.FromLists(tagKeys, tagValues)
            };
            doc.Ways[id] = way;
            doc.Stats.WayCount++;
        }

        static void ReadRelation(XmlReader reader, OsmDocument doc)
        {
            if (IsInvisible(reader))
            {
                doc.Stats.SkippedInvisible++;
                if (!reader.IsEmptyElement)
                {
                    reader.Skip();
                }

                return;
            }

            doc.Stats.RelationCount++;
            long id = ParseInt64(reader.GetAttribute("id"));
            var members = new List<OsmRelationMember>(8);
            var tagKeys = new List<string>(8);
            var tagValues = new List<string>(8);

            if (!reader.IsEmptyElement)
            {
                int depth = reader.Depth;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                    {
                        break;
                    }

                    if (reader.NodeType != XmlNodeType.Element)
                    {
                        continue;
                    }

                    if (reader.Name == "member")
                    {
                        members.Add(new OsmRelationMember
                        {
                            Type = reader.GetAttribute("type") ?? string.Empty,
                            Ref = ParseInt64(reader.GetAttribute("ref")),
                            Role = reader.GetAttribute("role") ?? string.Empty
                        });
                    }
                    else if (reader.Name == "tag")
                    {
                        tagKeys.Add(reader.GetAttribute("k") ?? string.Empty);
                        tagValues.Add(reader.GetAttribute("v") ?? string.Empty);
                    }
                }
            }

            OsmTagList tags = OsmTagList.FromLists(tagKeys, tagValues);
            if (!string.Equals(tags.Get("type"), "multipolygon", StringComparison.Ordinal))
            {
                return;
            }

            doc.Multipolygons.Add(new OsmRelationData
            {
                Id = id,
                Members = members.ToArray(),
                Tags = tags
            });
            doc.Stats.MultipolygonCount++;
        }

        static OsmTagList ReadChildTags(XmlReader reader)
        {
            var tagKeys = new List<string>(4);
            var tagValues = new List<string>(4);
            int depth = reader.Depth;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
                {
                    break;
                }

                if (reader.NodeType == XmlNodeType.Element && reader.Name == "tag")
                {
                    tagKeys.Add(reader.GetAttribute("k") ?? string.Empty);
                    tagValues.Add(reader.GetAttribute("v") ?? string.Empty);
                }
            }

            return OsmTagList.FromLists(tagKeys, tagValues);
        }

        static bool IsInvisible(XmlReader reader)
        {
            string visible = reader.GetAttribute("visible");
            return visible != null && string.Equals(visible, "false", StringComparison.OrdinalIgnoreCase);
        }

        static void ComputeBoundsFromNodes(OsmDocument doc)
        {
            if (doc.Nodes.Count == 0)
            {
                doc.Stats.HasBounds = false;
                return;
            }

            double minLat = double.PositiveInfinity;
            double minLon = double.PositiveInfinity;
            double maxLat = double.NegativeInfinity;
            double maxLon = double.NegativeInfinity;
            foreach (KeyValuePair<long, OsmNodeData> pair in doc.Nodes)
            {
                OsmNodeData n = pair.Value;
                if (n.Lat < minLat) minLat = n.Lat;
                if (n.Lon < minLon) minLon = n.Lon;
                if (n.Lat > maxLat) maxLat = n.Lat;
                if (n.Lon > maxLon) maxLon = n.Lon;
            }

            doc.Stats.MinLat = minLat;
            doc.Stats.MinLon = minLon;
            doc.Stats.MaxLat = maxLat;
            doc.Stats.MaxLon = maxLon;
            doc.Stats.HasBounds = true;
            doc.Stats.BoundsComputedFromNodes = true;
        }

        static double ParseDouble(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        static long ParseInt64(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return 0;
            }

            return long.Parse(s, CultureInfo.InvariantCulture);
        }
    }

}
