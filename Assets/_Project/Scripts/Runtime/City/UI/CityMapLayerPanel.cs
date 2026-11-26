using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using ZombieCity.City.Core;
using ZombieCity.City.Scene;

namespace ZombieCity.City.UI
{
    public sealed class CityMapLayerPanel : MonoBehaviour
    {
        [Serializable]
        struct OverlayToggle { public CityOverlay overlay; public Toggle toggle; }

        [SerializeField] Toggle toggleAccess;
        [SerializeField] Toggle toggleFlags;
        [SerializeField] Toggle toggleBuildingId;
        [SerializeField] ToggleGroup layerGroup;
        [SerializeField] OverlayToggle overlayRailOnRoad;
        [SerializeField] OverlayToggle overlayUnmarked;
        [SerializeField] OverlayToggle overlayCrosswalk;
        [SerializeField] OverlayToggle overlayGate;
        [SerializeField] OverlayToggle overlayBridge;
        [SerializeField] Button resetCameraButton;
        [SerializeField] Button collapseButton;
        [SerializeField] Button expandButton;
        [SerializeField] GameObject expandedContent;
        [SerializeField] GameObject collapsedChip;

        ILayerView _layers;
        ICameraRig _cameraRig;
        bool _syncing;

        [Inject]
        public void Construct(ILayerView layers, ICameraRig cameraRig)
        {
            _layers = layers;
            _cameraRig = cameraRig;
        }

        void Start()
        {
            if (_layers == null) return;
            WireLayer(toggleAccess, CityLayer.Access);
            WireLayer(toggleFlags, CityLayer.Flags);
            WireLayer(toggleBuildingId, CityLayer.BuildingId);
            WireOverlay(overlayRailOnRoad);
            WireOverlay(overlayUnmarked);
            WireOverlay(overlayCrosswalk);
            WireOverlay(overlayGate);
            WireOverlay(overlayBridge);
            resetCameraButton?.onClick.AddListener(() => _cameraRig?.ResetView());
            collapseButton?.onClick.AddListener(ToggleCollapsed);
            expandButton?.onClick.AddListener(ToggleCollapsed);
            _layers.Changed += SyncFromModel;
            SyncFromModel();
        }

        void OnDestroy()
        {
            if (_layers != null) _layers.Changed -= SyncFromModel;
        }

        void WireLayer(Toggle t, CityLayer layer)
        {
            if (t == null) return;
            if (layerGroup != null) t.group = layerGroup;
            t.onValueChanged.AddListener(on => { if (!_syncing && on) _layers.SetLayer(layer); });
        }

        void WireOverlay(OverlayToggle e)
        {
            if (e.toggle == null) return;
            e.toggle.onValueChanged.AddListener(on => { if (!_syncing) _layers.SetOverlay(e.overlay, on); });
        }

        void ToggleCollapsed()
        {
            bool show = expandedContent != null && !expandedContent.activeSelf;
            if (expandedContent != null) expandedContent.SetActive(show);
            if (collapsedChip != null) collapsedChip.SetActive(!show);
        }

        void SyncFromModel()
        {
            if (_layers == null) return;
            _syncing = true;
            var layer = _layers.Layer;
            toggleAccess?.SetIsOnWithoutNotify(layer == CityLayer.Access);
            toggleFlags?.SetIsOnWithoutNotify(layer == CityLayer.Flags);
            toggleBuildingId?.SetIsOnWithoutNotify(layer == CityLayer.BuildingId);
            overlayRailOnRoad.toggle?.SetIsOnWithoutNotify(_layers.GetOverlay(CityOverlay.RailOnRoad));
            overlayUnmarked.toggle?.SetIsOnWithoutNotify(_layers.GetOverlay(CityOverlay.UnmarkedCrossing));
            overlayCrosswalk.toggle?.SetIsOnWithoutNotify(_layers.GetOverlay(CityOverlay.Crosswalk));
            overlayGate.toggle?.SetIsOnWithoutNotify(_layers.GetOverlay(CityOverlay.GatePassage));
            overlayBridge.toggle?.SetIsOnWithoutNotify(_layers.GetOverlay(CityOverlay.Bridge));
            _syncing = false;
        }
    }
}
