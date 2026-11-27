#if UNITY_EDITOR
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using ZombieCity.City.Core;
using ZombieCity.City.Scene;
using ZombieCity.City.UI;

namespace ZombieCity.City.EditorTools
{
  public static class CityReport007GcBench
  {
    const int FrameCount = 600;
    static bool _running;
    static int _frame;
    static ILayerView _layers;
    static CameraRig _rig;
    static ProfilerRecorder _gcRecorder;
    static long _totalGc;
    static long _maxGc;
    static int _nonZeroFrames;

    [MenuItem("City/Run GC bench (600 frames, TASK-010)")]
    public static void RunFromMenu()
    {
      if (!Application.isPlaying)
      {
        Debug.LogWarning("[City] Enter Play Mode on City_Prototype first.");
        return;
      }

      Begin();
    }

    public static void Begin()
    {
      _layers = Object.FindAnyObjectByType<GroundView>();
      _rig = Object.FindAnyObjectByType<CameraRig>();
      if (_layers == null || _rig == null)
      {
        Debug.LogError("[City] GC bench: missing GroundView or CameraRig.");
        return;
      }

      _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Allocated In Frame");
      _frame = 0;
      _totalGc = 0;
      _maxGc = 0;
      _nonZeroFrames = 0;
      _running = true;
      EditorApplication.update += Tick;
      Debug.Log("[City] GC bench started (" + FrameCount + " frames).");
    }

    static void Tick()
    {
      if (!_running || !Application.isPlaying)
      {
        Stop();
        return;
      }

      SimulateInput();
      long gc = _gcRecorder.Valid ? _gcRecorder.LastValue : 0;
      if (gc > 0)
      {
        _nonZeroFrames++;
        _totalGc += gc;
        if (gc > _maxGc) _maxGc = gc;
      }

      _frame++;
      if (_frame >= FrameCount)
      {
        var sb = new StringBuilder();
        sb.Append("[City] GC bench: frames=").Append(FrameCount);
        sb.Append(" nonZero=").Append(_nonZeroFrames);
        sb.Append(" totalBytes=").Append(_totalGc);
        sb.Append(" maxFrameBytes=").Append(_maxGc);
        Debug.Log(sb.ToString());
        Stop();
      }
    }

    static void SimulateInput()
    {
      float t = _frame * 0.05f;
      if (_rig != null)
      {
        float x = Mathf.Sin(t) * 40f;
        float z = Mathf.Cos(t * 0.7f) * 40f;
        _rig.SetViewFrame(x, z, 40f + Mathf.Sin(t * 0.2f) * 5f, 0f);
      }

      if (_layers != null && (_frame % 90) == 0)
      {
        var layer = (_frame / 90) % 3;
        _layers.SetLayer(layer == 0 ? CityLayer.Access : layer == 1 ? CityLayer.Flags : CityLayer.BuildingId);
        _layers.SetOverlay(CityOverlay.Crosswalk, (_frame / 45) % 2 == 0);
      }
    }

    static void Stop()
    {
      _running = false;
      EditorApplication.update -= Tick;
      if (_gcRecorder.Valid) _gcRecorder.Dispose();
    }
  }
}
#endif