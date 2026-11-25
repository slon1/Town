using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City.EditorTools
{
    /// <summary>
    /// Editor menu for OSM import. 1C: parse + log stats (CityModel write in 1E).
    /// </summary>
    public static class ImportOsmMenu
    {
        const string MenuPath = "ZombieCity/City/Import OSM...";

        static string PrefsKey => "ZombieCity.ImportOsmPath." + Application.dataPath.GetHashCode();

        [MenuItem(MenuPath)]
        static void ImportOsm()
        {
            Debug.Log($"[City] defaults: cellSize={new CityBakeConfig().cellSize}");

            string startDir = Application.dataPath + "/Source";
            string prefsPath = EditorPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(prefsPath))
            {
                string prefsDir = Path.GetDirectoryName(prefsPath);
                if (!string.IsNullOrEmpty(prefsDir) && Directory.Exists(prefsDir))
                {
                    startDir = prefsDir;
                }
            }

            string path = EditorUtility.OpenFilePanel("Select OSM", startDir, "osm");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            EditorPrefs.SetString(PrefsKey, path);
            ParseAndLog(path);
        }

        /// <summary>Parse path and write a single [City] summary block (used by menu and MCP checks).</summary>
        public static OsmDocument ParseAndLog(string path)
        {
            OsmDocument doc = OsmXmlParser.ParseFile(path);
            OsmParseStats s = doc.Stats;
            LocalEquirectangularProjection proj = doc.CreateProjection();
            CityModel model = CityModelBuilder.Build(doc, new CityBakeConfig(), path);
            CityModelStats ms = model.stats;
            Debug.Log(
                $"[City] OSM parse+classify OK\n" +
                $"  file: {path}\n" +
                $"  nodes={s.NodeCount} ways={s.WayCount} relations={s.RelationCount} multipolygons={s.MultipolygonCount}\n" +
                $"  bounds=({s.MinLat:F7},{s.MinLon:F7})-({s.MaxLat:F7},{s.MaxLon:F7})" +
                $" sizeM=({model.meta.sizeXM:F1}×{model.meta.sizeZM:F1})\n" +
                $"  roads={ms.roadCount} walkways={ms.walkwayCount} areas={ms.areaCount} " +
                $"buildings={ms.buildingCount} holes={ms.buildingWithHolesCount}\n" +
                $"  bridges={ms.bridgeCount} tunnels={ms.tunnelCount} crossings={ms.crossingCount}\n" +
                $"  warnings: missingRefs={ms.warningMissingRefs} unclosedMP={ms.warningUnclosedMultipolygons} " +
                $"incompleteMP={ms.warningIncompleteMultipolygons} defaultHeight={ms.warningBuildingDefaultHeight}\n" +
                $"  hash=0x{model.meta.dataHash:X16} parseMs={s.ElapsedMs:F1}");
            return doc;
        }
    }
}
