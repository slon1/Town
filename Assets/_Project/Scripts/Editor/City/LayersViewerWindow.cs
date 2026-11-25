using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCity.City;
using ZombieCity.City.Core;

namespace ZombieCity.City.EditorTools
{
    /// <summary>
    /// ADR-003 §5 — просмотр слоёв Access / Flags / BuildingId / Components.
    /// </summary>
    public sealed class LayersViewerWindow : EditorWindow
    {
        public enum LayerKind
        {
            Access,
            Flags,
            BuildingId,
            ComponentsPedestrian,
            ComponentsZombie
        }

        const string DefaultBake = "Assets/_Project/City/Baked/CityBake_map.asset";

        static readonly Color32 AccessObstacle = Hex("#202020");
        static readonly Color32 AccessRoadway = Hex("#D04040");
        static readonly Color32 AccessPedestrian = Hex("#40C080");

        CityBakeAsset _bake;
        LayerKind _layer = LayerKind.Access;
        Texture2D _tex;
        float _zoom = 1f;
        Vector2 _pan;
        Vector2 _scroll;
        int _hoverI = -1, _hoverJ = -1;
        AccessConnectivity.Report _pedReport;
        AccessConnectivity.Report _zomReport;
        int _islandMin = 200;

        [MenuItem("ZombieCity/City/Layers Viewer")]
        public static void Open()
        {
            var w = GetWindow<LayersViewerWindow>("City Layers");
            w.minSize = new Vector2(640, 480);
            w.Show();
        }

        void OnEnable()
        {
            if (_bake == null)
            {
                _bake = AssetDatabase.LoadAssetAtPath<CityBakeAsset>(DefaultBake);
            }

            Reload();
        }

        void OnDisable()
        {
            DestroyTex();
        }

        void Reload()
        {
            DestroyTex();
            _pedReport = null;
            _zomReport = null;
            if (_bake == null)
            {
                return;
            }

            if (_bake.access == null || _bake.access.Length != _bake.w * _bake.h)
            {
                _bake.RebuildAccessFromFlags();
            }

            var settings = AssetDatabase.LoadAssetAtPath<CityBakeSettings>(
                "Assets/_Project/City/Settings/CityBakeSettings.asset");
            _islandMin = settings?.config?.islandMinCells ?? 200;
            RebuildTexture();
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var newBake = (CityBakeAsset)EditorGUILayout.ObjectField(_bake, typeof(CityBakeAsset), false, GUILayout.Width(280));
            if (newBake != _bake)
            {
                _bake = newBake;
                Reload();
            }

            var newLayer = (LayerKind)EditorGUILayout.EnumPopup(_layer, GUILayout.Width(180));
            if (newLayer != _layer)
            {
                _layer = newLayer;
                RebuildTexture();
            }

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                Reload();
            }

            if (GUILayout.Button("Export PNG", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                ExportCurrentPng();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_bake == null || _tex == null)
            {
                EditorGUILayout.HelpBox("Назначьте CityBakeAsset (Bake Grid).", MessageType.Info);
                return;
            }

            Rect view = GUILayoutUtility.GetRect(position.width, position.height - 60, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleViewInput(view);
            DrawMap(view);
            DrawTooltip(view);
        }

        void HandleViewInput(Rect view)
        {
            Event e = Event.current;
            if (!view.Contains(e.mousePosition))
            {
                return;
            }

            if (e.type == EventType.ScrollWheel)
            {
                float old = _zoom;
                _zoom = Mathf.Clamp(_zoom * (e.delta.y > 0 ? 0.9f : 1.1f), 0.25f, 32f);
                Vector2 local = e.mousePosition - view.position;
                _pan = local - (local - _pan) * (_zoom / old);
                e.Use();
                Repaint();
            }

            if (e.type == EventType.MouseDrag && e.button == 0)
            {
                _pan += e.delta;
                e.Use();
                Repaint();
            }

            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                if (TryMapPixel(view, e.mousePosition, out int i, out int j))
                {
                    _hoverI = i;
                    _hoverJ = j;
                }
                else
                {
                    _hoverI = _hoverJ = -1;
                }

                Repaint();
            }
        }

        void DrawMap(Rect view)
        {
            GUI.BeginGroup(view);
            Rect img = new Rect(_pan.x, _pan.y, _bake.w * _zoom, _bake.h * _zoom);
            // Unity tex y-up: flip vertically so north (j high) is top
            Matrix4x4 prev = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(1, -1), new Vector2(img.x, img.y + img.height * 0.5f));
            GUI.DrawTexture(new Rect(img.x, img.y, img.width, img.height), _tex, ScaleMode.StretchToFill, false);
            GUI.matrix = prev;
            GUI.EndGroup();
        }

