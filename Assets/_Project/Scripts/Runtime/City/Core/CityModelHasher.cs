using System;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Canonical FNV-1a hashing for <see cref="CityModel"/> (ADR-001 §4).
    /// </summary>
    public static class CityModelHasher
    {
        public static byte[] CanonicalBytes(CityModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var w = new CanonicalWriter();
            WriteMeta(w, model.meta);
            WriteRoads(w, model.roads);
            WriteWalkways(w, model.walkways);
            WriteAreas(w, model.areas);
            WriteBarriers(w, model.barriers);
            WriteRailways(w, model.railways);
            WriteWaterways(w, model.waterways);
            WritePointFeatures(w, model.pointFeatures);
            // Stats are derived; not part of data hash contract (ADR lists Meta + entity kinds).
            return w.ToArray();
        }

        public static ulong ComputeDataHash(CityModel model)
        {
            return Fnv1a64.Compute(CanonicalBytes(model));
        }

        public static void SortEntities(CityModel model)
        {
            if (model == null)
            {
                return;
            }

            SortByOsmId(model.roads, (a, b) => a.osmId.CompareTo(b.osmId));
            SortByOsmId(model.walkways, (a, b) => a.osmId.CompareTo(b.osmId));
            SortByOsmId(model.areas, (a, b) => a.osmId.CompareTo(b.osmId));
            SortByOsmId(model.barriers, (a, b) => a.osmId.CompareTo(b.osmId));
            SortByOsmId(model.railways, (a, b) => a.osmId.CompareTo(b.osmId));
            SortByOsmId(model.waterways, (a, b) => a.osmId.CompareTo(b.osmId));
            SortByOsmId(model.pointFeatures, (a, b) => a.osmId.CompareTo(b.osmId));
        }

        static void SortByOsmId<T>(T[] array, Comparison<T> comparison)
        {
            if (array == null || array.Length < 2)
            {
                return;
            }

            Array.Sort(array, comparison);
        }

        static void WriteMeta(CanonicalWriter w, CityModelMeta meta)
        {
            if (meta == null)
            {
                meta = new CityModelMeta();
            }

            // Without time/paths; dataHash excluded (circular). settingsHash included.
            w.WriteString(meta.sourceName);
            w.WriteLatLon(meta.bboxMinLat);
            w.WriteLatLon(meta.bboxMinLon);
            w.WriteLatLon(meta.bboxMaxLat);
            w.WriteLatLon(meta.bboxMaxLon);
            w.WriteLatLon(meta.lat0);
            w.WriteLatLon(meta.lon0);
            w.WriteMeters(meta.sizeXM);
            w.WriteMeters(meta.sizeZM);
            w.WriteInt64(unchecked((long)meta.settingsHash));
        }

        static void WriteRoads(CanonicalWriter w, Road[] roads)
        {
            if (roads == null)
            {
                w.WriteInt32(0);
                return;
            }

            w.WriteInt32(roads.Length);
            for (int i = 0; i < roads.Length; i++)
            {
                Road r = roads[i] ?? new Road();
                w.WriteInt64(r.osmId);
                w.WriteEnum(r.highwayClass);
                w.WriteMeters(r.widthM);
                w.WriteInt32(r.layer);
                w.WriteBool(r.isBridge);
                w.WriteBool(r.isTunnel);
                w.WriteBool(r.isBuildingPassage);
                w.WriteBool(r.isShared);
                w.WriteEnum(r.sidewalkMode);
                w.WriteVec2dArray(r.points);
            }
        }

        static void WriteWalkways(CanonicalWriter w, Walkway[] walkways)
        {
            if (walkways == null)
            {
                w.WriteInt32(0);
                return;
            }

            w.WriteInt32(walkways.Length);
            for (int i = 0; i < walkways.Length; i++)
            {
                Walkway x = walkways[i] ?? new Walkway();
                w.WriteInt64(x.osmId);
                w.WriteEnum(x.kind);
                w.WriteMeters(x.widthM);
                w.WriteInt32(x.layer);
                w.WriteBool(x.isBridge);
                w.WriteBool(x.isTunnel);
                w.WriteBool(x.isBuildingPassage);
                w.WriteVec2dArray(x.points);
            }
        }

        static void WriteAreas(CanonicalWriter w, Area[] areas)
        {
            if (areas == null)
            {
                w.WriteInt32(0);
                return;
            }

            w.WriteInt32(areas.Length);
            for (int i = 0; i < areas.Length; i++)
            {
                Area a = areas[i] ?? new Area();
                w.WriteInt64(a.osmId);
                w.WriteEnum(a.kind);
                w.WriteMeters(a.heightM);
                w.WriteMeters(a.minHeightM);
                w.WriteEnum(a.heightSource);
                w.WriteString(a.buildingType);
                w.WriteVec2dArray(a.points);
                w.WriteInt32Array(a.ringStart);
            }
        }

        static void WriteBarriers(CanonicalWriter w, Barrier[] barriers)
        {
            if (barriers == null)
            {
                w.WriteInt32(0);
                return;
            }

            w.WriteInt32(barriers.Length);
            for (int i = 0; i < barriers.Length; i++)
            {
                Barrier b = barriers[i] ?? new Barrier();
                w.WriteInt64(b.osmId);
                w.WriteEnum(b.kind);
                w.WriteVec2dArray(b.points);
            }
        }

        static void WriteRailways(CanonicalWriter w, Railway[] railways)
        {
            if (railways == null)
            {
                w.WriteInt32(0);
                return;
            }

            w.WriteInt32(railways.Length);
            for (int i = 0; i < railways.Length; i++)
            {
                Railway r = railways[i] ?? new Railway();
                w.WriteInt64(r.osmId);
                w.WriteEnum(r.kind);
                w.WriteBool(r.isBridge);
                w.WriteBool(r.isTunnel);
                w.WriteInt32(r.layer);
                w.WriteVec2dArray(r.points);
            }
        }

        static void WriteWaterways(CanonicalWriter w, Waterway[] waterways)
        {
            if (waterways == null)
            {
                w.WriteInt32(0);
                return;
            }

            w.WriteInt32(waterways.Length);
            for (int i = 0; i < waterways.Length; i++)
            {
                Waterway x = waterways[i] ?? new Waterway();
                w.WriteInt64(x.osmId);
                w.WriteEnum(x.kind);
                w.WriteBool(x.isTunnel);
                w.WriteVec2dArray(x.points);
            }
        }

        static void WritePointFeatures(CanonicalWriter w, PointFeature[] points)
        {
            if (points == null)
            {
                w.WriteInt32(0);
                return;
            }

            w.WriteInt32(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                PointFeature p = points[i] ?? new PointFeature();
                w.WriteInt64(p.osmId);
                w.WriteEnum(p.kind);
                w.WriteVec2d(p.position);
            }
        }
    }
}
