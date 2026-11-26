#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using ZombieCity.City.Core;
using ZombieCity.City.Scene;
using ZombieCity.City.UI;

namespace ZombieCity.City.EditorTools
{
  /// <summary>Game View captures for REPORT-007 (TASK-007/7C).</summary>
  public static class CityReport007ScreenshotMenu
  {
    const string ScenePath = "Assets/_Project/Scenes/City_Prototype.unity";
    const string OutputDir = "DOC/reports/img";
    const string SettingsPath = "Assets/_Project/City/Scene/CameraRigSettings_Default.asset";

    static bool _pending;
    static int _step;
    static int _frameCounter;
    static GroundView _ground;
    static CityMapLayerPanel _panel;
    static CameraRigSettings _settings;

    /// <summary>MCP helper: state 0=access+crosswalk, 1=collapsed, 2=flags, 3=buildingId, 4=pitch90, 5=pitch60.</summary>
    public static void ApplyReport007State(int state)
    {
      _ground = Object.FindAnyObjectByType<GroundView>();
      _panel = Object.FindAnyObjectByType<CityMapLayerPanel>();
      _settings = AssetDatabase.LoadAssetAtPath<CameraRigSettings>(SettingsPath);
      if (_ground == null || _panel == null)
      {
        Debug.LogError("[City] ApplyReport007State: scene not ready.");
        return;
      }

      SetExpanded(_panel, state != 1);
      switch (state)
      {
        case 0:
          _ground.SetLayer(CityLayer.Access);
          _ground.SetOverlay(CityOverlay.Crosswalk, true);
          break;
        case 1:
          _ground.SetLayer(CityLayer.Access);
          _ground.SetOverlay(CityOverlay.Crosswalk, true);
          break;
        case 2:
          _ground.SetLayer(CityLayer.Flags);
          _ground.SetOverlay(CityOverlay.Crosswalk, false);
          break;
        case 3:
          _ground.SetLayer(CityLayer.BuildingId);
          _ground.SetOverlay(CityOverlay.Crosswalk, false);
          break;
        case 4:
          if (_settings != null) _settings.pitchDegrees = 90f;
          _ground.SetLayer(CityLayer.Access);
          break;
        case 5:
          if (_settings != null) _settings.pitchDegrees = 60f;
          _ground.SetLayer(CityLayer.Access);
          break;
      }
    }

    [MenuItem("City/Capture REPORT-007 Screenshots")]
    public static void StartCapture()
    {
      string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir));
      if (!Directory.Exists(outDir))
      {
        Directory.CreateDirectory(outDir);
      }

      if (!EditorApplication.isPlaying)
      {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        _pending = true;
        _step = 0;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.isPlaying = true;
        return;
      }

      BeginSequence();
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
      if (!_pending || change != PlayModeStateChange.EnteredPlayMode)
      {
        return;
      }

      _pending = false;
      EditorApplication.playModeStateChanged -= OnPlayMode;
      BeginSequence();
    }

    static void BeginSequence()
    {
      _step = 0;
      _frameCounter = 0;
      _ground = Object.FindAnyObjectByType<GroundView>();
      _panel = Object.FindAnyObjectByType<CityMapLayerPanel>();
      _settings = AssetDatabase.LoadAssetAtPath<CameraRigSettings>(SettingsPath);

      if (_ground == null || _panel == null)
      {
        Debug.LogError("[City] REPORT-007 capture: missing GroundView or panel.");
        EditorApplication.isPlaying = false;
        return;
      }

      EditorApplication.update += Tick;
    }

    static void Tick()
    {
      if (!Application.isPlaying)
      {
        EditorApplication.update -= Tick;
        return;
      }

      _frameCounter++;
      if (_frameCounter < 30)
      {
        return;
      }

      switch (_step)
      {
        case 0:
          SetExpanded(_panel, true);
          _ground.SetLayer(CityLayer.Access);
          _ground.SetOverlay(CityOverlay.Crosswalk, true);
          _frameCounter = 0;
          _step = 1;
          break;
        case 1:
          if (_frameCounter < 15) return;
          SaveCapture("task007_panel_access_crosswalk.png");
          _step = 2;
          _frameCounter = 0;
          break;
        case 2:
          SetExpanded(_panel, false);
          _frameCounter = 0;
          _step = 3;
          break;
        case 3:
          if (_frameCounter < 10) return;
          SaveCapture("task007_panel_collapsed.png");
          SetExpanded(_panel, true);
          _ground.SetLayer(CityLayer.Flags);
          _ground.SetOverlay(CityOverlay.Crosswalk, false);
          _frameCounter = 0;
          _step = 4;
          break;
        case 4:
          if (_frameCounter < 15) return;
          SaveCapture("task007_layer_flags.png");
          _ground.SetLayer(CityLayer.BuildingId);
          _frameCounter = 0;
          _step = 5;
          break;
        case 5:
          if (_frameCounter < 15) return;
          SaveCapture("task007_layer_building_id.png");
          if (_settings != null) _settings.pitchDegrees = 90f;
          _frameCounter = 0;
          _step = 6;
          break;
        case 6:
          if (_frameCounter < 15) return;
          SaveCapture("task007_camera_pitch_90.png");
          if (_settings != null) _settings.pitchDegrees = 60f;
          _frameCounter = 0;
          _step = 7;
          break;
        case 7:
          if (_frameCounter < 15) return;
          SaveCapture("task007_camera_pitch_60.png");
          if (_settings != null) _settings.pitchDegrees = 90f;
          Debug.Log("[City] REPORT-007 screenshots done.");
          EditorApplication.update -= Tick;
          EditorApplication.isPlaying = false;
          break;
      }
    }

    static void SetExpanded(CityMapLayerPanel panel, bool expanded)
    {
      var so = new SerializedObject(panel);
      var expandedContent = so.FindProperty("expandedContent").objectReferenceValue as GameObject;
      var collapsedChip = so.FindProperty("collapsedChip").objectReferenceValue as GameObject;
      if (expandedContent != null) expandedContent.SetActive(expanded);
      if (collapsedChip != null) collapsedChip.SetActive(!expanded);
    }

    static void SaveCapture(string fileName)
    {
      string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir, fileName));
      ScreenCapture.CaptureScreenshot(path, 1);
      Debug.Log("[City] Screenshot: " + path);
    }
  }
}
#endif