        void DrawTooltip(Rect view)
        {
            if (_hoverI < 0 || _bake == null)
            {
                return;
            }

            int i = _hoverI, j = _hoverJ;
            CellFlags flags = _bake.GetFlags(i, j);
            CityAccess access = _bake.GetAccess(i, j);
            ushort bid = _bake.GetBuildingId(i, j);
            Vec2d c = _bake.Grid.CellCenter(i, j);
            long osm = 0;
            if (bid > 0 && _bake.buildings != null)
            {
                for (int b = 0; b < _bake.buildings.Length; b++)
                {
                    if (_bake.buildings[b].id == bid)
                    {
                        osm = _bake.buildings[b].osmId;
                        break;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"cell ({i},{j})  world ({c.x:F2},{c.z:F2})");
            sb.AppendLine($"access={access}");
            sb.AppendLine($"flags={FormatFlags(flags)}");
            sb.Append($"buildingId={bid}");
            if (osm != 0)
            {
                sb.Append($"  osmId={osm}");
            }

            GUI.Label(new Rect(view.x + 8, view.yMax - 72, view.width - 16, 68), sb.ToString(), EditorStyles.helpBox);
        }

        bool TryMapPixel(Rect view, Vector2 mouse, out int i, out int j)
        {
            i = j = -1;
            Vector2 local = mouse - view.position - _pan;
            // flipped draw: y from top
            float fx = local.x / _zoom;
            float fy = (_bake.h * _zoom - local.y) / _zoom;
            i = Mathf.FloorToInt(fx);
            j = Mathf.FloorToInt(fy);
            return _bake.Grid.InBounds(i, j);
        }

        void RebuildTexture()
        {
            DestroyTex();
            if (_bake == null)
            {
                return;
            }

            int w = _bake.w, h = _bake.h;
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[w * h];
            switch (_layer)
            {
                case LayerKind.Access:
                    FillAccess(pixels, w, h);
                    break;
                case LayerKind.Flags:
                    FillFlags(pixels, w, h);
                    break;
                case LayerKind.BuildingId:
                    FillBuildingId(pixels, w, h);
                    break;
                case LayerKind.ComponentsPedestrian:
                    EnsurePed();
                    FillComponents(pixels, w, h, _pedReport);
                    break;
                case LayerKind.ComponentsZombie:
                    EnsureZom();
                    FillComponents(pixels, w, h, _zomReport);
                    break;
            }

            _tex.SetPixels32(pixels);
            _tex.Apply(false, false);
        }

        void EnsurePed()
        {
            if (_pedReport != null)
            {
                return;
            }

            _pedReport = AccessConnectivity.Analyze(
                _bake.Grid, _bake.GetAccessArray(), _bake.GetFlagsArray(), _islandMin,
                AccessConnectivity.Mask.Pedestrian);
        }

        void EnsureZom()
        {
            if (_zomReport != null)
            {
                return;
            }

            _zomReport = AccessConnectivity.Analyze(
                _bake.Grid, _bake.GetAccessArray(), _bake.GetFlagsArray(), _islandMin,
                AccessConnectivity.Mask.Zombie);
        }

        void FillAccess(Color32[] pixels, int w, int h)
        {
            byte[] a = _bake.GetAccessArray();
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int idx = i + j * w;
                pixels[idx] = a[idx] switch
                {
                    (byte)CityAccess.Roadway => AccessRoadway,
                    (byte)CityAccess.Pedestrian => AccessPedestrian,
                    _ => AccessObstacle
                };
            }
        }

        void FillFlags(Color32[] pixels, int w, int h)
        {
            ushort[] flags = _bake.GetFlagsArray();
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                pixels[i + j * w] = ExportLayersPngMenu_ColorForFlags(flags[i + j * w]);
            }
        }

