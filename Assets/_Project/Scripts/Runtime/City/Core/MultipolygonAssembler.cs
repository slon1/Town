using System;
using System.Collections.Generic;

namespace ZombieCity.City.Core
{
    /// <summary>
    /// Stitches multipolygon outer/inner ways into closed rings. ADR-001 §4.
    /// </summary>
    public static class MultipolygonAssembler
    {
        public sealed class AssembledPolygon
        {
            public List<Vec2d> Outer;
            public List<List<Vec2d>> Holes = new List<List<Vec2d>>();
            public List<long> SourceWayIds = new List<long>();
        }

        /// <summary>
        /// Returns assembled polygons (one per closed outer). Null/empty if nothing usable.
        /// Increments warning counters on doc.Stats for failures.
        /// </summary>
        public static List<AssembledPolygon> Assemble(
            OsmDocument doc,
            OsmRelationData relation,
            LocalEquirectangularProjection projection)
        {
            var result = new List<AssembledPolygon>();
            if (doc == null || relation == null || relation.Members == null)
            {
                return result;
            }

            var outerWays = new List<long>();
            var innerWays = new List<long>();
            bool missingMember = false;

            for (int i = 0; i < relation.Members.Length; i++)
            {
                OsmRelationMember m = relation.Members[i];
                if (m == null || !string.Equals(m.Type, "way", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!doc.Ways.ContainsKey(m.Ref))
                {
                    missingMember = true;
                    continue;
                }

                string role = m.Role ?? string.Empty;
                if (string.Equals(role, "inner", StringComparison.OrdinalIgnoreCase))
                {
                    innerWays.Add(m.Ref);
                }
                else
                {
                    // empty role or outer
                    outerWays.Add(m.Ref);
                }
            }

            if (missingMember)
            {
                doc.Stats.WarningIncompleteMultipolygons++;
            }

            if (outerWays.Count == 0)
            {
                doc.Stats.WarningUnclosedMultipolygons++;
                return result;
            }

            List<List<Vec2d>> outerRings = StitchWaysToRings(doc, projection, outerWays, out int outerFail);
            List<List<Vec2d>> innerRings = StitchWaysToRings(doc, projection, innerWays, out int innerFail);
            if (outerFail > 0 || innerFail > 0)
            {
                doc.Stats.WarningUnclosedMultipolygons += outerFail + innerFail;
            }

            if (outerRings.Count == 0)
            {
                if (outerFail == 0)
                {
                    doc.Stats.WarningUnclosedMultipolygons++;
                }

                return result;
            }

            for (int o = 0; o < outerRings.Count; o++)
            {
                var outer = outerRings[o];
                if (!RingOps.Normalize(outer, wantCcw: true))
                {
                    doc.Stats.WarningUnclosedMultipolygons++;
                    continue;
                }

                var poly = new AssembledPolygon
                {
                    Outer = outer,
                    SourceWayIds = new List<long>(outerWays)
                };

                for (int h = 0; h < innerRings.Count; h++)
                {
                    var hole = new List<Vec2d>(innerRings[h]);
                    if (!RingOps.Normalize(hole, wantCcw: false))
                    {
                        continue;
                    }

                    Vec2d sample = RingOps.InteriorSample(hole);
                    if (RingOps.PointInPolygon(sample, outer))
                    {
                        poly.Holes.Add(hole);
                    }
                }

                result.Add(poly);
            }

            return result;
        }

        static List<List<Vec2d>> StitchWaysToRings(
            OsmDocument doc,
            LocalEquirectangularProjection projection,
            List<long> wayIds,
            out int failedChains)
        {
            failedChains = 0;
            var rings = new List<List<Vec2d>>();
            if (wayIds == null || wayIds.Count == 0)
            {
                return rings;
            }

            var remaining = new List<List<Vec2d>>();
            for (int i = 0; i < wayIds.Count; i++)
            {
                if (!doc.Ways.TryGetValue(wayIds[i], out OsmWayData way))
                {
                    continue;
                }

                Vec2d[] pts = doc.ProjectWayPoints(way, projection);
                if (pts == null || pts.Length < 2)
                {
                    continue;
                }

                remaining.Add(new List<Vec2d>(pts));
            }

            while (remaining.Count > 0)
            {
                var chain = remaining[0];
                remaining.RemoveAt(0);
                bool progressed = true;
                while (progressed && !IsClosed(chain))
                {
                    progressed = false;
                    Vec2d start = chain[0];
                    Vec2d end = chain[chain.Count - 1];
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        var cand = remaining[i];
                        Vec2d c0 = cand[0];
                        Vec2d c1 = cand[cand.Count - 1];
                        if (RingOps.NearlyEqual(end, c0))
                        {
                            Append(chain, cand, skipFirst: true);
                            remaining.RemoveAt(i);
                            progressed = true;
                            break;
                        }

                        if (RingOps.NearlyEqual(end, c1))
                        {
                            cand.Reverse();
                            Append(chain, cand, skipFirst: true);
                            remaining.RemoveAt(i);
                            progressed = true;
                            break;
                        }

                        if (RingOps.NearlyEqual(start, c1))
                        {
                            // prepend cand before chain
                            Append(cand, chain, skipFirst: true);
                            chain = cand;
                            remaining.RemoveAt(i);
                            progressed = true;
                            break;
                        }

                        if (RingOps.NearlyEqual(start, c0))
                        {
                            cand.Reverse();
                            Append(cand, chain, skipFirst: true);
                            chain = cand;
                            remaining.RemoveAt(i);
                            progressed = true;
                            break;
                        }
                    }
                }

                if (IsClosed(chain) || (chain.Count >= 3 && RingOps.NearlyEqual(chain[0], chain[chain.Count - 1])))
                {
                    rings.Add(chain);
                }
                else if (chain.Count >= 3 && TryForceClose(chain))
                {
                    rings.Add(chain);
                }
                else
                {
                    failedChains++;
                }
            }

            return rings;
        }

        static bool TryForceClose(List<Vec2d> chain)
        {
            if (chain.Count < 3)
            {
                return false;
            }

            if (!RingOps.NearlyEqual(chain[0], chain[chain.Count - 1]))
            {
                // Endpoints far — do not force-close incomplete rings
                double dx = chain[0].x - chain[chain.Count - 1].x;
                double dz = chain[0].z - chain[chain.Count - 1].z;
                if (dx * dx + dz * dz > 1.0) // >1 m
                {
                    return false;
                }

                chain.Add(chain[0]);
            }

            return true;
        }

        static bool IsClosed(List<Vec2d> chain)
        {
            return chain != null && chain.Count >= 4 && RingOps.NearlyEqual(chain[0], chain[chain.Count - 1]);
        }

        static void Append(List<Vec2d> dest, List<Vec2d> src, bool skipFirst)
        {
            int start = skipFirst ? 1 : 0;
            for (int i = start; i < src.Count; i++)
            {
                dest.Add(src[i]);
            }
        }
    }
}
