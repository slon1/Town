using UnityEditor;
using UnityEngine;
using ZombieCity.City;
using ZombieCity.City.Core;

namespace ZombieCity.City.EditorTools
{
    /// <summary>
    /// Draws CityModelGizmoViewer in Scene view (TASK-001/1F palette).
    /// </summary>
    public static class CityModelGizmoDrawer
    {
        static readonly Color Road = Hex("4A4A4A");
        static readonly Color Walkway = Hex("C8C8C8");
        static readonly Color Crossing = Hex("FFFFFF");
        static readonly Color Building = Hex("E0A040");
        static readonly Color Barrier = Hex("FF7A00");
        static readonly Color Water = Hex("2E7DFF");
        static readonly Color Green = Hex("3FA34D");
        static readonly Color Railway = Hex("9A4DFF");
        static readonly Color Tunnel = Hex("FF2D55");
        static readonly Color Bridge = Hex("00D1C1");

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        static void Draw(CityModelGizmoViewer viewer, GizmoType gizmoType)
        {
            if (viewer == null)
            {
                return;
            }

            CityModel model = viewer.Model;
            if (model == null)
            {
                return;
            }

            if (viewer.showRoads && model.roads != null)
            {
                for (int i = 0; i < model.roads.Length; i++)
                {
                    Road r = model.roads[i];
                    if (r == null)
                    {
                        continue;
                    }

                    Color c = Road;
                    if (r.isTunnel && viewer.showTunnels)
                    {
                        c = Tunnel;
                    }
                    else if (r.isBridge && viewer.showBridges)
                    {
                        c = Bridge;
                    }

                    DrawPolyline(r.points, c);
                }
            }

            if (model.walkways != null)
            {
                for (int i = 0; i < model.walkways.Length; i++)
                {
                    Walkway w = model.walkways[i];
                    if (w == null)
                    {
                        continue;
                    }

                    bool isCrossing = w.kind == WalkwayKind.Crossing;
                    if (isCrossing && !viewer.showCrossings)
                    {
                        continue;
                    }

                    if (!isCrossing && !viewer.showWalkways)
                    {
                        continue;
                    }

                    Color c = isCrossing ? Crossing : Walkway;
                    if (w.isTunnel && viewer.showTunnels)
                    {
                        c = Tunnel;
                    }
                    else if (w.isBridge && viewer.showBridges)
                    {
                        c = Bridge;
                    }

                    DrawPolyline(w.points, c);
                }
            }

            if (viewer.showBuildings || viewer.showWater || viewer.showGreen)
            {
                DrawAreas(model.areas, viewer);
            }

            if (viewer.showBarriers && model.barriers != null)
            {
                for (int i = 0; i < model.barriers.Length; i++)
                {
                    Barrier b = model.barriers[i];
                    if (b != null)
                    {
                        DrawPolyline(b.points, Barrier);
                    }
                }
            }

            if (viewer.showRailways && model.railways != null)
            {
                for (int i = 0; i < model.railways.Length; i++)
                {
                    Railway r = model.railways[i];
                    if (r == null)
                    {
                        continue;
                    }

                    Color c = Railway;
                    if (r.isTunnel && viewer.showTunnels)
                    {
                        c = Tunnel;
                    }
                    else if (r.isBridge && viewer.showBridges)
                    {
                        c = Bridge;
                    }

                    DrawPolyline(r.points, c);
                }
            }

            if (viewer.showWater && model.waterways != null)
            {
                for (int i = 0; i < model.waterways.Length; i++)
                {
                    Waterway w = model.waterways[i];
                    if (w == null)
                    {
                        continue;
                    }

                    Color c = w.isTunnel && viewer.showTunnels ? Tunnel : Water;
                    DrawPolyline(w.points, c);
                }
            }
        }

        static void DrawAreas(Area[] areas, CityModelGizmoViewer viewer)
        {
            if (areas == null)
            {
                return;
            }

            for (int i = 0; i < areas.Length; i++)
            {
                Area a = areas[i];
                if (a == null || a.points == null || a.ringStart == null || a.ringStart.Length == 0)
                {
                    continue;
                }

                Color c;
                switch (a.kind)
                {
                    case AreaKind.Building:
                        if (!viewer.showBuildings) continue;
                        c = Building;
                        break;
                    case AreaKind.Water:
                        if (!viewer.showWater) continue;
                        c = Water;
                        break;
                    case AreaKind.Green:
                        if (!viewer.showGreen) continue;
                        c = Green;
                        break;
                    case AreaKind.PedestrianArea:
                        if (!viewer.showWalkways) continue;
                        c = Walkway;
                        break;
                    default:
                        if (!viewer.showBuildings) continue;
                        c = Building;
                        break;
                }

                for (int r = 0; r < a.ringStart.Length; r++)
                {
                    int start = a.ringStart[r];
                    int end = r + 1 < a.ringStart.Length ? a.ringStart[r + 1] : a.points.Length;
                    DrawRingClosed(a.points, start, end, c);
                }
            }
        }

        static void DrawPolyline(Vec2d[] points, Color color)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            Gizmos.color = color;
            for (int i = 1; i < points.Length; i++)
            {
                Gizmos.DrawLine(ToWorld(points[i - 1]), ToWorld(points[i]));
            }
        }

        static void DrawRingClosed(Vec2d[] points, int start, int end, Color color)
        {
            int count = end - start;
            if (points == null || count < 2)
            {
                return;
            }

            Gizmos.color = color;
            for (int i = start + 1; i < end; i++)
            {
                Gizmos.DrawLine(ToWorld(points[i - 1]), ToWorld(points[i]));
            }

            Gizmos.DrawLine(ToWorld(points[end - 1]), ToWorld(points[start]));
        }

        static Vector3 ToWorld(Vec2d v)
        {
            return new Vector3((float)v.x, 0f, (float)v.z);
        }

        static Color Hex(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
            {
                return c;
            }

            return Color.magenta;
        }
    }
}
