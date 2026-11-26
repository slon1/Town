using System;

namespace ZombieCity.City.Scene
{
    /// <summary>Show or hide the building mesh so BuildingId on the ground stays visible (ADR-004 §6).</summary>
    public interface IBuildingsView
    {
        bool Visible { get; set; }
        event Action Changed;
    }
}
