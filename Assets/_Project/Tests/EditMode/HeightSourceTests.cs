using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// ADR-001 v1.4 heightSource (6B-tests, red before the builder and hasher write the field).
    /// </summary>
    public class HeightSourceTests
    {
        const string OsmHead = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<osm version=""0.6"">
 <bounds minlat=""0"" minlon=""0"" maxlat=""0.01"" maxlon=""0.01""/>
 <node id=""1"" lat=""0.001"" lon=""0.001""/>
 <node id=""2"" lat=""0.001"" lon=""0.002""/>
 <node id=""3"" lat=""0.002"" lon=""0.002""/>
 <node id=""4"" lat=""0.002"" lon=""0.001""/>
 <way id=""10"">
  <nd ref=""1""/><nd ref=""2""/><nd ref=""3""/><nd ref=""4""/><nd ref=""1""/>
  <tag k=""building"" v=""yes""/>
";

        static Area BuildBuilding(string extraTags)
        {
            string xml = OsmHead + extraTags + "</way></osm>";
            CityModel model = CityModelBuilder.Build(OsmXmlParser.ParseXml(xml), new CityBakeConfig());
            Assert.AreEqual(1, model.areas.Length);
            return model.areas[0];
        }

        [Test]
        public void HeightTag_WhenHeightPresent()
        {
            Area area = BuildBuilding("<tag k=\"height\" v=\"10\"/>");
            Assert.AreEqual(HeightSource.HeightTag, area.heightSource);
        }

        [Test]
        public void Levels_WhenBuildingLevelsWithoutHeight()
        {
            Area area = BuildBuilding("<tag k=\"building:levels\" v=\"4\"/>");
            Assert.AreEqual(HeightSource.Levels, area.heightSource);
        }

        [Test]
        public void Levels_WhenOnlyRoofLevels()
        {
            Area area = BuildBuilding("<tag k=\"roof:levels\" v=\"1\"/>");
            Assert.AreEqual(HeightSource.Levels, area.heightSource);
        }

        [Test]
        public void Default_WhenNeitherHeightNorLevels()
        {
            Area area = BuildBuilding(string.Empty);
            Assert.AreEqual(HeightSource.Default, area.heightSource);
        }

        [Test]
        public void Hasher_HeightSource_IsTheOnlyDifference()
        {
            CityModel tagged = Model(HeightSource.HeightTag);
            CityModel fallback = Model(HeightSource.Default);
            ulong taggedHash = CityModelHasher.ComputeDataHash(tagged);
            ulong fallbackHash = CityModelHasher.ComputeDataHash(fallback);
            Assert.AreNotEqual(taggedHash, fallbackHash);

            fallback.areas[0].heightSource = HeightSource.HeightTag;
            Assert.AreEqual(taggedHash, CityModelHasher.ComputeDataHash(fallback));

            fallback.areas[0].heightM = 99f;
            Assert.AreNotEqual(taggedHash, CityModelHasher.ComputeDataHash(fallback));
        }

        [Test]
        public void CityModelAsset_SerializesHeightSource()
        {
            Type assetType = Type.GetType("ZombieCity.City.CityModelAsset, Assembly-CSharp");
            Assert.IsNotNull(assetType, "CityModelAsset must stay in Assembly-CSharp");
            var asset = (ScriptableObject)ScriptableObject.CreateInstance(assetType);
            var modelField = assetType.GetField("model");
            Assert.IsNotNull(modelField);
            var model = (CityModel)modelField.GetValue(asset);
            model.areas = new[]
            {
                new Area
                {
                    osmId = 7,
                    kind = AreaKind.Building,
                    heightSource = HeightSource.Levels
                }
            };

            string json = EditorJsonUtility.ToJson(asset);
            var copy = (ScriptableObject)ScriptableObject.CreateInstance(assetType);
            EditorJsonUtility.FromJsonOverwrite(json, copy);
            var copyModel = (CityModel)modelField.GetValue(copy);
            Assert.AreEqual(HeightSource.Levels, copyModel.areas[0].heightSource);
            UnityEngine.Object.DestroyImmediate(asset);
            UnityEngine.Object.DestroyImmediate(copy);
        }

        static CityModel Model(HeightSource source)
        {
            return new CityModel
            {
                meta = new CityModelMeta { sourceName = "h" },
                areas = new[]
                {
                    new Area
                    {
                        osmId = 1,
                        kind = AreaKind.Building,
                        heightM = 12f,
                        minHeightM = 0f,
                        heightSource = source,
                        buildingType = "yes",
                        points = new[] { new Vec2d(0, 0), new Vec2d(1, 0), new Vec2d(0, 1) },
                        ringStart = new[] { 0 }
                    }
                }
            };
        }
    }
}
