using UnityEngine;

namespace ZombieCity.City.Scene
{
    [CreateAssetMenu(fileName = "CameraRigSettings", menuName = "City/Camera Rig Settings")]
    public sealed class CameraRigSettings : ScriptableObject
    {
        [Range(45f, 90f)] public float pitchDegrees = 90f;
        public float minOrthoSize = 15f;
        public float panMargin = 20f;
        public float zoomSpeedWheel = 0.1f;
        public float panSensitivity = 1f;
        public float pinchSensitivity = 1f;
        public float cameraDistance = 300f;
    }
}
