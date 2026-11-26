using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ZombieCity.City;
using ZombieCity.City.Core;

namespace ZombieCity.City.EditorTools
{
    /// <summary>
    /// ADR-003 §5 / §5.1 — просмотр слоёв, оверлеи, экспорт кропов (TASK-005).
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
        const string DefaultModel = "Assets/_Project/City/Baked/CityModel_map.asset";
        const string ReportsImgDir = "DOC/reports/img";
        const float OverlayAlpha = 0.7f;
        const long PankeWaterwayOsmId = 28777932;
        const long CourtyardBuildingOsmId = 3583425;

        static readonly Color32 AccessObstacle = Hex("#202020");
        static readonly Color32 AccessRoadway = Hex("#D04040");
        static readonly Color32 AccessPedestrian = Hex("#40C080");

        static readonly Color32 OvRoadRail = Hex("#FF0000");
        static readonly Color32 OvUnmarked = Hex("#FF00FF");
        static readonly Color32 OvCrosswalk = Hex("#FFE000");
        static readonly Color32 OvGatePassage = Hex("#FFFFFF");
        static readonly Color32 OvBridge = Hex("#00D1C1");

        CityBakeAsset _bake;
        CityModelAsset _cityModel;
        LayerKind _layer = LayerKind.Access;

        bool _ovRoadRail = true;
        bool _ovUnmarked;
        bool _ovCrosswalk = true;
        bool _ovGatePassage;
        bool _ovBridge;

        Texture2D _tex;
        float _zoom = 1f;
        Vector2 _pan;
        int _hoverI = -1, _hoverJ = -1;
        double _lastCursorX;
        double _lastCursorZ;
        bool _hasCursorWorld;

        float _cropCenterX;
        float _cropCenterZ;
        float _cropSizeM = 50f;
        int _cropPxPerCell = 4;

        CrossingEntry[] _crossings = Array.Empty<CrossingEntry>();
        int _crossingIndex;

        AccessConnectivity.Report _pedReport;
        AccessConnectivity.Report _zomReport;
        int _islandMin = 200;

        struct CrossingEntry
        {
            public long osmId;
            public double midX;
            public double midZ;
        }

        struct OverlayEnable
        {
            public bool roadRail, unmarked, crosswalk, gatePassage, bridge;

            public bool Any => roadRail || unmarked || crosswalk || gatePassage || bridge;
        }

        OverlayEnable OverlayToggles => new OverlayEnable
        {
            roadRail = _ovRoadRail,
            unmarked = _ovUnmarked,
            crosswalk = _ovCrosswalk,
            gatePassage = _ovGatePassage,
            bridge = _ovBridge
        };

        [MenuItem("ZombieCity/City/Layers Viewer")]
        public static void Open()
        {
            var w = GetWindow<LayersViewerWindow>("City Layers");
            w.minSize = new Vector2(720, 520);
            w.Show();
        }

        void OnEnable()
        {
            if (_bake == null)
            {
                _bake = AssetDatabase.LoadAssetAtPath<CityBakeAsset>(DefaultBake);
            }

            if (_cityModel == null)
            {
                _cityModel = AssetDatabase.LoadAssetAtPath<CityModelAsset>(DefaultModel);
            }

            ReloadCrossings();
            Reload();
        }

        void OnDisable()
        {
            DestroyTex();
        }

        bool HasModel => _cityModel != null && _cityModel.model != null;

