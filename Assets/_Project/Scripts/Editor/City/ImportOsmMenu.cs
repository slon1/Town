using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCity.City;
using ZombieCity.City.Core;
using Debug = UnityEngine.Debug;

namespace ZombieCity.City.EditorTools
{
    /// <summary>
    /// Editor menu: OSM → CityModelAsset (TASK-001/1E).
    /// </summary>
    public static class ImportOsmMenu
    {
        const string MenuPath = "ZombieCity/City/Import OSM...";
        const string BakedFolder = "Assets/_Project/City/Baked";
        const string DefaultSettingsPath = "Assets/_Project/City/Settings/CityBakeSettings.asset";

        static string PrefsKeyOsm => "ZombieCity.ImportOsmPath." + Application.dataPath.GetHashCode();
        static string PrefsKeySettings => "ZombieCity.ImportSettingsPath." + Application.dataPath.GetHashCode();

        [MenuItem(MenuPath)]
        static void ImportOsm()
        {
            string startDir = Application.dataPath + "/Source";
            string prefsPath = EditorPrefs.GetString(PrefsKeyOsm, string.Empty);
            if (!string.IsNullOrEmpty(prefsPath))
            {
                string prefsDir = Path.GetDirectoryName(prefsPath);
                if (!string.IsNullOrEmpty(prefsDir) && Directory.Exists(prefsDir))
                {
                    startDir = prefsDir;
                }
            }

            string osmPath = EditorUtility.OpenFilePanel("Select OSM", startDir, "osm");
            if (string.IsNullOrEmpty(osmPath))
            {
                return;
            }

            EditorPrefs.SetString(PrefsKeyOsm, osmPath);

            CityBakeSettings settings = LoadOrPickSettings();
            if (settings == null)
            {
                Debug.LogWarning("[City] Import cancelled: no CityBakeSettings.");
                return;
            }

            Import(osmPath, settings);
        }

        static CityBakeSettings LoadOrPickSettings()
        {
            string prefs = EditorPrefs.GetString(PrefsKeySettings, DefaultSettingsPath);
            CityBakeSettings settings = AssetDatabase.LoadAssetAtPath<CityBakeSettings>(prefs);
            if (settings == null)
            {
                settings = AssetDatabase.LoadAssetAtPath<CityBakeSettings>(DefaultSettingsPath);
            }

            if (settings != null)
            {
                EditorPrefs.SetString(PrefsKeySettings, AssetDatabase.GetAssetPath(settings));
                return settings;
            }

            string abs = EditorUtility.OpenFilePanel("Select CityBakeSettings", Application.dataPath + "/_Project/City/Settings", "asset");
            if (string.IsNullOrEmpty(abs))
            {
                return null;
            }

            string rel = AbsoluteToAssetPath(abs);
            settings = AssetDatabase.LoadAssetAtPath<CityBakeSettings>(rel);
            if (settings != null)
            {
                EditorPrefs.SetString(PrefsKeySettings, rel);
            }

            return settings;
        }

