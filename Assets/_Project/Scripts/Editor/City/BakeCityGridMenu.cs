using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCity.City;
using ZombieCity.City.Core;
using Debug = UnityEngine.Debug;

namespace ZombieCity.City.EditorTools
{
    public static class BakeCityGridMenu
    {
        const string MenuPath = "ZombieCity/City/Bake Grid...";
        const string BakedFolder = "Assets/_Project/City/Baked";
        const string DefaultModel = "Assets/_Project/City/Baked/CityModel_map.asset";
        const string DefaultSettings = "Assets/_Project/City/Settings/CityBakeSettings.asset";

        [MenuItem(MenuPath)]
        static void BakeGrid()
        {
            CityModelAsset modelAsset = AssetDatabase.LoadAssetAtPath<CityModelAsset>(DefaultModel);
            if (modelAsset == null)
            {
                string path = EditorUtility.OpenFilePanel("Select CityModelAsset", Application.dataPath + "/_Project/City/Baked", "asset");
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                string rel = "Assets" + path.Replace("\\", "/").Substring(Application.dataPath.Replace("\\", "/").Length);
                modelAsset = AssetDatabase.LoadAssetAtPath<CityModelAsset>(rel);
            }

            CityBakeSettings settings = AssetDatabase.LoadAssetAtPath<CityBakeSettings>(DefaultSettings);
            if (modelAsset == null || settings == null)
            {
                Debug.LogWarning("[City] Bake cancelled: need CityModelAsset and CityBakeSettings.");
                return;
            }

            Bake(modelAsset, settings);
        }

        public static string Bake(CityModelAsset modelAsset, CityBakeSettings settings)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                EditorUtility.DisplayProgressBar("City Bake", "Rasterizing…", 0.3f);
                CityBakeConfig config = settings.config ?? new CityBakeConfig();
                RasterBakeResult result = CityRasterizer.Bake(modelAsset.model, config);
                EditorUtility.DisplayProgressBar("City Bake", "Writing asset…", 0.8f);

                if (!AssetDatabase.IsValidFolder(BakedFolder))
                {
                    AssetDatabase.CreateFolder("Assets/_Project/City", "Baked");
                }

                string assetPath = $"{BakedFolder}/CityBake_map.asset";
                CityBakeAsset asset = AssetDatabase.LoadAssetAtPath<CityBakeAsset>(assetPath);
                bool created = false;
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<CityBakeAsset>();
                    AssetDatabase.CreateAsset(asset, assetPath);
                    created = true;
                }

                string guidBefore = AssetDatabase.AssetPathToGUID(assetPath);
                ulong dataHashBefore = asset.dataHash;
                asset.ApplyBakeResult(result);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                string guidAfter = AssetDatabase.AssetPathToGUID(assetPath);

                sw.Stop();
                LogSummary(result, asset, sw.ElapsedMilliseconds, guidBefore == guidAfter, created, dataHashBefore);
                return assetPath;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void LogSummary(
            RasterBakeResult r, CityBakeAsset asset, long ms, bool guidStable, bool created, ulong dataHashBefore)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[City] Bake Grid summary");
            sb.AppendLine($"  grid={r.grid.w}×{r.grid.h} cell={r.grid.cell} m  origin=({r.grid.origin.x:F2},{r.grid.origin.z:F2})");
            sb.AppendLine($"  time={ms} ms  created={created} guidStable={guidStable}");
            sb.AppendLine($"  dataHash=0x{r.dataHash:X16}  configHash=0x{r.configHash:X16}  modelHash=0x{r.modelDataHash:X16}");
            sb.AppendLine($"  accessHash=0x{asset.accessHash:X16}");
            if (dataHashBefore != 0)
            {
                sb.AppendLine($"  dataHashBefore=0x{dataHashBefore:X16} unchanged={(dataHashBefore == r.dataHash)}");
            }

            sb.AppendLine($"  buildings={r.stats.buildingCount} zeroCells={r.stats.buildingsWithZeroCells} conflicts={r.stats.buildingConflicts}");
            sb.AppendLine($"  underBridges={r.stats.cellsUnderBridges} waterCleared={r.stats.waterClearedByLinear} railCleared={r.stats.railClearedByLinear} tramStamped={r.stats.tramStamped} tramCells={r.stats.tramCells} tramSkipped={r.stats.tramSkipped} outside={r.stats.objectsOutsideGrid}");
            sb.AppendLine($"  Road|Crosswalk cells={r.stats.cellsRoadAndCrosswalk}");
            double cellArea = r.grid.cell * (double)r.grid.cell;
            string[] names =
            {
                "Road", "Sidewalk", "Crosswalk", "Building", "Water", "Barrier", "Rail", "Green",
                "Bridge", "SidewalkGen", "BuildingPassage", "Cycleway", "Steps", "Gate", "Shared", "Tram"
            };
            for (int b = 0; b < 16; b++)
            {
                int c = r.stats.cellsByBit != null && b < r.stats.cellsByBit.Length ? r.stats.cellsByBit[b] : 0;
                if (c == 0)
                {
                    continue;
                }

                sb.AppendLine($"  {names[b]}: {c} cells ({c * cellArea:F1} m²)");
            }

            Debug.Log(sb.ToString());
        }
    }
}
