#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieCity.City.Core;
using ZombieCity.City.Scene;
using ZombieCity.City.UI;

namespace ZombieCity.City.EditorTools
{
  /// <summary>Game View captures for REPORT-007 / TASK-010.</summary>
  public static class CityReport007ScreenshotMenu
  {
    const string ScenePath = "Assets/_Project/Scenes/City_Prototype.unity";
    const string OutputDir = "DOC/reports/img";
    const float ZoomPitchCenterX = 88f;
    const float ZoomPitchCenterZ = 49f;
    const float ZoomOrthoSize = 25f;

    static bool _pendingPlay;
    static int _step;
    static int _frameCounter;
    static ILayerView _layers;
    static IBuildingsView _buildings;
    static CityMapLayerPanel _panel;
    static CameraRig _rig;
    static Image _resetImage;
    static Color _savedResetColor;
    static bool _diagnosisDone;

    [MenuItem("City/Capture REPORT-007 Screenshots (TASK-010)")]
    public static void StartCapture()
    {
      EnsureOutputDir();
      _diagnosisDone = false;
      if (!EditorApplication.isPlaying)
      {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        _pendingPlay = true;
        _step = 0;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.isPlaying = true;
        return;
      }

      _step = 0;
      BeginSequence();
    }

    [MenuItem("City/Capture panel_buildings_toggle.png")]
    public static void CaptureBuildingsToggleOnly()
    {
      EnsureOutputDir();
      if (!EditorApplication.isPlaying)
      {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
        EditorApplication.delayCall += () => EditorApplication.delayCall += CaptureBuildingsTogglePlay;
        return;
      }

      CaptureBuildingsTogglePlay();
    }

    static void OnPlayMode(PlayModeStateChange change)
    {
      if (!_pendingPlay || change != PlayModeStateChange.EnteredPlayMode) return;
      _pendingPlay = false;
      EditorApplication.playModeStateChanged -= OnPlayMode;
      BeginSequence();
    }

    static void BeginSequence()
    {
      _frameCounter = 0;
      if (!ResolveScene())
      {
        EditorApplication.isPlaying = false;
        return;
      }

      EditorApplication.update += Tick;
    }

    static bool ResolveScene()
    {
      _layers = UnityEngine.Object.FindAnyObjectByType<GroundView>();
      _buildings = UnityEngine.Object.FindAnyObjectByType<BuildingsView>();
      _panel = UnityEngine.Object.FindAnyObjectByType<CityMapLayerPanel>();
      _rig = UnityEngine.Object.FindAnyObjectByType<CameraRig>();
      if (_layers == null || _panel == null || _rig == null)
      {
        Debug.LogError("[City] Capture: missing GroundView, panel, or CameraRig.");
        return false;
      }

      return true;
    }

    static void Tick()
    {
      if (!Application.isPlaying)
      {
        EditorApplication.update -= Tick;
        return;
      }

      _frameCounter++;

      if (!_diagnosisDone)
      {
        if (_frameCounter < 40) return;
        RunDiagnosisShot();
        _diagnosisDone = true;
        _frameCounter = 0;
        return;
      }

      switch (_step)
      {
        case 0:
          if (_frameCounter < 25) return;
          SetExpanded(true);
          _layers.SetLayer(CityLayer.Access);
          _layers.SetOverlay(CityOverlay.Crosswalk, true);
          if (_buildings != null) _buildings.Visible = true;
          _rig.ResetView();
          SetPitch(90f);
          _frameCounter = 0;
          _step = 1;
          return;
        case 1:
          if (_frameCounter < 15) return;
          SaveCapture("task007_panel_access_crosswalk.png");
          SetExpanded(false);
          _frameCounter = 0;
          _step = 2;
          return;
        case 2:
          if (_frameCounter < 12) return;
          SaveCapture("task007_panel_collapsed.png");
          SetExpanded(true);
          _layers.SetLayer(CityLayer.Flags);
          _layers.SetOverlay(CityOverlay.Crosswalk, false);
          _frameCounter = 0;
          _step = 3;
          return;
        case 3:
          if (_frameCounter < 15) return;
          SaveCapture("task007_layer_flags.png");
          _layers.SetLayer(CityLayer.BuildingId);
          _layers.SetOverlay(CityOverlay.Crosswalk, false);
          if (_buildings != null) _buildings.Visible = true;
          _frameCounter = 0;
          _step = 4;
          return;
        case 4:
          if (_frameCounter < 15) return;
          SaveCapture("task007_layer_building_id.png");
          _layers.SetLayer(CityLayer.Flags);
          _layers.SetOverlay(CityOverlay.Crosswalk, true);
          if (_buildings != null) _buildings.Visible = false;
          _frameCounter = 0;
          _step = 5;
          return;
        case 5:
          if (_frameCounter < 15) return;
          SaveCapture("task007_panel_sync_flags.png");
          _layers.SetLayer(CityLayer.BuildingId);
          _layers.SetOverlay(CityOverlay.Crosswalk, false);
          if (_buildings != null) _buildings.Visible = false;
          _frameCounter = 0;
          _step = 6;
          return;
        case 6:
          if (_frameCounter < 15) return;
          SaveCapture("panel_buildings_toggle.png");
          _rig.ResetView();
          SetPitch(90f);
          _frameCounter = 0;
          _step = 7;
          return;
        case 7:
          if (_frameCounter < 18) return;
          SaveCapture("task007_camera_pitch_90.png");
          _rig.ResetView();
          SetPitch(60f);
          _frameCounter = 0;
          _step = 8;
          return;
        case 8:
          if (_frameCounter < 18) return;
          SaveCapture("task007_camera_pitch_60.png");
          SetPitch(60f);
          _rig.SetViewFrame(ZoomPitchCenterX, ZoomPitchCenterZ, ZoomOrthoSize);
          _frameCounter = 0;
          _step = 9;
          return;
        case 9:
          if (_frameCounter < 18) return;
          SaveCapture("task007_camera_pitch_60_zoom.png");
          SetPitch(90f);
          _rig.ResetView();
          LogPitchHashes();
          Debug.Log("[City] TASK-010 screenshot sequence done.");
          EditorApplication.update -= Tick;
          EditorApplication.isPlaying = false;
          break;
      }
    }

