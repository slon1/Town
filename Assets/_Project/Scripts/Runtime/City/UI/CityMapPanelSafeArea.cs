using UnityEngine;

namespace ZombieCity.City.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class CityMapPanelSafeArea : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastSafe;
        Vector2Int _lastSize;
        void Awake() => _rect = (RectTransform)transform;
        void OnEnable() => Apply();
        void Update() { var safe = Screen.safeArea; var size = new Vector2Int(Screen.width, Screen.height); if (safe != _lastSafe || size != _lastSize) Apply(); }
        void Apply() { _lastSafe = Screen.safeArea; _lastSize = new Vector2Int(Screen.width, Screen.height); _rect.anchoredPosition = new Vector2(_lastSafe.xMin + 8f, -(Screen.height - _lastSafe.yMax) - 8f); }
    }
}