        void FillBuildingId(Color32[] pixels, int w, int h)
        {
            ushort[] ids = _bake.GetBuildingIdArray();
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                ushort id = ids[i + j * w];
                pixels[i + j * w] = id == 0 ? Hex("#101010") : HashColor(id);
            }
        }

        void FillComponents(Color32[] pixels, int w, int h, AccessConnectivity.Report report)
        {
            int[] ids = report.cellComponentId;
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int idx = i + j * w;
                int cid = ids[idx];
                pixels[idx] = cid == 0 ? AccessObstacle : HashColor((ushort)(cid * 17));
            }
        }

        void ExportCurrentPng()
        {
            string name = _layer.ToString().ToLowerInvariant();
            string path = EditorUtility.SaveFilePanel("Export layer PNG", Application.dataPath + "/../DOC/reports/img", name, "png");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllBytes(path, _tex.EncodeToPNG());
            Debug.Log("[City] Layers Viewer exported " + path);
        }

        void DestroyTex()
        {
            if (_tex != null)
            {
                DestroyImmediate(_tex);
                _tex = null;
            }
        }

        // Local copy of flag palette (same as 2E) — avoid making Export helper public churn.
        static Color32 ExportLayersPngMenu_ColorForFlags(ushort f)
        {
            Color32 ColEmpty = Hex("#101010");
            Color32 ColBuilding = Hex("#3A3A3A");
            Color32 ColWater = Hex("#2E7DFF");
            Color32 ColBarrier = Hex("#FF7A00");
            Color32 ColGate = Hex("#FFE000");
            Color32 ColRail = Hex("#9A4DFF");
            Color32 ColCrosswalk = Hex("#FFFFFF");
            Color32 ColSidewalk = Hex("#C8C8C8");
            Color32 ColRoad = Hex("#5A5A5A");
            Color32 ColGreen = Hex("#3FA34D");
            Color32 ColBridgeTint = Hex("#00D1C1");
            Color32 baseCol = ColEmpty;
            if ((f & (ushort)CellFlags.Building) != 0) baseCol = ColBuilding;
            else if ((f & (ushort)CellFlags.Water) != 0) baseCol = ColWater;
            else if ((f & (ushort)CellFlags.Barrier) != 0) baseCol = ColBarrier;
            else if ((f & (ushort)CellFlags.Gate) != 0) baseCol = ColGate;
            else if ((f & (ushort)CellFlags.Rail) != 0) baseCol = ColRail;
            else if ((f & (ushort)CellFlags.Crosswalk) != 0) baseCol = ColCrosswalk;
            else if ((f & (ushort)(CellFlags.Sidewalk | CellFlags.Cycleway | CellFlags.Steps | CellFlags.SidewalkGenerated)) != 0) baseCol = ColSidewalk;
            else if ((f & (ushort)CellFlags.Road) != 0) baseCol = ColRoad;
            else if ((f & (ushort)CellFlags.Green) != 0) baseCol = ColGreen;
            if ((f & (ushort)CellFlags.Bridge) != 0)
            {
                baseCol = new Color32(
                    (byte)(baseCol.r + (ColBridgeTint.r - baseCol.r) * 0.45f),
                    (byte)(baseCol.g + (ColBridgeTint.g - baseCol.g) * 0.45f),
                    (byte)(baseCol.b + (ColBridgeTint.b - baseCol.b) * 0.45f),
                    255);
            }

            return baseCol;
        }

        static string FormatFlags(CellFlags f)
        {
            if (f == CellFlags.None)
            {
                return "None";
            }

            var sb = new StringBuilder();
            foreach (CellFlags bit in Enum.GetValues(typeof(CellFlags)))
            {
                if (bit == CellFlags.None)
                {
                    continue;
                }

                if ((f & bit) != 0)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('|');
                    }

                    sb.Append(bit);
                }
            }

            return sb.ToString();
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

        /// <summary>Batch export for REPORT-003 deliverables.</summary>
        public static void ExportReportImages(CityBakeAsset bake, string outDir, int scale = 1)
        {
            if (bake == null)
            {
                throw new ArgumentNullException(nameof(bake));
            }

            Directory.CreateDirectory(outDir);
            if (bake.access == null || bake.access.Length != bake.w * bake.h)
            {
                bake.RebuildAccessFromFlags();
            }

            int islandMin = 200;
            var ped = AccessConnectivity.Analyze(bake.Grid, bake.GetAccessArray(), bake.GetFlagsArray(), islandMin, AccessConnectivity.Mask.Pedestrian);
            var zom = AccessConnectivity.Analyze(bake.Grid, bake.GetAccessArray(), bake.GetFlagsArray(), islandMin, AccessConnectivity.Mask.Zombie);

            WriteScaled(bake, Path.Combine(outDir, "access.png"), scale, (i, j) =>
            {
                return bake.GetAccess(i, j) switch
                {
                    CityAccess.Roadway => AccessRoadway,
                    CityAccess.Pedestrian => AccessPedestrian,
                    _ => AccessObstacle
                };
            });
            WriteScaled(bake, Path.Combine(outDir, "components_pedestrian.png"), scale, (i, j) =>
            {
                int id = ped.cellComponentId[i + j * bake.w];
                return id == 0 ? AccessObstacle : HashColor((ushort)(id * 17));
            });
            WriteScaled(bake, Path.Combine(outDir, "components_zombie.png"), scale, (i, j) =>
            {
                int id = zom.cellComponentId[i + j * bake.w];
                return id == 0 ? AccessObstacle : HashColor((ushort)(id * 17));
            });

            // Crops ≥400×400, ~3–4 px/cell → ~100–134 cells window, use 4 px/cell
            const int cropCells = 100;
            const int px = 4;
            WriteCrop(bake, Path.Combine(outDir, "crop_panke_osloer.png"), WorldToCell(bake, 68.59, 54.17), cropCells, px);
            WriteCrop(bake, Path.Combine(outDir, "crop_railway.png"), FindRailCenter(bake), cropCells, px);
            WriteCrop(bake, Path.Combine(outDir, "crop_courtyard_3583425.png"), BuildingCenter(bake, 3583425), cropCells, px);
        }

        static (int i, int j) WorldToCell(CityBakeAsset bake, double x, double z)
        {
            bake.Grid.WorldToCell(new Vec2d(x, z), out int i, out int j);
            return (i, j);
        }

        static (int i, int j) FindRailCenter(CityBakeAsset bake)
        {
            ushort[] flags = bake.GetFlagsArray();
            long si = 0, sj = 0;
            int n = 0;
            for (int j = 0; j < bake.h; j++)
            for (int i = 0; i < bake.w; i++)
            {
                if ((flags[i + j * bake.w] & (ushort)CellFlags.Rail) != 0)
                {
                    si += i;
                    sj += j;
                    n++;
                }
            }

            if (n == 0)
            {
                return (bake.w / 2, bake.h / 2);
            }

            return ((int)(si / n), (int)(sj / n));
        }

        static (int i, int j) BuildingCenter(CityBakeAsset bake, long osmId)
        {
            if (bake.buildings != null)
            {
                for (int b = 0; b < bake.buildings.Length; b++)
                {
                    if (bake.buildings[b].osmId == osmId)
                    {
                        var info = bake.buildings[b];
                        return ((info.minI + info.maxI) / 2, (info.minJ + info.maxJ) / 2);
                    }
                }
            }

            return (bake.w / 2, bake.h / 2);
        }

        static void WriteScaled(CityBakeAsset bake, string path, int scale, Func<int, int, Color32> color)
        {
            int w = bake.w, h = bake.h;
            var tex = new Texture2D(w * scale, h * scale, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                Color32 c = color(i, j);
                for (int dy = 0; dy < scale; dy++)
                for (int dx = 0; dx < scale; dx++)
                    tex.SetPixel(i * scale + dx, j * scale + dy, c);
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
        }

        static void WriteCrop(
            CityBakeAsset bake, string path, (int i, int j) center, int cells, int pxPerCell)
        {
            int half = cells / 2;
            int i0 = Mathf.Clamp(center.i - half, 0, bake.w - cells);
            int j0 = Mathf.Clamp(center.j - half, 0, bake.h - cells);
            int cw = Mathf.Min(cells, bake.w - i0);
            int ch = Mathf.Min(cells, bake.h - j0);
            int tw = cw * pxPerCell;
            int th = ch * pxPerCell;
            // ensure ≥400
            if (tw < 400)
            {
                pxPerCell = (400 + cw - 1) / cw;
                tw = cw * pxPerCell;
                th = ch * pxPerCell;
            }

            var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int j = 0; j < ch; j++)
            for (int i = 0; i < cw; i++)
            {
                Color32 c = bake.GetAccess(i0 + i, j0 + j) switch
                {
                    CityAccess.Roadway => AccessRoadway,
                    CityAccess.Pedestrian => AccessPedestrian,
                    _ => AccessObstacle
                };
                for (int dy = 0; dy < pxPerCell; dy++)
                for (int dx = 0; dx < pxPerCell; dx++)
                    tex.SetPixel(i * pxPerCell + dx, j * pxPerCell + dy, c);
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
        }
    }
}