    static void RunDiagnosisShot()
    {
      SetExpanded(true);
      _layers.SetLayer(CityLayer.Access);
      _layers.SetOverlay(CityOverlay.Crosswalk, false);
      if (_buildings != null) _buildings.Visible = true;
      _rig.SetViewFrame(-72f, 58f, 38f);
      SetPitch(90f);
      CacheResetButton();
      if (_resetImage != null)
      {
        _savedResetColor = _resetImage.color;
        _resetImage.color = new Color(1f, 0.1f, 0.85f, 1f);
      }

      SaveCapture("task010_ui_diagnosis.png");
      if (_resetImage != null) _resetImage.color = _savedResetColor;
      Debug.Log("[City] UI diagnosis frame saved.");
    }

    static void CaptureBuildingsTogglePlay()
    {
      if (!ResolveScene()) return;
      SetExpanded(true);
      _layers.SetLayer(CityLayer.BuildingId);
      _layers.SetOverlay(CityOverlay.Crosswalk, false);
      if (_buildings != null) _buildings.Visible = false;
      SaveCapture("panel_buildings_toggle.png");
      Debug.Log("[City] panel_buildings_toggle.png saved.");
    }

    static void CacheResetButton()
    {
      _resetImage = null;
      if (_panel == null) return;
      var so = new SerializedObject(_panel);
      var btn = so.FindProperty("resetCameraButton").objectReferenceValue as Button;
      _resetImage = btn != null ? btn.targetGraphic as Image : null;
    }

    static void SetPitch(float degrees)
    {
      var settings = _rig != null ? _rig.ActiveSettings : null;
      if (settings != null) settings.pitchDegrees = degrees;
    }

    static void SetExpanded(bool expanded)
    {
      if (_panel == null) return;
      var so = new SerializedObject(_panel);
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

    static void LogPitchHashes()
    {
      string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir));
      LogSha12(Path.Combine(dir, "task007_camera_pitch_90.png"));
      LogSha12(Path.Combine(dir, "task007_camera_pitch_60.png"));
      if (_rig != null)
      {
        Debug.Log("[City] Camera pitch euler.x = " + _rig.transform.eulerAngles.x);
      }
    }

    static void LogSha12(string path)
    {
      if (!File.Exists(path))
      {
        Debug.LogWarning("[City] Missing capture for hash: " + path);
        return;
      }

      using var sha = SHA256.Create();
      byte[] hash = sha.ComputeHash(File.ReadAllBytes(path));
      var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
      Debug.Log("[City] SHA256 " + Path.GetFileName(path) + " = " + hex.Substring(0, 12));
    }

    static void EnsureOutputDir()
    {
      string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir));
      if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
    }

    public static void ApplyReport007State(int state)
    {
      if (!ResolveScene()) return;
      SetExpanded(state != 1);
      switch (state)
      {
        case 0:
        case 1:
          _layers.SetLayer(CityLayer.Access);
          _layers.SetOverlay(CityOverlay.Crosswalk, true);
          break;
        case 2:
          _layers.SetLayer(CityLayer.Flags);
          _layers.SetOverlay(CityOverlay.Crosswalk, false);
          break;
        case 3:
          _layers.SetLayer(CityLayer.BuildingId);
          _layers.SetOverlay(CityOverlay.Crosswalk, false);
          break;
        case 4:
          SetPitch(90f);
          _rig.ResetView();
          break;
        case 5:
          SetPitch(60f);
          _rig.ResetView();
          break;
        case 6:
          SetPitch(60f);
          _rig.SetViewFrame(ZoomPitchCenterX, ZoomPitchCenterZ, ZoomOrthoSize);
          break;
        case 7:
          _layers.SetLayer(CityLayer.Flags);
          _layers.SetOverlay(CityOverlay.Crosswalk, true);
          if (_buildings != null) _buildings.Visible = false;
          break;
      }
    }
  }
}
#endif