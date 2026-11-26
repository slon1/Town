using System;
using ZombieCity.City.Core;

namespace ZombieCity.City.Scene
{
    /// <summary>Ground layer switch (ADR-004 §6). Camera rig is TASK-007.</summary>
    public interface ILayerView
    {
        CityLayer Layer { get; }
        void SetLayer(CityLayer layer);
        void SetOverlay(CityOverlay overlay, bool enabled);
        bool GetOverlay(CityOverlay overlay);
        event Action Changed;
    }
}
