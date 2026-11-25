using UnityEngine;
using ZombieCity.City.Core;

namespace ZombieCity.City
{
    /// <summary>
    /// Scene viewer for CityModelAsset. Data + layer toggles only (no Editor API).
    /// Drawing is done by Editor DrawGizmo drawer.
    /// </summary>
    public class CityModelGizmoViewer : MonoBehaviour
    {
        public CityModelAsset cityModel;

        public bool showRoads = true;
        public bool showWalkways = true;
        public bool showCrossings = true;
        public bool showBuildings = true;
        public bool showBarriers = true;
        public bool showWater = true;
        public bool showGreen = true;
        public bool showRailways = true;
        public bool showTunnels = true;
        public bool showBridges = true;

        public CityModel Model => cityModel != null ? cityModel.model : null;
    }
}
