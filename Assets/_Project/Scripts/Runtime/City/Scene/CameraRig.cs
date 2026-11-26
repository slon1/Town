using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Zenject;
using ZombieCity.City;
using ZombieCity.City.Core;

namespace ZombieCity.City.Scene
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour, ICameraRig
    {
        const float PortraitAspect = 1080f / 2160f;
        static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

        Camera _camera;
        ICityMap _map;
        CameraRigSettings _settings;
        Vector2 _focusXZ;
        float _startOrthoSize;
        float _maxOrthoSize;
        float _boundsMinX, _boundsMaxX, _boundsMinZ, _boundsMaxZ;
        bool _mousePanActive;
        Vector2 _lastPanScreen;
        bool _pinchActive;
        float _lastPinchDistance;
        int _touchPanId = -1;
        Vector2 _lastTouchPanScreen;

        [Inject]
        public void Construct(ICityMap map, CameraRigSettings settings)
        {
            _map = map;
            _settings = settings;
        }

        void Awake() => _camera = GetComponent<Camera>();

        void Start()
        {
            if (_map == null || _settings == null) return;
            CacheMapBounds();
            ConfigureCamera();
            ResetView();
        }

        public CameraRigSettings ActiveSettings => _settings;

        public void SetViewFrame(float focusWorldX, float focusWorldZ, float orthographicSize)
        {
            if (_settings == null) return;
            _focusXZ.x = focusWorldX;
            _focusXZ.y = focusWorldZ;
            _camera.orthographicSize = Mathf.Clamp(orthographicSize, _settings.minOrthoSize, _maxOrthoSize > 0.01f ? _maxOrthoSize : orthographicSize);
            ClampFocusToBounds();
            ApplyTransform();
        }

        public void ResetView()
        {
            if (_map == null || _settings == null) return;
            var grid = _map.Grid;
            float mapW = grid.w * grid.cell;
            float mapH = grid.h * grid.cell;
            _focusXZ.x = (float)(grid.origin.x + mapW * 0.5);
            _focusXZ.y = (float)(grid.origin.z + mapH * 0.5);
            float aspect = _camera.aspect > 0.01f ? _camera.aspect : PortraitAspect;
            _startOrthoSize = mapW / (2f * aspect);
            float fitHeight = mapH * 0.5f;
            _maxOrthoSize = _startOrthoSize > fitHeight ? _startOrthoSize : fitHeight;
            _camera.orthographicSize = _startOrthoSize;
            ApplyTransform();
            ClampFocusToBounds();
            ApplyTransform();
        }

        void Update()
        {
            if (_map == null || _settings == null) return;
            HandleMousePanZoom();
            HandleTouch();
            ClampFocusToBounds();
            ApplyTransform();
        }

        void CacheMapBounds()
        {
            var grid = _map.Grid;
            float mapW = grid.w * grid.cell;
            float mapH = grid.h * grid.cell;
            float m = _settings.panMargin;
            _boundsMinX = (float)grid.origin.x - m;
            _boundsMaxX = (float)(grid.origin.x + mapW) + m;
            _boundsMinZ = (float)grid.origin.z - m;
            _boundsMaxZ = (float)(grid.origin.z + mapH) + m;
        }

        void ConfigureCamera()
        {
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(16f / 255f, 16f / 255f, 16f / 255f, 1f);
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 2000f;
        }

        void ApplyTransform()
        {
            float pitch = Mathf.Clamp(_settings.pitchDegrees, 45f, 90f);
            var rot = Quaternion.Euler(pitch, 0f, 0f);
            transform.rotation = rot;
            var forward = rot * Vector3.forward;
            var focus = new Vector3(_focusXZ.x, 0f, _focusXZ.y);
            transform.position = focus - forward * _settings.cameraDistance;
        }

        void HandleMousePanZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (IsPointerBlocked(mouse)) { _mousePanActive = false; return; }
            var pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) { _mousePanActive = true; _lastPanScreen = pos; }
            else if (mouse.leftButton.wasReleasedThisFrame) _mousePanActive = false;
            if (_mousePanActive && mouse.leftButton.isPressed) { PanScreen(_lastPanScreen, pos); _lastPanScreen = pos; }
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f) ZoomByFactor(1f - scroll * _settings.zoomSpeedWheel * 0.01f);
        }

        void HandleTouch()
        {
            var ts = Touchscreen.current;
            if (ts == null) return;
            var touches = ts.touches;
            int active = 0;
            TouchControl f0 = default, f1 = default;
            for (int i = 0; i < touches.Count; i++)
            {
                var t = touches[i];
                if (!t.press.isPressed) continue;
                if (IsPointerBlocked(t.touchId.ReadValue())) continue;
                if (active == 0) f0 = t; else if (active == 1) f1 = t;
                active++;
            }
            if (active >= 2)
            {
                _touchPanId = -1;
                var p0 = f0.position.ReadValue();
                var p1 = f1.position.ReadValue();
                float dist = Vector2.Distance(p0, p1);
                if (!_pinchActive) { _pinchActive = true; _lastPinchDistance = dist; }
                else if (_lastPinchDistance > 1f)
                {
                    float scale = dist / _lastPinchDistance;
                    ZoomByFactor(1f + (1f - scale) * _settings.pinchSensitivity);
                    _lastPinchDistance = dist;
                }
                _mousePanActive = false;
                return;
            }
            _pinchActive = false;
            if (active == 1)
            {
                var t = f0;
                int id = t.touchId.ReadValue();
                var pos = t.position.ReadValue();
                if (_touchPanId != id) { _touchPanId = id; _lastTouchPanScreen = pos; }
                else { PanScreen(_lastTouchPanScreen, pos); _lastTouchPanScreen = pos; }
            }
            else _touchPanId = -1;
        }

        void PanScreen(Vector2 from, Vector2 to)
        {
            if ((to - from).sqrMagnitude < 0.01f) return;
            if (!TryGroundHit(from, out var a) || !TryGroundHit(to, out var b)) return;
            _focusXZ.x -= (b.x - a.x) * _settings.panSensitivity;
            _focusXZ.y -= (b.z - a.z) * _settings.panSensitivity;
        }

        void ZoomByFactor(float factor)
        {
            if (factor <= 0f) return;
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * factor, _settings.minOrthoSize, _maxOrthoSize);
        }

        void ClampFocusToBounds()
        {
            if (!TryGetVisibleGroundAabb(out float vMinX, out float vMaxX, out float vMinZ, out float vMaxZ)) return;
            float viewW = vMaxX - vMinX, viewH = vMaxZ - vMinZ;
            float allowW = _boundsMaxX - _boundsMinX, allowH = _boundsMaxZ - _boundsMinZ;
            float fx = _focusXZ.x, fz = _focusXZ.y;
            if (viewW >= allowW) fx = (_boundsMinX + _boundsMaxX) * 0.5f;
            else
            {
                float half = viewW * 0.5f;
                fx = Mathf.Clamp(fx, _boundsMinX + half, _boundsMaxX - half);
            }
            if (viewH >= allowH) fz = (_boundsMinZ + _boundsMaxZ) * 0.5f;
            else
            {
                float half = viewH * 0.5f;
                fz = Mathf.Clamp(fz, _boundsMinZ + half, _boundsMaxZ - half);
            }
            _focusXZ.x = fx;
            _focusXZ.y = fz;
        }

        bool TryGetVisibleGroundAabb(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = float.PositiveInfinity; maxX = float.NegativeInfinity;
            minZ = float.PositiveInfinity; maxZ = float.NegativeInfinity;
            if (!Corner(0, 0, ref minX, ref maxX, ref minZ, ref maxZ)) return false;
            Corner(1, 0, ref minX, ref maxX, ref minZ, ref maxZ);
            Corner(1, 1, ref minX, ref maxX, ref minZ, ref maxZ);
            Corner(0, 1, ref minX, ref maxX, ref minZ, ref maxZ);
            return true;
        }

        bool Corner(float vx, float vy, ref float minX, ref float maxX, ref float minZ, ref float maxZ)
        {
            var screen = _camera.ViewportToScreenPoint(new Vector3(vx, vy, 0f));
            if (!TryGroundHit(screen, out var hit)) return false;
            if (hit.x < minX) minX = hit.x;
            if (hit.x > maxX) maxX = hit.x;
            if (hit.z < minZ) minZ = hit.z;
            if (hit.z > maxZ) maxZ = hit.z;
            return true;
        }

        bool TryGroundHit(Vector2 screen, out Vector3 world)
        {
            var ray = _camera.ScreenPointToRay(screen);
            if (!GroundPlane.Raycast(ray, out float enter)) { world = default; return false; }
            world = ray.GetPoint(enter);
            return true;
        }

        static bool IsPointerBlocked(Mouse mouse) =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        static bool IsPointerBlocked(int touchId) =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);
    }
}