        /// <summary>Full import used by menu and automated checks. Returns asset path.</summary>
        public static string Import(string osmAbsolutePath, CityBakeSettings settings)
        {
            if (settings == null)
            {
                throw new System.ArgumentNullException(nameof(settings));
            }

            if (settings.config == null)
            {
                settings.config = new CityBakeConfig();
            }

            var sw = Stopwatch.StartNew();
            try
            {
                EditorUtility.DisplayProgressBar("[City] Import OSM", "Parsing…", 0.1f);
                OsmDocument doc = OsmXmlParser.ParseFile(osmAbsolutePath);

                EditorUtility.DisplayProgressBar("[City] Import OSM", "Classifying…", 0.45f);
                CityModel model = CityModelBuilder.Build(doc, settings.config, osmAbsolutePath);

                EditorUtility.DisplayProgressBar("[City] Import OSM", "Writing asset…", 0.8f);
                string assetPath = WriteOrOverwriteAsset(osmAbsolutePath, model);

                sw.Stop();
                LogSummary(osmAbsolutePath, doc, model, sw.Elapsed.TotalMilliseconds, assetPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return assetPath;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Compatibility helper for earlier steps / MCP.</summary>
        public static OsmDocument ParseAndLog(string path)
        {
            CityBakeSettings settings = AssetDatabase.LoadAssetAtPath<CityBakeSettings>(DefaultSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CityBakeSettings>();
                settings.config = new CityBakeConfig();
            }

            Import(path, settings);
            return OsmXmlParser.ParseFile(path);
        }

        static string WriteOrOverwriteAsset(string osmAbsolutePath, CityModel model)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/City"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "City");
            }

            if (!AssetDatabase.IsValidFolder(BakedFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/City", "Baked");
            }

            string osmName = Path.GetFileNameWithoutExtension(osmAbsolutePath);
            if (string.IsNullOrEmpty(osmName))
            {
                osmName = "map";
            }

            string assetPath = $"{BakedFolder}/CityModel_{osmName}.asset";
            CityModelAsset existing = AssetDatabase.LoadAssetAtPath<CityModelAsset>(assetPath);
            if (existing != null)
            {
                existing.model = model;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return assetPath;
            }

            var created = ScriptableObject.CreateInstance<CityModelAsset>();
            created.model = model;
            AssetDatabase.CreateAsset(created, assetPath);
            return assetPath;
        }

        static void LogSummary(
            string osmPath,
            OsmDocument doc,
            CityModel model,
            double totalMs,
            string assetPath)
        {
            OsmParseStats s = doc.Stats;
            CityModelStats ms = model.stats;
            CityModelMeta meta = model.meta;
            int mp = s.MultipolygonCount;
            double incompletePct = mp > 0 ? 100.0 * ms.warningIncompleteMultipolygons / mp : 0.0;

            Debug.Log(
                "[City] Import OSM complete\n" +
                $"  file: {osmPath}\n" +
                $"  asset: {assetPath}\n" +
                $"  bbox deg: ({meta.bboxMinLat:F7},{meta.bboxMinLon:F7})-({meta.bboxMaxLat:F7},{meta.bboxMaxLon:F7})\n" +
                $"  bbox m: {meta.sizeXM:F1} × {meta.sizeZM:F1} (lat0={meta.lat0:F7}, lon0={meta.lon0:F7})\n" +
                $"  nodes={s.NodeCount} ways={s.WayCount} relations={s.RelationCount} multipolygons={mp}\n" +
                $"  roads={ms.roadCount} (len={ms.roadLengthM:F0} m) walkways={ms.walkwayCount} (len={ms.walkwayLengthM:F0} m)\n" +
                $"  areas={ms.areaCount} buildings={ms.buildingCount} holes={ms.buildingWithHolesCount} " +
                $"height avg/max={ms.buildingHeightAvgM:F1}/{ms.buildingHeightMaxM:F1} m\n" +
                $"  waterways={ms.waterwayCount} railways={ms.railwayCount} barriers={ms.barrierCount} points={ms.pointFeatureCount}\n" +
                $"  bridges={ms.bridgeCount} tunnels={ms.tunnelCount} crossings={ms.crossingCount}\n" +
                $"  warnings: missingRefs={ms.warningMissingRefs} unclosedMP={ms.warningUnclosedMultipolygons} " +
                $"incompleteMP={ms.warningIncompleteMultipolygons} ({incompletePct:F1}% of MP) " +
                $"defaultHeight={ms.warningBuildingDefaultHeight} buildingParts={ms.warningBuildingPartsIgnored}\n" +
                $"  settingsHash=0x{meta.settingsHash:X16} dataHash=0x{meta.dataHash:X16}\n" +
                $"  elapsedMs={totalMs:F1}");
        }

        static string AbsoluteToAssetPath(string absolute)
        {
            absolute = absolute.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/');
            if (absolute.StartsWith(data))
            {
                return "Assets" + absolute.Substring(data.Length);
            }

            return absolute;
        }
    }
}
