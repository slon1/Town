using UnityEngine;

namespace ZombieCity.City.Scene
{
    [CreateAssetMenu(fileName = "CameraRigSettings", menuName = "City/Camera Rig Settings")]
    public sealed class CameraRigSettings : ScriptableObject
    {
        [Range(45f, 90f)] public float pitchDegrees = 90f;
        public float minOrthoSize = 15f;
        public float panMargin = 20f;
        [Tooltip("Exponent per wheel notch; ortho *= Exp(-zoomPerNotch * notches).")]
        public float zoomPerNotch = 0.15f;
        [Tooltip("Typical delta per wheel detent (e.g. 120 on Windows). Trackpad uses 1.")]
        public float scrollNotchUnit = 120f;
        public float panSensitivity = 1f;
        public float pinchSensitivity = 1f;
        public float cameraDistance = 300f;
    }
}