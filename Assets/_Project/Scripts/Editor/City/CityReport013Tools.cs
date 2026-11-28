#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZombieCity.City.Scene;

namespace ZombieCity.City.EditorTools
{
  public static class CityReport013Tools
  {
    const string ScenePath = "Assets/_Project/Scenes/City_Prototype.unity";
    const string OutputDir = "DOC/reports/img";
    const float RefAspect = 1080f / 2160f;
    const float FocusX = 88f;
    const float FocusZ = 49f;
    const float FocusOrtho = 40f;

    [MenuItem("City/Log Wheel Notch Table (TASK-013)")]
    public static void LogNotchTable()
    {
      var rig = Object.FindAnyObjectByType<CameraRig>();
      if (rig == null || !Application.isPlaying)
      {
        Debug.LogWarning("[City] Play City_Prototype first.");
        return;
      }

      var screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
      rig.LogWheelNotchTable(screen);
    }

    [MenuItem("City/Capture TASK-013 Zoom To Pointer (before/after)")]
    public static void CaptureZoomBeforeAfter()
    {
      if (!Application.isPlaying)
      {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
        EditorApplication.delayCall += () => EditorApplication.delayCall += CaptureZoomBeforeAfterPlay;
        return;
      }

      CaptureZoomBeforeAfterPlay();
    }

    static void CaptureZoomBeforeAfterPlay()
    {
      var rig = Object.FindAnyObjectByType<CameraRig>();
      if (rig == null)
      {
        Debug.LogError("[City] TASK-013 capture: no CameraRig.");
        return;
      }

      EnsureOutputDir();
      var cam = rig.GetComponent<Camera>();
      if (cam != null) cam.aspect = RefAspect;

      rig.SetViewFrame(FocusX, FocusZ, FocusOrtho, RefAspect);
      var screen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
      rig.LogCaptureState("task013_zoom_before");
      CapturePng("task013_zoom_to_pointer_before.png");
      rig.ApplyZoomNotches(screen, 4f);
      rig.LogCaptureState("task013_zoom_after");
      CapturePng("task013_zoom_to_pointer_after.png");
      Debug.Log("[City] TASK-013 zoom before/after saved to " + OutputDir);
    }

    static void EnsureOutputDir()
    {
      var full = Path.Combine(Application.dataPath, "..", OutputDir);
      if (!Directory.Exists(full)) Directory.CreateDirectory(full);
    }

    static void CapturePng(string fileName)
    {
      var path = Path.Combine(Application.dataPath, "..", OutputDir, fileName);
      ScreenCapture.CaptureScreenshot(path);
      AssetDatabase.Refresh();
    }
  }
}
#endif