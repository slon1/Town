using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// ADR-002 §4 / §8 contracts for CityRasterizer (2C). Written from ADR, not from impl.
    /// </summary>
    public class CityRasterizerTests
    {
        const float Cell = 0.5f;

        static CityBakeConfig Cfg() => new CityBakeConfig { cellSize = Cell };

        static CityModel EmptyModel(double size = 40)
        {
            return new CityModel
            {
                meta = new CityModelMeta
                {
                    lat0 = 0,
                    lon0 = 0,
                    // Projected bbox ≈ ±size/2 if we set metric via fake lat/lon using identity-ish:
                    // Use FromBBox path by setting lat0/lon0 and bbox that FromModel projects.
                    // Simpler: tests build models with points already in meters; FromModel uses lat/lon.
                    // For synthetic tests we set meta so FromModel yields origin≈0 covering points.
                    bboxMinLat = -size / 2 / LocalEquirectangularProjection.EarthRadiusM * (180.0 / Math.PI),
                    bboxMaxLat = size / 2 / LocalEquirectangularProjection.EarthRadiusM * (180.0 / Math.PI),
                    bboxMinLon = -size / 2 / LocalEquirectangularProjection.EarthRadiusM * (180.0 / Math.PI),
                    bboxMaxLon = size / 2 / LocalEquirectangularProjection.EarthRadiusM * (180.0 / Math.PI),
                    sizeXM = size,
                    sizeZM = size
                }
            };
        }

        static Vec2d[] Rect(double x0, double z0, double x1, double z1) => new[]
        {
            new Vec2d(x0, z0), new Vec2d(x1, z0), new Vec2d(x1, z1), new Vec2d(x0, z1), new Vec2d(x0, z0)
        };

        static Area Building(long id, Vec2d[] outer, Vec2d[] hole = null, float h = 12f)
        {
            if (hole == null)
            {
                return new Area
                {
                    osmId = id,
                    kind = AreaKind.Building,
                    heightM = h,
                    buildingType = "yes",
                    points = outer,
                    ringStart = new[] { 0 }
                };
            }

            var pts = new Vec2d[outer.Length + hole.Length];
            Array.Copy(outer, 0, pts, 0, outer.Length);
            Array.Copy(hole, 0, pts, outer.Length, hole.Length);
            return new Area
            {
                osmId = id,
                kind = AreaKind.Building,
                heightM = h,
                buildingType = "yes",
                points = pts,
                ringStart = new[] { 0, outer.Length }
            };
        }

        static ushort FlagAt(RasterBakeResult r, double x, double z)
        {
            r.grid.WorldToCell(new Vec2d(x, z), out int i, out int j);
            Assert.IsTrue(r.grid.InBounds(i, j), $"out of bounds ({x},{z}) -> ({i},{j})");
            return r.flags[r.grid.Index(i, j)];
        }

        static ushort BidAt(RasterBakeResult r, double x, double z)
        {
            r.grid.WorldToCell(new Vec2d(x, z), out int i, out int j);
            return r.buildingId[r.grid.Index(i, j)];
        }

        [Test]
        public void Building_ClearsRoadSidewalkGreen()
        {
            var model = EmptyModel();
            model.areas = new[]
            {
                new Area
                {
                    osmId = 1,
                    kind = AreaKind.Green,
                    points = Rect(-8, -8, 8, 8),
                    ringStart = new[] { 0 }
                },
                Building(10, Rect(-4, -4, 4, 4))
            };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 2,
                    widthM = 4f,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };
            model.walkways = new[]
            {
                new Walkway
                {
                    osmId = 3,
                    kind = WalkwayKind.Footway,
                    widthM = 2f,
                    points = new[] { new Vec2d(0, -10), new Vec2d(0, 10) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Building);
            Assert.AreEqual(0, f & (ushort)CellFlags.Road, "building clears Road");
            Assert.AreEqual(0, f & (ushort)CellFlags.Sidewalk, "building clears Sidewalk");
            Assert.AreEqual(0, f & (ushort)CellFlags.Green, "building clears Green");
        }

        [Test]
        public void BuildingPassage_ClearsBuildingAndZeroesBuildingId()
        {
            var model = EmptyModel();
            model.areas = new[] { Building(10, Rect(-5, -5, 5, 5)) };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 20,
                    widthM = 3f,
                    isBuildingPassage = true,
                    points = new[] { new Vec2d(-8, 0), new Vec2d(8, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreEqual(0, f & (ushort)CellFlags.Building);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.BuildingPassage);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Road);
            Assert.AreEqual(0, BidAt(r, 0, 0));
        }

        [Test]
        public void Bridge_ClearsWaterAndBuilding_SetsBridge()
        {
            var model = EmptyModel();
            model.areas = new[]
            {
                new Area
                {
                    osmId = 1,
                    kind = AreaKind.Water,
                    points = Rect(-6, -6, 6, 6),
                    ringStart = new[] { 0 }
                },
                Building(10, Rect(-3, -3, 3, 3))
            };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 30,
                    widthM = 4f,
                    isBridge = true,
                    layer = 1,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Bridge);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Road);
            Assert.AreEqual(0, f & (ushort)CellFlags.Water);
            Assert.AreEqual(0, f & (ushort)CellFlags.Building);
            Assert.AreEqual(0, BidAt(r, 0, 0));
            Assert.Greater(r.stats.cellsUnderBridges, 0);
        }

        [Test]
        public void Tunnel_And_CrossingNode_NotRasterized()
        {
            var model = EmptyModel();
            model.roads = new[]
            {
                new Road
                {
                    osmId = 1,
                    widthM = 4f,
                    isTunnel = true,
                    points = new[] { new Vec2d(-10, 5), new Vec2d(10, 5) }
                }
            };
            model.waterways = new[]
            {
                new Waterway
                {
                    osmId = 2,
                    kind = WaterwayKind.Stream,
                    isTunnel = true,
                    points = new[] { new Vec2d(5, -10), new Vec2d(5, 10) }
                }
            };
            model.pointFeatures = new[]
            {
                new PointFeature { osmId = 3, kind = PointKind.CrossingNode, position = new Vec2d(0, 0) }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, FlagAt(r, 0, 5) & (ushort)CellFlags.Road);
            Assert.AreEqual(0, FlagAt(r, 5, 0) & (ushort)CellFlags.Water);
            // CrossingNode must not set Crosswalk
            Assert.AreEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.Crosswalk);
        }

        [Test]
        public void Crosswalk_KeepsRoadBit()
        {
            var model = EmptyModel();
            model.roads = new[]
            {
                new Road
                {
                    osmId = 1,
                    widthM = 6f,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };
            model.walkways = new[]
            {
                new Walkway
                {
                    osmId = 2,
                    kind = WalkwayKind.Crossing,
                    widthM = 3f,
                    points = new[] { new Vec2d(0, -4), new Vec2d(0, 4) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Road);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Crosswalk);
            Assert.Greater(r.stats.cellsRoadAndCrosswalk, 0);
        }

        [Test]
        public void Gate_ClearsBarrier()
        {
            var model = EmptyModel();
            model.barriers = new[]
            {
                new Barrier
                {
                    osmId = 1,
                    kind = BarrierKind.Fence,
                    points = new[] { new Vec2d(-8, 0), new Vec2d(8, 0) }
                }
            };
            model.pointFeatures = new[]
            {
                new PointFeature { osmId = 2, kind = PointKind.Gate, position = new Vec2d(0, 0) }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreEqual(0, f & (ushort)CellFlags.Barrier);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Gate);
        }

        [Test]
        public void Linear_ClearsWater_IncrementsWaterClearedByLinear()
        {
            var model = EmptyModel();
            model.areas = new[]
            {
                new Area
                {
                    osmId = 1,
                    kind = AreaKind.Water,
                    points = Rect(-8, -8, 8, 8),
                    ringStart = new[] { 0 }
                }
            };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 2,
                    widthM = 4f,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            ushort f = FlagAt(r, 0, 0);
            Assert.AreNotEqual(0, f & (ushort)CellFlags.Road);
            Assert.AreEqual(0, f & (ushort)CellFlags.Water, "v1.1: road clears Water");
            Assert.Greater(r.stats.waterClearedByLinear, 0);
        }

        [Test]
        public void WalkwayAndRailway_AlsoClearWater()
        {
            var model = EmptyModel();
            model.waterways = new[]
            {
                new Waterway
                {
                    osmId = 1,
                    kind = WaterwayKind.Stream,
                    points = new[] { new Vec2d(0, -10), new Vec2d(0, 10) }
                }
            };
            model.walkways = new[]
            {
                new Walkway
                {
                    osmId = 2,
                    kind = WalkwayKind.Footway,
                    widthM = 2f,
                    points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) }
                }
            };
            model.railways = new[]
            {
                new Railway
                {
                    osmId = 3,
                    points = new[] { new Vec2d(-10, 3), new Vec2d(10, 3) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.Water);
            Assert.AreEqual(0, FlagAt(r, 0, 3) & (ushort)CellFlags.Water);
            Assert.Greater(r.stats.waterClearedByLinear, 0);
        }

        [Test]
        public void ThinBuilding_ZeroCells_StillInTable()
        {
            var model = EmptyModel();
            // Sliver thinner than cell, away from centers
            model.areas = new[]
            {
                Building(99, new[]
                {
                    new Vec2d(1.01, 1.01),
                    new Vec2d(1.02, 1.01),
                    new Vec2d(1.02, 1.02),
                    new Vec2d(1.01, 1.02),
                    new Vec2d(1.01, 1.01)
                })
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(1, r.buildings.Length);
            Assert.AreEqual(99, r.buildings[0].osmId);
            Assert.AreEqual(0, r.buildings[0].cellCount);
            Assert.AreEqual(1, r.stats.buildingsWithZeroCells);
        }

        [Test]
        public void OverlappingBuildings_HigherIdWins_CountsConflict()
        {
            var model = EmptyModel();
            model.areas = new[]
            {
                Building(1, Rect(-4, -4, 4, 4)),
                Building(2, Rect(-2, -2, 2, 2))
            };

            var r = CityRasterizer.Bake(model, Cfg());
            // ids assigned by ascending osmId: osm1→id1, osm2→id2; later id wins
            Assert.AreEqual(2, BidAt(r, 0, 0));
            Assert.Greater(r.stats.buildingConflicts, 0);
        }

        [Test]
        public void ObjectOutsideGrid_Counted()
        {
            var model = EmptyModel(20);
            model.roads = new[]
            {
                new Road
                {
                    osmId = 1,
                    widthM = 2f,
                    points = new[] { new Vec2d(100, 100), new Vec2d(110, 100) }
                }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.GreaterOrEqual(r.stats.objectsOutsideGrid, 1);
        }

        [Test]
        public void Determinism_SameHash_AfterEntityReorder()
        {
            var model = EmptyModel();
            model.areas = new[]
            {
                new Area { osmId = 5, kind = AreaKind.Green, points = Rect(-6, -6, 6, 6), ringStart = new[] { 0 } },
                Building(10, Rect(-3, -3, 3, 3))
            };
            model.roads = new[]
            {
                new Road { osmId = 1, widthM = 3f, points = new[] { new Vec2d(-8, 0), new Vec2d(8, 0) } },
                new Road { osmId = 2, widthM = 3f, points = new[] { new Vec2d(0, -8), new Vec2d(0, 8) } }
            };

            var a = CityRasterizer.Bake(model, Cfg());
            var shuffled = EmptyModel();
            shuffled.meta = model.meta;
            shuffled.areas = new[] { model.areas[1], model.areas[0] };
            shuffled.roads = new[] { model.roads[1], model.roads[0] };
            var b = CityRasterizer.Bake(shuffled, Cfg());

            Assert.AreEqual(a.dataHash, b.dataHash);
            Assert.AreEqual(a.dataHash, CityRasterizer.Bake(model, Cfg()).dataHash);
        }

        [Test]
        public void MiniBlock_GoldenBitMap()
        {
            // Synthetic quarter: green, water strip, road, sidewalk, building+courtyard,
            // crossing, barrier+gate, bridge over water.
            var model = EmptyModel(50);
            model.areas = new[]
            {
                new Area
                {
                    osmId = 1,
                    kind = AreaKind.Green,
                    points = Rect(-20, -20, 20, 20),
                    ringStart = new[] { 0 }
                },
                new Area
                {
                    osmId = 2,
                    kind = AreaKind.Water,
                    points = Rect(-20, 8, 20, 12),
                    ringStart = new[] { 0 }
                },
                Building(100, Rect(-6, -6, 6, 6), Rect(-2, -2, 2, 2))
            };
            model.roads = new[]
            {
                new Road
                {
                    osmId = 10,
                    widthM = 4f,
                    points = new[] { new Vec2d(-18, 0), new Vec2d(18, 0) }
                },
                new Road
                {
                    osmId = 11,
                    widthM = 4f,
                    isBridge = true,
                    layer = 1,
                    points = new[] { new Vec2d(-18, 10), new Vec2d(18, 10) }
                }
            };
            model.walkways = new[]
            {
                new Walkway
                {
                    osmId = 20,
                    kind = WalkwayKind.Sidewalk,
                    widthM = 2f,
                    points = new[] { new Vec2d(-18, 3), new Vec2d(18, 3) }
                },
                new Walkway
                {
                    osmId = 21,
                    kind = WalkwayKind.Crossing,
                    widthM = 3f,
                    points = new[] { new Vec2d(0, -3), new Vec2d(0, 3) }
                }
            };
            model.barriers = new[]
            {
                new Barrier
                {
                    osmId = 30,
                    kind = BarrierKind.Wall,
                    points = new[] { new Vec2d(10, -8), new Vec2d(10, 8) }
                }
            };
            model.pointFeatures = new[]
            {
                new PointFeature { osmId = 31, kind = PointKind.Gate, position = new Vec2d(10, 0) }
            };

            var r = CityRasterizer.Bake(model, Cfg());

            // Courtyard center empty of building
            Assert.AreEqual(0, FlagAt(r, 0, 0) & (ushort)CellFlags.Building);
            Assert.AreEqual(0, BidAt(r, 0, 0));
            // Building ring body
            Assert.AreNotEqual(0, FlagAt(r, 4, 0) & (ushort)CellFlags.Building);
            // Road∩crosswalk near origin on road axis outside building? building covers (-6..6);
            // check road east of building
            ushort roadEast = FlagAt(r, 12, 0);
            Assert.AreNotEqual(0, roadEast & (ushort)CellFlags.Road);
            // Bridge over water
            ushort br = FlagAt(r, 0, 10);
            Assert.AreNotEqual(0, br & (ushort)CellFlags.Bridge);
            Assert.AreEqual(0, br & (ushort)CellFlags.Water);
            // Gate on barrier
            Assert.AreNotEqual(0, FlagAt(r, 10, 0) & (ushort)CellFlags.Gate);
            Assert.AreEqual(0, FlagAt(r, 10, 0) & (ushort)CellFlags.Barrier);
            // Sidewalk north of road
            Assert.AreNotEqual(0, FlagAt(r, 12, 3) & (ushort)CellFlags.Sidewalk);

            Assert.Greater(r.stats.cellsByBit[(int)Math.Log((int)CellFlags.Building, 2)], 0);
            Assert.Greater(r.stats.cellsUnderBridges, 0);
        }

        [Test]
        public void Courtyard_Building3583425_HoleEmpty_RingFilled()
        {
            string mapPath = FindMapOsm();
            if (mapPath == null)
            {
                Assert.Ignore("map.osm not found");
            }

            OsmDocument doc;
            using (var fs = File.OpenRead(mapPath))
            {
                doc = OsmXmlParser.Parse(fs);
            }

            var model = CityModelBuilder.Build(doc, Cfg(), "map.osm");
            var r = CityRasterizer.Bake(model, Cfg());

            BuildingRasterInfo info = null;
            for (int i = 0; i < r.buildings.Length; i++)
            {
                if (r.buildings[i].osmId == 3583425L)
                {
                    info = r.buildings[i];
                    break;
                }
            }

            Assert.IsNotNull(info, "building osmId 3583425 must be in table");
            Assert.Greater(info.cellCount, 0);

            // Courtyard approx center from REPORT-001: (-35.8, 11.2)
            Assert.AreEqual(0, FlagAt(r, -35.8, 11.2) & (ushort)CellFlags.Building,
                "courtyard cells must not have Building");
            Assert.AreEqual(0, BidAt(r, -35.8, 11.2), "courtyard buildingId must be 0");

            // Ring body sample near aabb west edge of building (~-50..-17 x, -11..33 z)
            Assert.AreNotEqual(0, FlagAt(r, -45, 11) & (ushort)CellFlags.Building,
                "building ring around courtyard must be filled");
            Assert.AreEqual(info.id, BidAt(r, -45, 11));
        }

        [Test]
        public void Stats_CellsByBit_Populated()
        {
            var model = EmptyModel();
            model.areas = new[] { Building(1, Rect(-3, -3, 3, 3)) };
            model.roads = new[]
            {
                new Road { osmId = 2, widthM = 3f, points = new[] { new Vec2d(-10, 0), new Vec2d(10, 0) } }
            };

            var r = CityRasterizer.Bake(model, Cfg());
            Assert.AreEqual(16, r.stats.cellsByBit.Length);
            Assert.Greater(r.stats.cellsByBit[BitIndex(CellFlags.Building)], 0);
            Assert.Greater(r.stats.buildingCount, 0);
        }

        static int BitIndex(CellFlags f)
        {
            int v = (int)f;
            int i = 0;
            while ((v >>= 1) != 0)
            {
                i++;
            }

            return i;
        }

        static string FindMapOsm()
        {
            string[] hits = Directory.GetFiles(Application.dataPath, "map.osm", SearchOption.AllDirectories);
            return hits.Length > 0 ? hits[0] : null;
        }
    }
}
