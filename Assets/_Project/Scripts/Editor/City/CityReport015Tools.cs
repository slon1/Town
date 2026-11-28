#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZombieCity.City.Core;
using ZombieCity.City.Scene;
using ZombieCity.City.UI;

namespace ZombieCity.City.EditorTools
{
  public static class CityReport015Tools
  {
    const string ScenePath = "Assets/_Project/Scenes/City_Prototype.unity";
    const string OutputDir = "DOC/reports/img";
    const float RefAspect = 1080f / 2160f;
    const float ZoomX = 130f;
    const float ZoomZ = 48f;
    const float ZoomOrtho = 25f;
    const int WarmupFrames = 25;
    const int SettleFrames = 18;

    static ILayerView _layers;
    static IBuildingsView _buildings;
    static CityMapLayerPanel _panel;
    static CameraRig _rig;
    static int _step;
    static int _frame;
    static bool _capturing;

    [MenuItem("City/Debug/Tram Overlay On")]
    public static void TramOverlayOn() => SetTramOverlay(true);

    [MenuItem("City/Debug/Tram Overlay Off")]
    public static void TramOverlayOff() => SetTramOverlay(false);

    static void SetTramOverlay(bool enabled)
    {
      if (!Application.isPlaying)
      {
        Debug.LogWarning("[City] Enter Play Mode on City_Prototype first.");
        return;
      }

      var layers = Object.FindAnyObjectByType<GroundView>();
      if (layers == null)
      {
        Debug.LogError("[City] No GroundView.");
        return;
      }

      layers.SetOverlay(CityOverlay.Tram, enabled);
      Debug.Log("[City] CityOverlay.Tram = " + enabled);
    }

    [MenuItem("City/Capture REPORT-015 Screenshots (TASK-015)")]
    public static void StartCapture()
    {
      EnsureOutputDir();
      if (!EditorApplication.isPlaying)
      {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
        EditorApplication.delayCall += () => EditorApplication.delayCall += BeginCaptureSequence;
        return;
      }

      BeginCaptureSequence();
    }

    static void BeginCaptureSequence()
    {
      EditorApplication.update -= CaptureTick;
      _step = 0;
      _frame = 0;
      _capturing = true;
      EditorApplication.update += CaptureTick;
    }

    static void CaptureTick()
    {
      if (!_capturing || !Application.isPlaying)
      {
        EditorApplication.update -= CaptureTick;
        _capturing = false;
        return;
      }

      _frame++;
      switch (_step)
      {
        case 0:
          if (_frame < WarmupFrames) return;
          if (!ResolveScene())
          {
            StopCapture();
            return;
          }

          Screen.SetResolution(1080, 2160, false);
          SetExpanded(true);
          ApplyVariantA(tramOn: true, useResetView: true);
          _frame = 0;
          _step = 1;
          return;
        case 1:
          if (_frame < SettleFrames) return;
          _rig.LogCaptureState("tram_panel_on");
          CaptureGameViewScreen("tram_panel_on.png");
          ApplyVariantA(tramOn: false, useResetView: true);
          _frame = 0;
          _step = 2;
          return;
        case 2:
          if (_frame < SettleFrames) return;
          _rig.LogCaptureState("tram_panel_off");
          CaptureGameViewScreen("tram_panel_off.png");
          ApplyVariantA(tramOn: true, useResetView: false);
          _rig.SetViewFrame(ZoomX, ZoomZ, ZoomOrtho, RefAspect);
          _frame = 0;
          _step = 3;
          return;
        case 3:
          if (_frame < SettleFrames) return;
          _rig.LogCaptureState("tram_panel_on_zoom130_48");
          CaptureGameViewScreen("tram_panel_on_zoom130_48_25.png");
          ApplyVariantA(tramOn: false, useResetView: false);
          _rig.SetViewFrame(ZoomX, ZoomZ, ZoomOrtho, RefAspect);
          _frame = 0;
          _step = 4;
          return;
        case 4:
          if (_frame < SettleFrames) return;
          _rig.LogCaptureState("tram_panel_off_zoom130_48");
          CaptureGameViewScreen("tram_panel_off_zoom130_48_25.png");
          Debug.Log("[City] TASK-015 captures saved under " + OutputDir);
          StopCapture();
          return;
      }
    }

    static void StopCapture()
    {
      _capturing = false;
      EditorApplication.update -= CaptureTick;
    }

    static void ApplyVariantA(bool tramOn, bool useResetView)
    {
      _layers.SetLayer(CityLayer.Access);
      _layers.SetOverlay(CityOverlay.RailOnRoad, false);
      _layers.SetOverlay(CityOverlay.UnmarkedCrossing, false);
      _layers.SetOverlay(CityOverlay.Crosswalk, false);
      _layers.SetOverlay(CityOverlay.GatePassage, false);
      _layers.SetOverlay(CityOverlay.Bridge, false);
      _layers.SetOverlay(CityOverlay.Tram, tramOn);
      if (_buildings != null) _buildings.Visible = true;
      var settings = _rig.ActiveSettings;
      if (settings != null) settings.pitchDegrees = 90f;
      if (useResetView) _rig.ResetView();
    }

    static bool ResolveScene()
    {
      _layers = Object.FindAnyObjectByType<GroundView>();
      _buildings = Object.FindAnyObjectByType<BuildingsView>();
      _panel = Object.FindAnyObjectByType<CityMapLayerPanel>();
      _rig = Object.FindAnyObjectByType<CameraRig>();
      if (_layers == null || _panel == null || _rig == null)
      {
        Debug.LogError("[City] TASK-015: missing GroundView, panel, or CameraRig.");
        return false;
      }

      return true;
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

    static void CaptureGameViewScreen(string fileName)
    {
      var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir, fileName));
      ScreenCapture.CaptureScreenshot(path, 1);
      Debug.Log("[City] Screenshot (screen): " + path);
    }

    static void EnsureOutputDir()
    {
      var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir));
      if (!Directory.Exists(full)) Directory.CreateDirectory(full);
    }

    [MenuItem("City/Open Layers Viewer For REPORT-015")]
    public static void OpenLayersViewerForReport() => LayersViewerWindow.OpenForTramReport();
  }
}
#endif