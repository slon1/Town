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
            float endRadius = settings?.config?.crosswalkEndSearchRadius ?? 3f;
            float cwFallback = settings?.config?.crosswalkWidthFallback ?? 3f;
            CityModelAsset modelAsset = AssetDatabase.LoadAssetAtPath<CityModelAsset>(
                "Assets/_Project/City/Baked/CityModel_map.asset");
            Walkway[] walkways = modelAsset != null && modelAsset.model != null
                ? modelAsset.model.walkways
                : null;
            string md = BuildReportMarkdown(
                bake, islandMin, endRadius, cwFallback, walkways, out string consoleBlock);
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutMd));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, md, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log(consoleBlock + "\n[City] Wrote " + OutMd);
        }

        public static string BuildReportMarkdown(
            CityBakeAsset bake,
            int islandMinCells,
            float crosswalkEndSearchRadius,
            float crosswalkWidthFallback,
            Walkway[] walkways,
            out string consoleBlock)
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
            sb.AppendLine($"crosswalkEndSearchRadius={crosswalkEndSearchRadius.ToString("F1", CultureInfo.InvariantCulture)} m");
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

            var strict = AccessConnectivity.AnalyzeStrict(
                grid, access, flags, walkways, crosswalkEndSearchRadius, crosswalkWidthFallback, islandMinCells);
            AppendStrict(sb, strict, cellArea);

            consoleBlock = BuildConsole(ped, zom, strict, islandMinCells, bake);
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

        static void AppendStrict(StringBuilder sb, AccessConnectivity.StrictDiagnostics d, double cellArea)
        {
            sb.AppendLine();
            sb.AppendLine("## StrictPedestrianMask");
            sb.AppendLine();
            sb.AppendLine($"| without Crosswalk | with Crosswalk |");
            sb.AppendLine($"|------------------:|---------------:|");
            sb.AppendLine($"| {d.componentsWithoutCrosswalk} | {d.componentsWithCrosswalk} |");
            sb.AppendLine();
            sb.AppendLine("### Strict without Crosswalk (top 10)");
            sb.AppendLine();
            AppendStrictComponents(sb, d.strictWithout, 10);
            sb.AppendLine();
            sb.AppendLine("### Strict with Crosswalk (top 10)");
            sb.AppendLine();
            AppendStrictComponents(sb, d.strictWith, 10);
            sb.AppendLine();
            sb.AppendLine("## Component size histogram");
            sb.AppendLine();
            sb.AppendLine("| mask | ≤10 | 11–50 | 51–199 | ≥200 |");
            sb.AppendLine("|------|----:|------:|-------:|-----:|");
            AppendHistogram(sb, "PedestrianMask", d.histogramPedestrian);
            AppendHistogram(sb, "StrictPedestrianMask", d.histogramStrict);
            sb.AppendLine();
            sb.AppendLine("## Crosswalk groups (cell cross-check)");
            sb.AppendLine();
            sb.AppendLine($"groups=**{d.crosswalkGroupCount}** connecting (neighbor ids ≥ 2)=**{d.connectingGroupCount}**");
            sb.AppendLine();
            sb.AppendLine("## Unmarked crossings (Road+Sidewalk, no Crosswalk)");
            sb.AppendLine();
            sb.AppendLine($"cells=**{d.unmarkedCellCount}** clusters=**{d.unmarkedClusterCount}**");
            if (d.unmarkedLargest != null && d.unmarkedLargest.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("| # | cells | center x | center z |");
                sb.AppendLine("|--:|------:|---------:|---------:|");
                for (int i = 0; i < d.unmarkedLargest.Length; i++)
                {
                    var c = d.unmarkedLargest[i];
                    sb.AppendLine($"| {i + 1} | {c.cellCount} | {c.centerX.ToString("F1", CultureInfo.InvariantCulture)} | {c.centerZ.ToString("F1", CultureInfo.InvariantCulture)} |");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## Crossings by way (ADR-003 §4.1)");
            sb.AppendLine();
            sb.AppendLine("| osmId | cells | end0 | dist0 m | end1 | dist1 m | status |");
            sb.AppendLine("|------:|------:|------|--------:|------|--------:|--------|");
            if (d.crossings != null)
            {
                for (int i = 0; i < d.crossings.Length; i++)
                {
                    var row = d.crossings[i];
                    sb.AppendLine(
                        $"| {row.osmId} | {row.cellCount} | {AccessConnectivity.FormatEnd(row.end0)} | {Dist(row.end0)} | {AccessConnectivity.FormatEnd(row.end1)} | {Dist(row.end1)} | {StatusText(row.status)} |");
                }
            }
        }

        static void AppendStrictComponents(StringBuilder sb, AccessConnectivity.ComponentInfo[] components, int topN)
        {
            sb.AppendLine("| id | cells | m² | share | center x | center z |");
            sb.AppendLine("|---:|------:|---:|------:|---------:|---------:|");
            if (components == null)
            {
                return;
            }

            int n = System.Math.Min(topN, components.Length);
            for (int i = 0; i < n; i++)
            {
                var c = components[i];
                sb.AppendLine($"| {c.id} | {c.cellCount} | {c.areaM2.ToString("F1", CultureInfo.InvariantCulture)} | {(c.shareOfPassable * 100).ToString("F1", CultureInfo.InvariantCulture)}% | {c.centerX.ToString("F1", CultureInfo.InvariantCulture)} | {c.centerZ.ToString("F1", CultureInfo.InvariantCulture)} |");
            }
        }

        static void AppendHistogram(StringBuilder sb, string name, AccessConnectivity.SizeHistogram h)
        {
            sb.AppendLine($"| {name} | {h.upTo10} | {h.from11To50} | {h.from51To199} | {h.atLeast200} |");
        }

        static string Dist(AccessConnectivity.CrossingEnd end)
        {
            return end.hasSidewalk ? end.distanceM.ToString("F2", CultureInfo.InvariantCulture) : "—";
        }

        static string StatusText(AccessConnectivity.CrossingEndStatus status)
        {
            switch (status)
            {
                case AccessConnectivity.CrossingEndStatus.BothEndsSameComponent:
                    return "оба конца в одной компоненте";
                case AccessConnectivity.CrossingEndStatus.ConnectsDifferent:
                    return "соединяет разные";
                case AccessConnectivity.CrossingEndStatus.OneEndWithoutSidewalk:
                    return "один конец без тротуара";
                default:
                    return "оба конца без тротуара";
            }
        }

        static string BuildConsole(
            AccessConnectivity.Report ped,
            AccessConnectivity.Report zom,
            AccessConnectivity.StrictDiagnostics strict,
            int islandMin,
            CityBakeAsset bake)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[City] Analyze Access");
            sb.AppendLine($"  Pedestrian components={ped.componentCount} islands={ped.islandCount} (min={islandMin})");
            sb.AppendLine($"  without/with Crosswalk={ped.pedestrianComponentsWithoutCrosswalk}/{ped.pedestrianComponentsWithCrosswalk}; connecting CW cells={ped.crosswalkCellsConnecting}/{ped.crosswalkCellCount}");
            sb.AppendLine($"  Zombie components={zom.componentCount} islands={zom.islandCount}");
            sb.AppendLine($"  Strict without/with Crosswalk={strict.componentsWithoutCrosswalk}/{strict.componentsWithCrosswalk}; groups connecting={strict.connectingGroupCount}/{strict.crosswalkGroupCount}");
            sb.AppendLine($"  unmarked cells/clusters={strict.unmarkedCellCount}/{strict.unmarkedClusterCount}; crossings={strict.crossings.Length}");
            sb.AppendLine($"  Road|Rail cells={ped.roadRailCellCount}");
            sb.AppendLine($"  shares Obstacle/Roadway/Pedestrian={ped.obstacleCells}/{ped.roadwayCells}/{ped.pedestrianCells}");
            sb.AppendLine($"  dataHash=0x{bake.dataHash:X16} accessHash=0x{bake.accessHash:X16}");
            return sb.ToString();
        }
    }
}