        void ReloadCrossings()
        {
            if (!HasModel)
            {
                _crossings = Array.Empty<CrossingEntry>();
                _crossingIndex = 0;
                return;
            }

            Walkway[] walkways = _cityModel.model.walkways;
            if (walkways == null)
            {
                _crossings = Array.Empty<CrossingEntry>();
                return;
            }

            var list = new List<CrossingEntry>();
            for (int i = 0; i < walkways.Length; i++)
            {
                Walkway w = walkways[i];
                if (w == null || w.kind != WalkwayKind.Crossing || w.points == null || w.points.Length == 0)
                {
                    continue;
                }

                double sx = 0, sz = 0;
                for (int p = 0; p < w.points.Length; p++)
                {
                    sx += w.points[p].x;
                    sz += w.points[p].z;
                }

                list.Add(new CrossingEntry
                {
                    osmId = w.osmId,
                    midX = sx / w.points.Length,
                    midZ = sz / w.points.Length
                });
            }

            list.Sort((a, b) => a.osmId.CompareTo(b.osmId));
            _crossings = list.ToArray();
            _crossingIndex = Mathf.Clamp(_crossingIndex, 0, Math.Max(0, _crossings.Length - 1));
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
            DrawToolbar();
            DrawOverlayPanel();
            DrawExportCropPanel();

            if (_bake == null || _tex == null)
            {
                EditorGUILayout.HelpBox("Назначьте CityBakeAsset (Bake Grid).", MessageType.Info);
                return;
            }

            float chrome = 248f;
            Rect view = GUILayoutUtility.GetRect(position.width, Mathf.Max(120f, position.height - chrome),
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            HandleViewInput(view);
            DrawMap(view);
            DrawTooltip(view);
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var newBake = (CityBakeAsset)EditorGUILayout.ObjectField(_bake, typeof(CityBakeAsset), false, GUILayout.Width(220));
            if (newBake != _bake)
            {
                _bake = newBake;
                Reload();
            }

            var newLayer = (LayerKind)EditorGUILayout.EnumPopup(_layer, GUILayout.Width(140));
            if (newLayer != _layer)
            {
                _layer = newLayer;
                RebuildTexture();
            }

            if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(52)))
            {
                Reload();
            }

            if (GUILayout.Button("Export layer", EditorStyles.toolbarButton, GUILayout.Width(82)))
            {
                ExportCurrentFullPng();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawOverlayPanel()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Overlays", GUILayout.Width(52));
            bool changed = false;
            changed |= TwoWayToggle(ref _ovRoadRail, "Road|Rail", 58);
            changed |= TwoWayToggle(ref _ovUnmarked, "Unmarked", 68);
            changed |= TwoWayToggle(ref _ovCrosswalk, "Crosswalk", 68);
            changed |= TwoWayToggle(ref _ovGatePassage, "Gate/Pass", 68);
            changed |= TwoWayToggle(ref _ovBridge, "Bridge", 52);
            if (changed)
            {
                RebuildTexture();
            }

            EditorGUILayout.EndHorizontal();
        }

        static bool TwoWayToggle(ref bool value, string label, float width)
        {
            bool n = EditorGUILayout.ToggleLeft(label, value, GUILayout.Width(width));
            if (n == value)
            {
                return false;
            }

            value = n;
            return true;
        }

        void DrawExportCropPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Export crop", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _cropCenterX = EditorGUILayout.FloatField("Center X", _cropCenterX);
            _cropCenterZ = EditorGUILayout.FloatField("Center Z", _cropCenterZ);
            _cropSizeM = EditorGUILayout.FloatField("Size m", _cropSizeM);
            _cropPxPerCell = EditorGUILayout.IntSlider("px/cell", _cropPxPerCell, 1, 8);
            if (GUILayout.Button("Center on cursor", GUILayout.Width(120)) && _hasCursorWorld)
            {
                _cropCenterX = (float)_lastCursorX;
                _cropCenterZ = (float)_lastCursorZ;
            }

