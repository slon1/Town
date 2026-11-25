using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCity.City;
using ZombieCity.City.Core;

namespace ZombieCity.City.EditorTools
{
    public static class AnalyzeAccessMenu
    {
        const string MenuPath = "ZombieCity/City/Analyze Access";
        const string DefaultBake = "Assets/_Project/City/Baked/CityBake_map.asset";
        const string DefaultSettings = "Assets/_Project/City/Settings/CityBakeSettings.asset";
        const string OutMd = "Assets/_Project/City/Baked/analysis_access.md";

        [MenuItem(MenuPath)]
        static void Analyze()
        {
            CityBakeAsset bake = AssetDatabase.LoadAssetAtPath<CityBakeAsset>(DefaultBake);
            CityBakeSettings settings = AssetDatabase.LoadAssetAtPath<CityBakeSettings>(DefaultSettings);
            if (bake == null)
            {
                Debug.LogWarning("[City] Analyze Access: bake CityBake_map.asset first.");
                return;
            }

            int islandMin = settings?.config?.islandMinCells ?? 200;
            string md = BuildReportMarkdown(bake, islandMin, out string consoleBlock);
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutMd));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, md, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log(consoleBlock + "\n[City] Wrote " + OutMd);
        }

        public static string BuildReportMarkdown(CityBakeAsset bake, int islandMinCells, out string consoleBlock)
        {
            if (bake.access == null || bake.access.Length != bake.w * bake.h)
            {
                bake.RebuildAccessFromFlags();
            }

            CityGrid grid = bake.Grid;
            byte[] access = bake.GetAccessArray();
            ushort[] flags = bake.GetFlagsArray();

            var ped = AccessConnectivity.Analyze(grid, access, flags, islandMinCells, AccessConnectivity.Mask.Pedestrian);
            var zom = AccessConnectivity.Analyze(grid, access, flags, islandMinCells, AccessConnectivity.Mask.Zombie);

            double cellArea = grid.cell * (double)grid.cell;
            int total = bake.w * bake.h;
            var sb = new StringBuilder();
            sb.AppendLine("# analysis_access");
            sb.AppendLine();
            sb.AppendLine($"grid={bake.w}×{bake.h} cell={bake.cell.ToString(CultureInfo.InvariantCulture)} m");
            sb.AppendLine($"islandMinCells={islandMinCells}");
            sb.AppendLine($"dataHash=0x{bake.dataHash:X16} accessHash=0x{bake.accessHash:X16}");
            sb.AppendLine();
            sb.AppendLine("## Class shares");
            sb.AppendLine();
            sb.AppendLine("| Class | cells | m² | % of grid |");
            sb.AppendLine("|-------|------:|---:|----------:|");
            AppendShare(sb, "Obstacle", ped.obstacleCells, cellArea, total);
            AppendShare(sb, "Roadway", ped.roadwayCells, cellArea, total);
            AppendShare(sb, "Pedestrian", ped.pedestrianCells, cellArea, total);
            sb.AppendLine();
            sb.AppendLine("## PedestrianMask components");
            sb.AppendLine();
            AppendComponents(sb, ped, cellArea, topN: 15);
            sb.AppendLine();
            sb.AppendLine($"islands (&lt; {islandMinCells} cells): **{ped.islandCount}** (top 10 listed)");
            AppendIslands(sb, ped);
            sb.AppendLine();
            sb.AppendLine("## Crosswalk effect (PedestrianMask)");
            sb.AppendLine();
            sb.AppendLine($"| without Crosswalk | with Crosswalk | Crosswalk cells | connecting |");
            sb.AppendLine($"|------------------:|---------------:|----------------:|-----------:|");
            sb.AppendLine($"| {ped.pedestrianComponentsWithoutCrosswalk} | {ped.pedestrianComponentsWithCrosswalk} | {ped.crosswalkCellCount} | {ped.crosswalkCellsConnecting} |");
            sb.AppendLine();
            sb.AppendLine("## ZombieMask components");
            sb.AppendLine();
            AppendComponents(sb, zom, cellArea, topN: 10);
            sb.AppendLine();
            sb.AppendLine($"islands: **{zom.islandCount}**");
            sb.AppendLine();
            sb.AppendLine("## Road|Rail (level crossings)");
            sb.AppendLine();
            sb.AppendLine($"cells: **{ped.roadRailCellCount}**");
            if (ped.roadRailCenters != null && ped.roadRailCenters.Length > 0)
            {
                int show = System.Math.Min(20, ped.roadRailCenters.Length);
                sb.AppendLine();
                sb.AppendLine("| # | x | z |");
                sb.AppendLine("|--:|--:|--:|");
                for (int i = 0; i < show; i++)
                {
                    var c = ped.roadRailCenters[i];
                    sb.AppendLine($"| {i + 1} | {c.x.ToString("F2", CultureInfo.InvariantCulture)} | {c.z.ToString("F2", CultureInfo.InvariantCulture)} |");
                }
            }

            consoleBlock = BuildConsole(ped, zom, islandMinCells, bake);
            return sb.ToString();
        }

        static void AppendShare(StringBuilder sb, string name, int cells, double cellArea, int total)
        {
            double pct = total > 0 ? 100.0 * cells / total : 0;
            sb.AppendLine($"| {name} | {cells} | {(cells * cellArea).ToString("F1", CultureInfo.InvariantCulture)} | {pct.ToString("F2", CultureInfo.InvariantCulture)} |");
        }

        static void AppendComponents(StringBuilder sb, AccessConnectivity.Report r, double cellArea, int topN)
        {
            sb.AppendLine($"componentCount=**{r.componentCount}**");
            sb.AppendLine();
            sb.AppendLine("| id | cells | m² | share | center x | center z |");
            sb.AppendLine("|---:|------:|---:|------:|---------:|---------:|");
            int n = System.Math.Min(topN, r.components.Length);
            for (int i = 0; i < n; i++)
            {
                var c = r.components[i];
                sb.AppendLine($"| {c.id} | {c.cellCount} | {c.areaM2.ToString("F1", CultureInfo.InvariantCulture)} | {(c.shareOfPassable * 100).ToString("F1", CultureInfo.InvariantCulture)}% | {c.centerX.ToString("F1", CultureInfo.InvariantCulture)} | {c.centerZ.ToString("F1", CultureInfo.InvariantCulture)} |");
            }
        }

        static void AppendIslands(StringBuilder sb, AccessConnectivity.Report r)
        {
            if (r.islands == null || r.islands.Length == 0)
            {
                sb.AppendLine();
                sb.AppendLine("_none in top list_");
                return;
            }

            sb.AppendLine();
            sb.AppendLine("| id | cells | m² | center x | center z |");
            sb.AppendLine("|---:|------:|---:|---------:|---------:|");
            for (int i = 0; i < r.islands.Length; i++)
            {
                var c = r.islands[i];
                sb.AppendLine($"| {c.id} | {c.cellCount} | {c.areaM2.ToString("F1", CultureInfo.InvariantCulture)} | {c.centerX.ToString("F1", CultureInfo.InvariantCulture)} | {c.centerZ.ToString("F1", CultureInfo.InvariantCulture)} |");
            }
        }

        static string BuildConsole(
            AccessConnectivity.Report ped, AccessConnectivity.Report zom, int islandMin, CityBakeAsset bake)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[City] Analyze Access");
            sb.AppendLine($"  Pedestrian components={ped.componentCount} islands={ped.islandCount} (min={islandMin})");
            sb.AppendLine($"  without/with Crosswalk={ped.pedestrianComponentsWithoutCrosswalk}/{ped.pedestrianComponentsWithCrosswalk}; connecting CW cells={ped.crosswalkCellsConnecting}/{ped.crosswalkCellCount}");
            sb.AppendLine($"  Zombie components={zom.componentCount} islands={zom.islandCount}");
            sb.AppendLine($"  Road|Rail cells={ped.roadRailCellCount}");
            sb.AppendLine($"  shares Obstacle/Roadway/Pedestrian={ped.obstacleCells}/{ped.roadwayCells}/{ped.pedestrianCells}");
            sb.AppendLine($"  dataHash=0x{bake.dataHash:X16} accessHash=0x{bake.accessHash:X16}");
            return sb.ToString();
        }
    }
}
