using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCity.City;
using ZombieCity.City.Core;

namespace ZombieCity.City.EditorTools
{
    public static class ExportLayersPngMenu
    {
        const string MenuPath = "ZombieCity/City/Export Layers PNG...";
        const string DefaultBake = "Assets/_Project/City/Baked/CityBake_map.asset";
        const string OutFlags = "DOC/reports/img/raster_flags.png";
        const string OutBuildings = "DOC/reports/img/raster_buildings.png";

        static readonly Color32 ColEmpty = Hex("#101010");
        static readonly Color32 ColBuilding = Hex("#3A3A3A");
        static readonly Color32 ColWater = Hex("#2E7DFF");
        static readonly Color32 ColBarrier = Hex("#FF7A00");
        static readonly Color32 ColGate = Hex("#FFE000");
        static readonly Color32 ColRail = Hex("#9A4DFF");
        static readonly Color32 ColCrosswalk = Hex("#FFFFFF");
        static readonly Color32 ColSidewalk = Hex("#C8C8C8");
        static readonly Color32 ColRoad = Hex("#5A5A5A");
        static readonly Color32 ColGreen = Hex("#3FA34D");
        static readonly Color32 ColBridgeTint = Hex("#00D1C1");

        [MenuItem(MenuPath)]
        static void Export()
        {
            CityBakeAsset bake = AssetDatabase.LoadAssetAtPath<CityBakeAsset>(DefaultBake);
            if (bake == null)
            {
                Debug.LogWarning("[City] Export PNG: bake CityBake_map.asset first.");
                return;
            }

            Export(bake);
        }

        public static void Export(CityBakeAsset bake)
        {
            string flagsPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutFlags);
            string bldPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutBuildings);
            Directory.CreateDirectory(Path.GetDirectoryName(flagsPath));

            WriteFlagsPng(bake, flagsPath);
            WriteBuildingsPng(bake, bldPath);
            Debug.Log($"[City] Exported PNG:\n  {OutFlags}\n  {OutBuildings}");
        }

        static void WriteFlagsPng(CityBakeAsset bake, string path)
        {
            int w = bake.w;
            int h = bake.h;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            ushort[] flags = bake.GetFlagsArray();

            // north = top → j=h-1 is top row in image (Unity tex y-up: row 0 = bottom)
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    ushort f = flags[i + j * w];
                    Color32 c = ColorForFlags(f);
                    tex.SetPixel(i, j, c);
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static void WriteBuildingsPng(CityBakeAsset bake, string path)
        {
            int w = bake.w;
            int h = bake.h;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            ushort[] ids = bake.GetBuildingIdArray();
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    ushort id = ids[i + j * w];
                    Color32 c = id == 0 ? ColEmpty : HashColor(id);
                    tex.SetPixel(i, j, c);
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static Color32 ColorForFlags(ushort f)
        {
            // Priority: Building > Water > Barrier > Gate > Rail > Crosswalk > Sidewalk/Cycleway/Steps > Road > Green > empty
            // Bridge tint applied on top.
            Color32 baseCol = ColEmpty;
            if ((f & (ushort)CellFlags.Building) != 0)
            {
                baseCol = ColBuilding;
            }
            else if ((f & (ushort)CellFlags.Water) != 0)
            {
                baseCol = ColWater;
            }
            else if ((f & (ushort)CellFlags.Barrier) != 0)
            {
                baseCol = ColBarrier;
            }
            else if ((f & (ushort)CellFlags.Gate) != 0)
            {
                baseCol = ColGate;
            }
            else if ((f & (ushort)CellFlags.Rail) != 0)
            {
                baseCol = ColRail;
            }
            else if ((f & (ushort)CellFlags.Crosswalk) != 0)
            {
                baseCol = ColCrosswalk;
            }
            else if ((f & (ushort)(CellFlags.Sidewalk | CellFlags.Cycleway | CellFlags.Steps | CellFlags.SidewalkGenerated)) != 0)
            {
                baseCol = ColSidewalk;
            }
            else if ((f & (ushort)CellFlags.Road) != 0)
            {
                baseCol = ColRoad;
            }
            else if ((f & (ushort)CellFlags.Green) != 0)
            {
                baseCol = ColGreen;
            }

            if ((f & (ushort)CellFlags.Bridge) != 0)
            {
                baseCol = Tint(baseCol, ColBridgeTint, 0.45f);
            }

            return baseCol;
        }

        static Color32 Tint(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)(a.r + (b.r - a.r) * t),
                (byte)(a.g + (b.g - a.g) * t),
                (byte)(a.b + (b.b - a.b) * t),
                255);
        }

        static Color32 HashColor(ushort id)
        {
            unchecked
            {
                uint x = id * 2654435761u;
                return new Color32((byte)(40 + (x & 0x7F)), (byte)(40 + ((x >> 8) & 0x7F)), (byte)(40 + ((x >> 16) & 0x7F)), 255);
            }
        }

        static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