            if (GUILayout.Button("Export PNG", GUILayout.Width(90)))
            {
                ExportParametricCrop();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var newModel = (CityModelAsset)EditorGUILayout.ObjectField(
                "CityModel", _cityModel, typeof(CityModelAsset), false, GUILayout.Width(280));
            if (newModel != _cityModel)
            {
                _cityModel = newModel;
                ReloadCrossings();
            }

            GUI.enabled = HasModel;
            if (GUILayout.Button("Панке / Osloer", GUILayout.Width(118)))
            {
                if (TryGetPankeCenter(out double x, out double z))
                {
                    ExportNamedCrop("crop_panke_osloer.png", x, z);
                }
            }

            if (GUILayout.Button("Prinzenallee (88;49)", GUILayout.Width(140)))
            {
                ExportNamedCrop("crop_prinzenallee_88_49.png", 88, 49);
            }

            GUI.enabled = _bake != null;
            if (GUILayout.Button("Двор 3583425", GUILayout.Width(110)))
            {
                BuildingCenterWorld(_bake, CourtyardBuildingOsmId, out double x, out double z);
                ExportNamedCrop("crop_courtyard_3583425.png", x, z);
            }

            GUI.enabled = HasModel && _crossings.Length > 0;
            var crossingLabels = new string[_crossings.Length];
            for (int i = 0; i < _crossings.Length; i++)
            {
                crossingLabels[i] = _crossings[i].osmId.ToString(CultureInfo.InvariantCulture);
            }

            _crossingIndex = EditorGUILayout.Popup("Переход osmId", _crossingIndex, crossingLabels, GUILayout.Width(180));
            if (GUILayout.Button("Export crossing", GUILayout.Width(110)) && _crossings.Length > 0)
            {
                var e = _crossings[_crossingIndex];
                ExportNamedCrop($"crop_crossing_{e.osmId}.png", e.midX, e.midZ);
            }

            GUI.enabled = true;
            if (!HasModel)
            {
                EditorGUILayout.LabelField("(пресеты с моделью неактивны)", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
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
                    Vec2d c = _bake.Grid.CellCenter(i, j);
                    _lastCursorX = c.x;
                    _lastCursorZ = c.z;
                    _hasCursorWorld = true;
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
            ushort uf = (ushort)flags;
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
            sb.AppendLine(FormatOverlayTooltip(uf));
            sb.Append($"buildingId={bid}");
            if (osm != 0)
            {
                sb.Append($"  osmId={osm}");
            }

            GUI.Label(new Rect(view.x + 8, view.yMax - 88, view.width - 16, 84), sb.ToString(), EditorStyles.helpBox);
        }

        static string FormatOverlayTooltip(ushort f)
        {
            List<string> names = GetMatchingOverlayNames(f);
            if (names.Count == 0)
            {
                return "overlay: —";
            }

            return "overlay: " + string.Join(", ", names);
        }

        bool TryMapPixel(Rect view, Vector2 mouse, out int i, out int j)
        {
            i = j = -1;
            Vector2 local = mouse - view.position - _pan;
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

            ushort[] flags = _bake.GetFlagsArray();
            var pixels = new Color32[w * h];
            OverlayEnable ov = OverlayToggles;

            switch (_layer)
            {
                case LayerKind.Access:
                    FillAccessLayer(pixels, w, h, flags, ov);
                    break;
                case LayerKind.Flags:
                    FillFlagsLayer(pixels, w, h, flags, ov);
                    break;
                case LayerKind.BuildingId:
                    FillBuildingIdLayer(pixels, w, h, flags, ov);
                    break;
                case LayerKind.ComponentsPedestrian:
                    EnsurePed();
                    FillComponentsLayer(pixels, w, h, flags, ov, _pedReport);
                    break;
                case LayerKind.ComponentsZombie:
                    EnsureZom();
                    FillComponentsLayer(pixels, w, h, flags, ov, _zomReport);
                    break;
            }

            _tex.SetPixels32(pixels);
            _tex.Apply(false, false);
        }

        void FillAccessLayer(Color32[] pixels, int w, int h, ushort[] flags, OverlayEnable ov)
        {
            byte[] a = _bake.GetAccessArray();
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int idx = i + j * w;
                Color32 c = a[idx] switch
                {
                    (byte)CityAccess.Roadway => AccessRoadway,
                    (byte)CityAccess.Pedestrian => AccessPedestrian,
                    _ => AccessObstacle
                };
                ApplyOverlay(ref c, flags[idx], ov);
                pixels[idx] = c;
            }
        }

        void FillFlagsLayer(Color32[] pixels, int w, int h, ushort[] flags, OverlayEnable ov)
        {
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int idx = i + j * w;
                Color32 c = ExportLayersPngMenu_ColorForFlags(flags[idx]);
                ApplyOverlay(ref c, flags[idx], ov);
                pixels[idx] = c;
            }
        }

