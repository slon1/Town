namespace ZombieCity.City.Core
{
    /// <summary>
    /// Ground texture pixels, RGBA32 little-endian, index i + j·w, row 0 = minimum j (ADR-004 §3, ADR-003 §5.1).
    /// One overlay wins per cell; overlays are not blended with each other. Alpha against the base is 0.7.
    /// </summary>
    public static class LayerComposer
    {
        public const float OverlayAlpha = 0.7f;

        const uint AccessObstacle = 0xFF202020u;
        const uint AccessRoadway = 0xFF4040D0u;
        const uint AccessPedestrian = 0xFF80C040u;

        const uint Empty = 0xFF101010u;
        const uint Building = 0xFF3A3A3Au;
        const uint Water = 0xFFFF7D2Eu;
        const uint Barrier = 0xFF007AFFu;
        const uint Gate = 0xFF00E0FFu;
        const uint Rail = 0xFFFF4D9Au;
        const uint Crosswalk = 0xFFFFFFFFu;
        const uint Sidewalk = 0xFFC8C8C8u;
        const uint Road = 0xFF5A5A5Au;
        const uint Green = 0xFF4DA33Fu;
        const uint BridgeTint = 0xFFC1D100u;

        const uint OvRail = 0xFF0000FFu;
        const uint OvUnmarked = 0xFFFF00FFu;
        const uint OvCrosswalk = 0xFF00E0FFu;
        const uint OvGate = 0xFFFFFFFFu;
        const uint OvBridge = 0xFFC1D100u;

        public static void Compose(
            int w,
            int h,
            ushort[] flags,
            ushort[] buildingIds,
            byte[] access,
            CityLayer layer,
            CityOverlay overlays,
            uint[] destination)
        {
            int count = w * h;
            for (int index = 0; index < count; index++)
            {
                ushort f = flags != null && index < flags.Length ? flags[index] : (ushort)0;
                uint color;
                if (layer == CityLayer.Flags)
                {
                    color = ColorForFlags(f);
                }
                else if (layer == CityLayer.BuildingId)
                {
                    ushort id = buildingIds != null && index < buildingIds.Length ? buildingIds[index] : (ushort)0;
                    color = id == 0 ? Empty : HashId(id);
                }
                else
                {
                    byte a = access != null && index < access.Length ? access[index] : (byte)0;
                    color = a == (byte)CityAccess.Roadway
                        ? AccessRoadway
                        : a == (byte)CityAccess.Pedestrian ? AccessPedestrian : AccessObstacle;
                }

                if (overlays != CityOverlay.None && TryWinningOverlay(f, overlays, out uint overlay))
                {
                    color = Blend(color, overlay, OverlayAlpha);
                }

                destination[index] = color;
            }
        }

        static uint ColorForFlags(ushort f)
        {
            uint color = Empty;
            if ((f & (ushort)CellFlags.Building) != 0)
            {
                color = Building;
            }
            else if ((f & (ushort)CellFlags.Water) != 0)
            {
                color = Water;
            }
            else if ((f & (ushort)CellFlags.Barrier) != 0)
            {
                color = Barrier;
            }
            else if ((f & (ushort)CellFlags.Gate) != 0)
            {
                color = Gate;
            }
            else if ((f & (ushort)CellFlags.Rail) != 0)
            {
                color = Rail;
            }
            else if ((f & (ushort)CellFlags.Crosswalk) != 0)
            {
                color = Crosswalk;
            }
            else if ((f & (ushort)(CellFlags.Sidewalk | CellFlags.Cycleway | CellFlags.Steps | CellFlags.SidewalkGenerated)) != 0)
            {
                color = Sidewalk;
            }
            else if ((f & (ushort)CellFlags.Road) != 0)
            {
                color = Road;
            }
            else if ((f & (ushort)CellFlags.Green) != 0)
            {
                color = Green;
            }

            if ((f & (ushort)CellFlags.Bridge) != 0)
            {
                color = Blend(color, BridgeTint, 0.45f);
            }

            return color;
        }

        static bool TryWinningOverlay(ushort f, CityOverlay enabled, out uint color)
        {
            if ((enabled & CityOverlay.RailOnRoad) != 0
                && (f & (ushort)CellFlags.Road) != 0
                && (f & (ushort)CellFlags.Rail) != 0)
            {
                color = OvRail;
                return true;
            }

            if ((enabled & CityOverlay.UnmarkedCrossing) != 0
                && (f & (ushort)CellFlags.Road) != 0
                && (f & (ushort)CellFlags.Crosswalk) == 0
                && (f & (ushort)(CellFlags.Sidewalk | CellFlags.SidewalkGenerated)) != 0)
            {
                color = OvUnmarked;
                return true;
            }

            if ((enabled & CityOverlay.Crosswalk) != 0 && (f & (ushort)CellFlags.Crosswalk) != 0)
            {
                color = OvCrosswalk;
                return true;
            }

            if ((enabled & CityOverlay.GatePassage) != 0
                && (f & (ushort)(CellFlags.Gate | CellFlags.BuildingPassage)) != 0)
            {
                color = OvGate;
                return true;
            }

            if ((enabled & CityOverlay.Bridge) != 0 && (f & (ushort)CellFlags.Bridge) != 0)
            {
                color = OvBridge;
                return true;
            }

            color = 0;
            return false;
        }

        static uint HashId(ushort id)
        {
            unchecked
            {
                uint x = id * 2654435761u;
                return Pack((byte)(40 + (x & 0x7F)), (byte)(40 + ((x >> 8) & 0x7F)), (byte)(40 + ((x >> 16) & 0x7F)));
            }
        }

        static uint Blend(uint a, uint b, float t)
        {
            float u = 1f - t;
            return Pack(
                (byte)(((byte)a) * u + ((byte)b) * t),
                (byte)(((byte)(a >> 8)) * u + ((byte)(b >> 8)) * t),
                (byte)(((byte)(a >> 16)) * u + ((byte)(b >> 16)) * t));
        }

        static uint Pack(byte r, byte g, byte b)
        {
            return (uint)(r | (g << 8) | (b << 16) | (255 << 24));
        }
    }
}
