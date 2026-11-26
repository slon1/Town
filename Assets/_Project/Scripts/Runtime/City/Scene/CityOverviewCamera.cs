using UnityEngine;
using Zenject;
using ZombieCity.City;

namespace ZombieCity.City.Scene
{
    /// <summary>
    /// Temporary top-down frame for the prototype scene. Gesture control is TASK-007; this is not <c>ICameraRig</c>.
    /// Fits the map width at 1080×2160 (aspect 0.5), pitch 90°, up = +Z.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CityOverviewCamera : MonoBehaviour
    {
        ICityMap _map;

        [Inject]
        public void Construct(ICityMap map)
        {
            _map = map;
        }

        void Start()
        {
            if (_map == null)
            {
                return;
            }

            var grid = _map.Grid;
            float mapW = grid.w * grid.cell;
            float mapH = grid.h * grid.cell;
            const float aspect = 1080f / 2160f;
            Camera cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = mapW / (2f * aspect);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(16f / 255f, 16f / 255f, 16f / 255f, 1f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2000f;
            float cx = (float)(grid.origin.x + mapW * 0.5);
            float cz = (float)(grid.origin.z + mapH * 0.5);
            transform.SetPositionAndRotation(new Vector3(cx, 400f, cz), Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