        void FillBuildingIdLayer(Color32[] pixels, int w, int h, ushort[] flags, OverlayEnable ov)
        {
            ushort[] ids = _bake.GetBuildingIdArray();
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int idx = i + j * w;
                ushort id = ids[idx];
                Color32 c = id == 0 ? Hex("#101010") : HashColor(id);
                ApplyOverlay(ref c, flags[idx], ov);
                pixels[idx] = c;
            }
        }

        void FillComponentsLayer(Color32[] pixels, int w, int h, ushort[] flags, OverlayEnable ov, AccessConnectivity.Report report)
        {
            int[] ids = report.cellComponentId;
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                int idx = i + j * w;
                int cid = ids[idx];
                Color32 c = cid == 0 ? AccessObstacle : HashColor((ushort)(cid * 17));
                ApplyOverlay(ref c, flags[idx], ov);
                pixels[idx] = c;
            }
        }

        static void ApplyOverlay(ref Color32 baseCol, ushort f, OverlayEnable ov)
        {
            if (!ov.Any)
            {
                return;
            }

            if (!TryGetWinningOverlayColor(f, ov, out Color32 oc))
            {
                return;
            }

            baseCol = Blend(baseCol, oc, OverlayAlpha);
        }

        static bool TryGetWinningOverlayColor(ushort f, OverlayEnable ov, out Color32 color)
        {
            color = default;
            if (ov.roadRail && MatchesRoadRail(f))
            {
                color = OvRoadRail;
                return true;
            }

            if (ov.unmarked && MatchesUnmarked(f))
            {
                color = OvUnmarked;
                return true;
            }

            if (ov.crosswalk && MatchesCrosswalk(f))
            {
                color = OvCrosswalk;
                return true;
            }

            if (ov.gatePassage && MatchesGatePassage(f))
            {
                color = OvGatePassage;
                return true;
            }

            if (ov.bridge && MatchesBridge(f))
            {
                color = OvBridge;
                return true;
            }

            return false;
        }

        static List<string> GetMatchingOverlayNames(ushort f)
        {
            var list = new List<string>(5);
            if (MatchesRoadRail(f))
            {
                list.Add("Road|Rail");
            }

            if (MatchesUnmarked(f))
            {
                list.Add("Unmarked");
            }

            if (MatchesCrosswalk(f))
            {
                list.Add("Crosswalk");
            }

            if (MatchesGatePassage(f))
            {
                list.Add("Gate|Passage");
            }

            if (MatchesBridge(f))
            {
                list.Add("Bridge");
            }

            return list;
        }

        static bool MatchesRoadRail(ushort f) =>
            (f & (ushort)CellFlags.Road) != 0 && (f & (ushort)CellFlags.Rail) != 0;

        static bool MatchesUnmarked(ushort f)
        {
            if ((f & (ushort)CellFlags.Road) == 0 || (f & (ushort)CellFlags.Crosswalk) != 0)
            {
                return false;
            }

            ushort sw = (ushort)(CellFlags.Sidewalk | CellFlags.SidewalkGenerated);
            return (f & sw) != 0;
        }

        static bool MatchesCrosswalk(ushort f) => (f & (ushort)CellFlags.Crosswalk) != 0;

        static bool MatchesGatePassage(ushort f) =>
            (f & (ushort)(CellFlags.Gate | CellFlags.BuildingPassage)) != 0;

        static bool MatchesBridge(ushort f) => (f & (ushort)CellFlags.Bridge) != 0;

        static Color32 Blend(Color32 a, Color32 b, float t)
        {
            float u = 1f - t;
            return new Color32(
                (byte)(a.r * u + b.r * t),
                (byte)(a.g * u + b.g * t),
                (byte)(a.b * u + b.b * t),
                255);
        }

        void ExportCurrentFullPng()
        {
            string name = _layer.ToString().ToLowerInvariant();
            string path = EditorUtility.SaveFilePanel("Export layer PNG", ProjectReportsDir(), name, "png");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllBytes(path, _tex.EncodeToPNG());
            Debug.Log("[City] Layers Viewer exported " + path);
        }

        void ExportParametricCrop()
        {
            int ix = (int)Math.Round(_cropCenterX);
            int iz = (int)Math.Round(_cropCenterZ);
            int isize = Math.Max(1, (int)Math.Round(_cropSizeM));
            string name = FormattableString.Invariant($"crop_x{ix}_z{iz}_{isize}m_{_cropPxPerCell}px.png");
            string path = Path.Combine(ProjectReportsDir(), name);
            WriteCropPng(path, _cropCenterX, _cropCenterZ, _cropSizeM, _cropPxPerCell);
            Debug.Log("[City] Exported crop " + path);
        }

        void ExportNamedCrop(string fileName, double centerX, double centerZ)
        {
            string path = Path.Combine(ProjectReportsDir(), fileName);
            WriteCropPng(path, centerX, centerZ, _cropSizeM, _cropPxPerCell);
            Debug.Log("[City] Preset crop " + path);
        }

        void WriteCropPng(string path, double centerX, double centerZ, float sizeM, int pxPerCell)
        {
            if (_bake == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            float cell = _bake.cell;
            int cells = Math.Max(1, (int)Math.Round(sizeM / cell));
            double half = sizeM * 0.5;
            var origin = new Vec2d(centerX - half, centerZ - half);
            _bake.Grid.WorldToCell(origin, out int i0, out int j0);
            i0 = Mathf.Clamp(i0, 0, Math.Max(0, _bake.w - cells));
            j0 = Mathf.Clamp(j0, 0, Math.Max(0, _bake.h - cells));
            int cw = Math.Min(cells, _bake.w - i0);
            int ch = Math.Min(cells, _bake.h - j0);
            pxPerCell = Math.Max(pxPerCell, (400 + cw - 1) / Math.Max(1, cw));

            ushort[] flags = _bake.GetFlagsArray();
            OverlayEnable ov = OverlayToggles;
            var tex = new Texture2D(cw * pxPerCell, ch * pxPerCell, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            for (int j = 0; j < ch; j++)
            for (int i = 0; i < cw; i++)
            {
                Color32 c = SampleLayerColor(i0 + i, j0 + j, flags, ov);
                for (int dy = 0; dy < pxPerCell; dy++)
                for (int dx = 0; dx < pxPerCell; dx++)
                {
                    tex.SetPixel(i * pxPerCell + dx, j * pxPerCell + dy, c);
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
        }

        Color32 SampleLayerColor(int i, int j, ushort[] flags, OverlayEnable ov)
        {
            int idx = i + j * _bake.w;
            ushort f = flags[idx];
            Color32 c;
            switch (_layer)
            {
                case LayerKind.Flags:
                    c = ExportLayersPngMenu_ColorForFlags(f);
                    break;
                case LayerKind.BuildingId:
                {
                    ushort id = _bake.GetBuildingId(i, j);
                    c = id == 0 ? Hex("#101010") : HashColor(id);
                    break;
                }
                case LayerKind.ComponentsPedestrian:
                    EnsurePed();
                    c = ComponentColor(_pedReport, idx);
                    break;
                case LayerKind.ComponentsZombie:
                    EnsureZom();
                    c = ComponentColor(_zomReport, idx);
                    break;
                default:
                    c = _bake.GetAccess(i, j) switch
                    {
                        CityAccess.Roadway => AccessRoadway,
                        CityAccess.Pedestrian => AccessPedestrian,
                        _ => AccessObstacle
                    };
                    break;
            }

            ApplyOverlay(ref c, f, ov);
            return c;
        }

        Color32 ComponentColor(AccessConnectivity.Report report, int idx)
        {
            int cid = report.cellComponentId[idx];
            return cid == 0 ? AccessObstacle : HashColor((ushort)(cid * 17));
        }

        bool TryGetPankeCenter(out double x, out double z)
        {
            x = z = 0;
            if (!HasModel)
            {
                return false;
            }

            Waterway[] wws = _cityModel.model.waterways;
            if (wws == null)
            {
                return false;
            }

            for (int i = 0; i < wws.Length; i++)
            {
                Waterway w = wws[i];
                if (w == null || w.osmId != PankeWaterwayOsmId || w.points == null || w.points.Length == 0)
                {
                    continue;
                }

                double sx = 0, sz = 0;
                for (int p = 0; p < w.points.Length; p++)
                {
                    sx += w.points[p].x;
                    sz += w.points[p].z;
                }

                x = sx / w.points.Length;
                z = sz / w.points.Length;
                return true;
            }

            return false;
        }

        static void BuildingCenterWorld(CityBakeAsset bake, long osmId, out double x, out double z)
        {
            var ij = BuildingCenterCell(bake, osmId);
            Vec2d c = bake.Grid.CellCenter(ij.i, ij.j);
            x = c.x;
            z = c.z;
        }

        static (int i, int j) BuildingCenterCell(CityBakeAsset bake, long osmId)
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

        static string ProjectReportsDir()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportsImgDir));
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

        void DestroyTex()
        {
            if (_tex != null)
            {
                DestroyImmediate(_tex);
                _tex = null;
            }
        }

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

        /// <summary>Batch export for REPORT-003 (без оверлеев).</summary>
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

            const int cropCells = 100;
            const int px = 4;
            WriteLegacyCrop(bake, Path.Combine(outDir, "crop_panke_osloer.png"), WorldToCell(bake, 68.59, 54.17), cropCells, px);
            WriteLegacyCrop(bake, Path.Combine(outDir, "crop_prinzenallee_88_49.png"), WorldToCell(bake, 88, 49), cropCells, px);
            WriteLegacyCrop(bake, Path.Combine(outDir, "crop_courtyard_3583425.png"),
                BuildingCenterCell(bake, CourtyardBuildingOsmId), cropCells, px);
        }

        static (int i, int j) WorldToCell(CityBakeAsset bake, double x, double z)
        {
            bake.Grid.WorldToCell(new Vec2d(x, z), out int i, out int j);
            return (i, j);
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
                {
                    tex.SetPixel(i * scale + dx, j * scale + dy, c);
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
        }

        static void WriteLegacyCrop(CityBakeAsset bake, string path, (int i, int j) center, int cells, int pxPerCell)
        {
            int half = cells / 2;
            int i0 = Mathf.Clamp(center.i - half, 0, bake.w - cells);
            int j0 = Mathf.Clamp(center.j - half, 0, bake.h - cells);
            int cw = Math.Min(cells, bake.w - i0);
            int ch = Math.Min(cells, bake.h - j0);
            pxPerCell = Math.Max(pxPerCell, (400 + cw - 1) / Math.Max(1, cw));
            var tex = new Texture2D(cw * pxPerCell, ch * pxPerCell, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

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
                {
                    tex.SetPixel(i * pxPerCell + dx, j * pxPerCell + dy, c);
                }
            }

            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);
        }

        /// <summary>Четыре пресета 5B в DOC/reports/img (оверлеи по умолчанию: Crosswalk).</summary>
        public static string ExportTaskPresets(CityBakeAsset bake, CityModelAsset model, long crossingOsmId = 0)
        {
            var window = CreateInstance<LayersViewerWindow>();
            window._bake = bake;
            window._cityModel = model;
            window._layer = LayerKind.Access;
            window._ovCrosswalk = true;
            window._ovUnmarked = false;
            window._ovRoadRail = false;
            window._ovBridge = false;
            window._ovGatePassage = false;
            window._cropSizeM = 50f;
            window._cropPxPerCell = 4;
            window.ReloadCrossings();

            var sb = new StringBuilder();
            if (window.HasModel && window.TryGetPankeCenter(out double px, out double pz))
            {
                window.ExportNamedCrop("crop_panke_osloer.png", px, pz);
                sb.AppendLine("crop_panke_osloer.png");
            }

            window.ExportNamedCrop("crop_prinzenallee_88_49.png", 88, 49);
            sb.AppendLine("crop_prinzenallee_88_49.png");
            BuildingCenterWorld(bake, CourtyardBuildingOsmId, out double bx, out double bz);
            window.ExportNamedCrop("crop_courtyard_3583425.png", bx, bz);
            sb.AppendLine("crop_courtyard_3583425.png");

            if (window._crossings.Length > 0)
            {
                CrossingEntry pick = window._crossings[0];
                if (crossingOsmId != 0)
                {
                    for (int i = 0; i < window._crossings.Length; i++)
                    {
                        if (window._crossings[i].osmId == crossingOsmId)
                        {
                            pick = window._crossings[i];
                            break;
                        }
                    }
                }

                string fn = FormattableString.Invariant($"crop_crossing_{pick.osmId}.png");
                window.ExportNamedCrop(fn, pick.midX, pick.midZ);
                sb.AppendLine(fn);
            }

            DestroyImmediate(window);
            return sb.ToString();
        }
    }
}