using System;
using System.Collections.Generic;
using System.IO;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// OsmDocument → CityModel classification and geometry (ADR-001 §4).
    /// </summary>
    public static class CityModelBuilder
    {
        public static CityModel Build(OsmDocument doc, CityBakeConfig config, string sourceName = null)
        {
            if (doc == null)
            {
                throw new ArgumentNullException(nameof(doc));
            }

            if (config == null)
            {
                config = new CityBakeConfig();
            }

            var model = new CityModel
            {
                meta = new CityModelMeta(),
                stats = new CityModelStats()
            };

            LocalEquirectangularProjection proj = doc.CreateProjection();
            FillMeta(model.meta, doc, proj, config, sourceName);

            var roads = new List<Road>();
            var walkways = new List<Walkway>();
            var areas = new List<Area>();
            var barriers = new List<Barrier>();
            var railways = new List<Railway>();
            var waterways = new List<Waterway>();
            var points = new List<PointFeature>();
            var waysUsedAsMultipolygonArea = new HashSet<long>();

            // Multipolygon areas first (so way buildings used only as members can be skipped)
            for (int i = 0; i < doc.Multipolygons.Count; i++)
            {
                ProcessMultipolygon(doc, doc.Multipolygons[i], config, proj, areas, waysUsedAsMultipolygonArea, model.stats);
            }

            foreach (KeyValuePair<long, OsmWayData> pair in doc.Ways)
            {
                ProcessWay(doc, pair.Value, config, proj, roads, walkways, areas, barriers, railways, waterways,
                    waysUsedAsMultipolygonArea, model.stats);
            }

            foreach (KeyValuePair<long, OsmNodeData> pair in doc.Nodes)
            {
                ProcessNode(pair.Value, config, proj, points);
            }

            model.roads = roads.ToArray();
            model.walkways = walkways.ToArray();
            model.areas = areas.ToArray();
            model.barriers = barriers.ToArray();
            model.railways = railways.ToArray();
            model.waterways = waterways.ToArray();
            model.pointFeatures = points.ToArray();

            FinalizeStats(model, doc);
            CityModelHasher.SortEntities(model);
            model.meta.settingsHash = config.ComputeSettingsHash();
            model.meta.dataHash = CityModelHasher.ComputeDataHash(model);
            return model;
        }

        static void FillMeta(
            CityModelMeta meta,
            OsmDocument doc,
            LocalEquirectangularProjection proj,
            CityBakeConfig config,
            string sourceName)
        {
            meta.sourceName = string.IsNullOrEmpty(sourceName)
                ? "map.osm"
                : Path.GetFileName(sourceName);
            meta.bboxMinLat = doc.Stats.MinLat;
            meta.bboxMinLon = doc.Stats.MinLon;
            meta.bboxMaxLat = doc.Stats.MaxLat;
            meta.bboxMaxLon = doc.Stats.MaxLon;
            meta.lat0 = proj.Lat0;
            meta.lon0 = proj.Lon0;
            Vec2d min = proj.Project(doc.Stats.MinLat, doc.Stats.MinLon);
            Vec2d max = proj.Project(doc.Stats.MaxLat, doc.Stats.MaxLon);
            meta.sizeXM = Math.Abs(max.x - min.x);
            meta.sizeZM = Math.Abs(max.z - min.z);
            meta.settingsHash = config.ComputeSettingsHash();
        }

        static void ProcessMultipolygon(
            OsmDocument doc,
            OsmRelationData rel,
            CityBakeConfig config,
            LocalEquirectangularProjection proj,
            List<Area> areas,
            HashSet<long> waysUsedAsMultipolygonArea,
            CityModelStats stats)
        {
            OsmTagList tags = MergeAreaTags(rel.Tags, doc, rel);
            if (tags.Has("building:part") && !IsBuilding(tags))
            {
                // part-only on relation without building — ignore
                stats.warningBuildingPartsIgnored++;
                return;
            }

            if (tags.Has("building:part"))
            {
                stats.warningBuildingPartsIgnored++;
                return;
            }

            if (!TryClassifyArea(tags, config, out AreaKind kind, out string buildingType))
            {
                return;
            }

            List<MultipolygonAssembler.AssembledPolygon> polys =
                MultipolygonAssembler.Assemble(doc, rel, proj);
            if (polys.Count == 0)
            {
                return;
            }

            for (int p = 0; p < polys.Count; p++)
            {
                MultipolygonAssembler.AssembledPolygon poly = polys[p];
                Area area = CreateAreaFromRings(rel.Id, kind, buildingType, tags, config, poly, stats);
                areas.Add(area);
                for (int w = 0; w < poly.SourceWayIds.Count; w++)
                {
                    waysUsedAsMultipolygonArea.Add(poly.SourceWayIds[w]);
                }
            }
        }

        static OsmTagList MergeAreaTags(OsmTagList relationTags, OsmDocument doc, OsmRelationData rel)
        {
            // Tags from relation if building present; else from first outer way
            if (relationTags != null && IsBuilding(relationTags))
            {
                return relationTags;
            }

            if (relationTags != null && relationTags.Count > 0 &&
                (relationTags.Has("natural") || relationTags.Has("landuse") || relationTags.Has("leisure") ||
                 relationTags.Has("waterway")))
            {
                return relationTags;
            }

            if (rel.Members != null)
            {
                for (int i = 0; i < rel.Members.Length; i++)
                {
                    OsmRelationMember m = rel.Members[i];
                    if (m == null || !string.Equals(m.Type, "way", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string role = m.Role ?? string.Empty;
                    if (string.Equals(role, "inner", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (doc.Ways.TryGetValue(m.Ref, out OsmWayData way) && way.Tags != null && way.Tags.Count > 0)
                    {
                        // Prefer relation tags overlay: if relation has any tags, merge key preference to relation
                        if (relationTags != null && relationTags.Count > 0 && IsBuilding(relationTags))
                        {
                            return relationTags;
                        }

                        if (relationTags != null && relationTags.Has("building"))
                        {
                            return relationTags;
                        }

                        return way.Tags;
                    }
                }
            }

            return relationTags ?? OsmTagList.Empty;
        }

        static void ProcessWay(
            OsmDocument doc,
            OsmWayData way,
            CityBakeConfig config,
            LocalEquirectangularProjection proj,
            List<Road> roads,
            List<Walkway> walkways,
            List<Area> areas,
            List<Barrier> barriers,
            List<Railway> railways,
            List<Waterway> waterways,
            HashSet<long> waysUsedAsMultipolygonArea,
            CityModelStats stats)
        {
            OsmTagList tags = way.Tags ?? OsmTagList.Empty;

            if (tags.Has("building:part"))
            {
                stats.warningBuildingPartsIgnored++;
                // still may be highway etc. — part alone doesn't create building
            }

            // Area from closed way (building / green / water / pedestrian area)
            bool skipBuildingArea = waysUsedAsMultipolygonArea.Contains(way.Id) && IsBuilding(tags);
            if (!skipBuildingArea && TryClassifyArea(tags, config, out AreaKind areaKind, out string buildingType))
            {
                if (!(tags.Has("building:part") && areaKind == AreaKind.Building))
                {
                    Vec2d[] pts = doc.ProjectWayPoints(way, proj);
                    var ring = new List<Vec2d>(pts);
                    if (RingOps.Normalize(ring, wantCcw: true))
                    {
                        var poly = new MultipolygonAssembler.AssembledPolygon { Outer = ring };
                        areas.Add(CreateAreaFromRings(way.Id, areaKind, buildingType, tags, config, poly, stats));
                    }
                }
            }

            string highway = tags.Get("highway");
            if (!string.IsNullOrEmpty(highway))
            {
                if (string.Equals(highway, "pedestrian", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(tags.Get("area"), "yes", StringComparison.OrdinalIgnoreCase))
                {
                    // already handled as area if closed; skip line
                }
                else if (TryClassifyAsRoad(tags, highway, out HighwayClass roadClass))
                {
                    Vec2d[] pts = doc.ProjectWayPoints(way, proj);
                    if (pts.Length >= 2)
                    {
                        TagValueParser.ReadTunnelFlags(tags, out bool isTunnel, out bool isPassage);
                        float widthM = TagValueParser.ResolveRoadWidth(tags, roadClass, config);
                        // ADR: track + motor_vehicle=yes|designated → Road width 3.0
                        if (string.Equals(highway, "track", StringComparison.OrdinalIgnoreCase) &&
                            !TagValueParser.TryParseMeters(tags.Get("width"), out _))
                        {
                            widthM = 3f;
                        }

                        roads.Add(new Road
                        {
                            osmId = way.Id,
                            highwayClass = roadClass,
                            widthM = widthM,
                            layer = TagValueParser.ParseLayer(tags),
                            isBridge = TagValueParser.IsBridge(tags),
                            isTunnel = isTunnel,
                            isBuildingPassage = isPassage,
                            isShared = roadClass == HighwayClass.LivingStreet,
                            sidewalkMode = TagValueParser.ParseSidewalkMode(tags),
                            points = pts
                        });
                    }
                }
                else if (TryClassifyAsWalkway(tags, highway, out WalkwayKind walkKind))
                {
                    Vec2d[] pts = doc.ProjectWayPoints(way, proj);
                    if (pts.Length >= 2)
                    {
                        TagValueParser.ReadTunnelFlags(tags, out bool isTunnel, out bool isPassage);
                        walkways.Add(new Walkway
                        {
                            osmId = way.Id,
                            kind = walkKind,
                            widthM = TagValueParser.ResolveWalkwayWidth(tags, walkKind, config),
                            layer = TagValueParser.ParseLayer(tags),
                            isBridge = TagValueParser.IsBridge(tags),
                            isTunnel = isTunnel,
                            isBuildingPassage = isPassage,
                            points = pts
                        });
                    }
                }
            }

            string barrier = tags.Get("barrier");
            if (!string.IsNullOrEmpty(barrier) && IsAllowed(config.barrierTags, barrier))
            {
                Vec2d[] pts = doc.ProjectWayPoints(way, proj);
                if (pts.Length >= 2)
                {
                    barriers.Add(new Barrier
                    {
                        osmId = way.Id,
                        kind = ClassifyBarrier(barrier),
                        points = pts
                    });
                }
            }

            string railway = tags.Get("railway");
            if (!string.IsNullOrEmpty(railway) && IsAllowed(config.railwayTags, railway))
            {
                Vec2d[] pts = doc.ProjectWayPoints(way, proj);
                if (pts.Length >= 2)
                {
                    TagValueParser.ReadTunnelFlags(tags, out bool isTunnel, out _);
                    bool subway = string.Equals(railway, "subway", StringComparison.OrdinalIgnoreCase);
                    railways.Add(new Railway
                    {
                        osmId = way.Id,
                        kind = RailwayKindParser.Parse(railway),
                        isBridge = TagValueParser.IsBridge(tags),
                        isTunnel = isTunnel || subway,
                        layer = TagValueParser.ParseLayer(tags),
                        points = pts
                    });
                }
            }

            string waterway = tags.Get("waterway");
            if (!string.IsNullOrEmpty(waterway) &&
                (string.Equals(waterway, "canal", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(waterway, "river", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(waterway, "stream", StringComparison.OrdinalIgnoreCase)))
            {
                Vec2d[] pts = doc.ProjectWayPoints(way, proj);
                if (pts.Length >= 2)
                {
                    TagValueParser.ReadTunnelFlags(tags, out bool isTunnel, out _);
                    waterways.Add(new Waterway
                    {
                        osmId = way.Id,
                        kind = ClassifyWaterway(waterway),
                        isTunnel = isTunnel,
                        points = pts
                    });
                }
            }
        }

        static void ProcessNode(
            OsmNodeData node,
            CityBakeConfig config,
            LocalEquirectangularProjection proj,
            List<PointFeature> points)
        {
            OsmTagList tags = node.Tags ?? OsmTagList.Empty;
            string barrier = tags.Get("barrier");
            if (!string.IsNullOrEmpty(barrier))
            {
                if (string.Equals(barrier, "gate", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(barrier, "lift_gate", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(barrier, "bollard", StringComparison.OrdinalIgnoreCase))
                {
                    points.Add(new PointFeature
                    {
                        osmId = node.Id,
                        kind = PointKind.Gate,
                        position = proj.Project(node.Lat, node.Lon)
                    });
                    return;
                }

                if (string.Equals(barrier, "entrance", StringComparison.OrdinalIgnoreCase))
                {
                    points.Add(new PointFeature
                    {
                        osmId = node.Id,
                        kind = PointKind.Entrance,
                        position = proj.Project(node.Lat, node.Lon)
                    });
                    return;
                }
            }

            if (string.Equals(tags.Get("highway"), "traffic_signals", StringComparison.OrdinalIgnoreCase))
            {
                points.Add(new PointFeature
                {
                    osmId = node.Id,
                    kind = PointKind.TrafficSignal,
                    position = proj.Project(node.Lat, node.Lon)
                });
                return;
            }

            if (string.Equals(tags.Get("highway"), "crossing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tags.Get("footway"), "crossing", StringComparison.OrdinalIgnoreCase))
            {
                points.Add(new PointFeature
                {
                    osmId = node.Id,
                    kind = PointKind.CrossingNode,
                    position = proj.Project(node.Lat, node.Lon)
                });
            }
        }

        static Area CreateAreaFromRings(
            long osmId,
            AreaKind kind,
            string buildingType,
            OsmTagList tags,
            CityBakeConfig config,
            MultipolygonAssembler.AssembledPolygon poly,
            CityModelStats stats)
        {
            var allPoints = new List<Vec2d>();
            var ringStart = new List<int>();
            ringStart.Add(0);
            allPoints.AddRange(StripClosing(poly.Outer));
            for (int h = 0; h < poly.Holes.Count; h++)
            {
                ringStart.Add(allPoints.Count);
                allPoints.AddRange(StripClosing(poly.Holes[h]));
            }

            float height = 0;
            float minHeight = 0;
            HeightSource heightSource = HeightSource.HeightTag;
            if (kind == AreaKind.Building)
            {
                heightSource = TagValueParser.ResolveHeightSource(tags, config);
                height = TagValueParser.ResolveBuildingHeight(tags, config, out bool usedDefault);
                if (usedDefault)
                {
                    stats.warningBuildingDefaultHeight++;
                }

                if (TagValueParser.TryParseMeters(tags.Get("min_height"), out float mh))
                {
                    minHeight = mh;
                }
            }

            return new Area
            {
                osmId = osmId,
                kind = kind,
                heightM = height,
                minHeightM = minHeight,
                heightSource = heightSource,
                buildingType = buildingType ?? string.Empty,
                points = allPoints.ToArray(),
                ringStart = ringStart.ToArray()
            };
        }

        static List<Vec2d> StripClosing(List<Vec2d> ring)
        {
            var copy = new List<Vec2d>(ring);
            if (copy.Count >= 2 && RingOps.NearlyEqual(copy[0], copy[copy.Count - 1]))
            {
                copy.RemoveAt(copy.Count - 1);
            }

            return copy;
        }

        static bool TryClassifyArea(OsmTagList tags, CityBakeConfig config, out AreaKind kind, out string buildingType)
        {
            kind = AreaKind.Other;
            buildingType = null;
            if (tags == null)
            {
                return false;
            }

            if (tags.Has("building:part") && !IsBuilding(tags))
            {
                return false;
            }

            if (IsBuilding(tags))
            {
                kind = AreaKind.Building;
                buildingType = tags.Get("building");
                return true;
            }

            string natural = tags.Get("natural");
            if (string.Equals(natural, "water", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tags.Get("waterway"), "riverbank", StringComparison.OrdinalIgnoreCase))
            {
                kind = AreaKind.Water;
                return true;
            }

            if (IsAllowed(config.naturalTags, natural) &&
                !string.Equals(natural, "water", StringComparison.OrdinalIgnoreCase))
            {
                kind = AreaKind.Green;
                return true;
            }

            if (IsAllowed(config.landuseTags, tags.Get("landuse")) ||
                IsAllowed(config.leisureTags, tags.Get("leisure")))
            {
                kind = AreaKind.Green;
                return true;
            }

            if (string.Equals(tags.Get("highway"), "pedestrian", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tags.Get("area"), "yes", StringComparison.OrdinalIgnoreCase))
            {
                kind = AreaKind.PedestrianArea;
                return true;
            }

            return false;
        }

        static bool IsBuilding(OsmTagList tags)
        {
            string b = tags?.Get("building");
            return !string.IsNullOrEmpty(b) && !string.Equals(b, "no", StringComparison.OrdinalIgnoreCase);
        }

        static bool TryClassifyAsRoad(OsmTagList tags, string highway, out HighwayClass cls)
        {
            cls = HighwayClass.Other;
            if (string.Equals(highway, "track", StringComparison.OrdinalIgnoreCase))
            {
                string mv = tags.Get("motor_vehicle");
                if (string.Equals(mv, "yes", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(mv, "designated", StringComparison.OrdinalIgnoreCase))
                {
                    cls = HighwayClass.Other; // width via default/table track→3 handled as road default
                    // Use service-like; ADR: Road width 3.0 for track+motor
                    cls = HighwayClass.Service;
                    return true;
                }

                return false;
            }

            if (highway.EndsWith("_link", StringComparison.OrdinalIgnoreCase))
            {
                cls = HighwayClass.Link;
                return true;
            }

            switch (highway)
            {
                case "motorway": cls = HighwayClass.Motorway; return true;
                case "trunk": cls = HighwayClass.Trunk; return true;
                case "primary": cls = HighwayClass.Primary; return true;
                case "secondary": cls = HighwayClass.Secondary; return true;
                case "tertiary": cls = HighwayClass.Tertiary; return true;
                case "unclassified": cls = HighwayClass.Unclassified; return true;
                case "residential": cls = HighwayClass.Residential; return true;
                case "living_street": cls = HighwayClass.LivingStreet; return true;
                case "service": cls = HighwayClass.Service; return true;
                default: return false;
            }
        }

        static bool TryClassifyAsWalkway(OsmTagList tags, string highway, out WalkwayKind kind)
        {
            kind = WalkwayKind.Footway;
            if (string.Equals(highway, "footway", StringComparison.OrdinalIgnoreCase))
            {
                string fw = tags.Get("footway");
                if (string.Equals(fw, "crossing", StringComparison.OrdinalIgnoreCase))
                {
                    kind = WalkwayKind.Crossing;
                }
                else if (string.Equals(fw, "sidewalk", StringComparison.OrdinalIgnoreCase))
                {
                    kind = WalkwayKind.Sidewalk;
                }
                else
                {
                    kind = WalkwayKind.Footway;
                }

                return true;
            }

            if (string.Equals(highway, "path", StringComparison.OrdinalIgnoreCase))
            {
                kind = WalkwayKind.Path;
                return true;
            }

            if (string.Equals(highway, "pedestrian", StringComparison.OrdinalIgnoreCase))
            {
                kind = WalkwayKind.Pedestrian;
                return true;
            }

            if (string.Equals(highway, "steps", StringComparison.OrdinalIgnoreCase))
            {
                kind = WalkwayKind.Steps;
                return true;
            }

            if (string.Equals(highway, "cycleway", StringComparison.OrdinalIgnoreCase))
            {
                kind = WalkwayKind.Cycleway;
                return true;
            }

            if (string.Equals(highway, "track", StringComparison.OrdinalIgnoreCase))
            {
                // walkway track (not motor)
                kind = WalkwayKind.Path;
                return true;
            }

            return false;
        }

        static BarrierKind ClassifyBarrier(string v)
        {
            if (string.Equals(v, "wall", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "city_wall", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "retaining_wall", StringComparison.OrdinalIgnoreCase))
            {
                return BarrierKind.Wall;
            }

            if (string.Equals(v, "fence", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v, "guard_rail", StringComparison.OrdinalIgnoreCase))
            {
                return BarrierKind.Fence;
            }

            if (string.Equals(v, "hedge", StringComparison.OrdinalIgnoreCase))
            {
                return BarrierKind.Hedge;
            }

            return BarrierKind.Other;
        }

        static WaterwayKind ClassifyWaterway(string v)
        {
            if (string.Equals(v, "canal", StringComparison.OrdinalIgnoreCase)) return WaterwayKind.Canal;
            if (string.Equals(v, "river", StringComparison.OrdinalIgnoreCase)) return WaterwayKind.River;
            if (string.Equals(v, "stream", StringComparison.OrdinalIgnoreCase)) return WaterwayKind.Stream;
            return WaterwayKind.Other;
        }

        static bool IsAllowed(string[] list, string value)
        {
            if (list == null || string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < list.Length; i++)
            {
                if (string.Equals(list[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        static void FinalizeStats(CityModel model, OsmDocument doc)
        {
            CityModelStats s = model.stats;
            s.roadCount = model.roads.Length;
            s.walkwayCount = model.walkways.Length;
            s.areaCount = model.areas.Length;
            s.barrierCount = model.barriers.Length;
            s.railwayCount = model.railways.Length;
            s.waterwayCount = model.waterways.Length;
            s.pointFeatureCount = model.pointFeatures.Length;
            s.warningMissingRefs = doc.Stats.MissingNodeRefs + doc.Stats.MissingWayMemberRefs;
            s.warningUnclosedMultipolygons = doc.Stats.WarningUnclosedMultipolygons;
            s.warningIncompleteMultipolygons = doc.Stats.WarningIncompleteMultipolygons;

            double roadLen = 0;
            int bridges = 0;
            int tunnels = 0;
            for (int i = 0; i < model.roads.Length; i++)
            {
                roadLen += RingOps.PolylineLength(model.roads[i].points);
                if (model.roads[i].isBridge) bridges++;
                if (model.roads[i].isTunnel) tunnels++;
            }

            double walkLen = 0;
            int crossings = 0;
            for (int i = 0; i < model.walkways.Length; i++)
            {
                walkLen += RingOps.PolylineLength(model.walkways[i].points);
                if (model.walkways[i].kind == WalkwayKind.Crossing) crossings++;
                if (model.walkways[i].isBridge) bridges++;
                if (model.walkways[i].isTunnel) tunnels++;
            }

            for (int i = 0; i < model.railways.Length; i++)
            {
                if (model.railways[i].isBridge) bridges++;
                if (model.railways[i].isTunnel) tunnels++;
            }

            for (int i = 0; i < model.waterways.Length; i++)
            {
                if (model.waterways[i].isTunnel) tunnels++;
            }

            int buildings = 0;
            int withHoles = 0;
            double heightSum = 0;
            float heightMax = 0;
            for (int i = 0; i < model.areas.Length; i++)
            {
                Area a = model.areas[i];
                if (a.kind != AreaKind.Building)
                {
                    continue;
                }

                buildings++;
                if (a.ringStart != null && a.ringStart.Length > 1)
                {
                    withHoles++;
                }

                heightSum += a.heightM;
                if (a.heightM > heightMax)
                {
                    heightMax = a.heightM;
                }
            }

            s.roadLengthM = roadLen;
            s.walkwayLengthM = walkLen;
            s.buildingCount = buildings;
            s.buildingWithHolesCount = withHoles;
            s.bridgeCount = bridges;
            s.tunnelCount = tunnels;
            s.crossingCount = crossings;
            s.buildingHeightAvgM = buildings > 0 ? (float)(heightSum / buildings) : 0;
            s.buildingHeightMaxM = heightMax;
        }
    }
}
