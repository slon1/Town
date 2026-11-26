#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using ZombieCity.City.Core;
using ZombieCity.City.UI;

namespace ZombieCity.City.EditorTools
{
  public static class CityPrototypeUiSceneSetup
  {
    const string PrefabPath = "Assets/_Project/UI/CityMapLayerPanel.prefab";
    const float RowHeight = 96f;
    const float PanelWidth = 420f;
    static readonly Color PanelBackdrop = new Color32(32, 32, 32, 230);
    static readonly Color ButtonFill = new Color32(42, 42, 42, 242);
    static readonly Color ToggleOff = new Color32(55, 55, 55, 242);
    static readonly Color ToggleOn = new Color32(70, 200, 120, 255);
    static readonly Color LabelOnPanel = new Color32(235, 235, 235, 255);

    [MenuItem("City/Build Map Layer Panel Prefab")]
    public static void BuildPrefabMenu() => BuildPrefab();

    [MenuItem("City/Setup City Prototype UI In Scene")]
    public static void Setup()
    {
      Camera cam = Camera.main;
      if (cam == null)
      {
        Debug.LogError("[City] No Main Camera in scene.");
        return;
      }

      EnsureEventSystem();

      Canvas canvas = Object.FindAnyObjectByType<Canvas>();
      if (canvas == null)
      {
        var canvasGo = new GameObject("UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        ApplyCanvasPlaneDistance(canvas, cam);

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 2160f);
        scaler.matchWidthOrHeight = 0.5f;

        Stretch((RectTransform)canvasGo.transform);
      }
      else
      {
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        ApplyCanvasPlaneDistance(canvas, cam);
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
          scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
          scaler.referenceResolution = new Vector2(1080f, 2160f);
          scaler.matchWidthOrHeight = 0.5f;
        }
      }

      var existing = Object.FindAnyObjectByType<CityMapLayerPanel>();
      if (existing != null)
      {
        Debug.Log("[City] CityMapLayerPanel already in scene.");
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        return;
      }

      var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
      if (prefab == null)
      {
        BuildPrefab();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
      }

      if (prefab == null)
      {
        Debug.LogError("[City] Missing panel prefab.");
        return;
      }

      var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
      instance.name = "CityMapLayerPanel";
      EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
      Debug.Log("[City] UI Canvas and layer panel ready.");
    }

    static void EnsureEventSystem()
    {
      if (Object.FindAnyObjectByType<EventSystem>() != null)
      {
        return;
      }

      var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    static void Stretch(RectTransform rect)
    {
      Stretch(rect, Vector2.zero, Vector2.zero);
    }

    static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
      rect.anchorMin = Vector2.zero;
      rect.anchorMax = Vector2.one;
      rect.offsetMin = offsetMin;
      rect.offsetMax = offsetMax;
    }

    static void BuildPrefab()
    {
      if (!AssetDatabase.IsValidFolder("Assets/_Project/UI"))
      {
        AssetDatabase.CreateFolder("Assets/_Project", "UI");
      }

      var root = new GameObject("CityMapLayerPanel", typeof(RectTransform), typeof(CanvasGroup));
      var rootRect = root.GetComponent<RectTransform>();
      rootRect.anchorMin = new Vector2(0f, 1f);
      rootRect.anchorMax = new Vector2(0f, 1f);
      rootRect.pivot = new Vector2(0f, 1f);
      rootRect.sizeDelta = new Vector2(PanelWidth, 720f);
      root.AddComponent<CityMapPanelSafeArea>();
      var panel = root.AddComponent<CityMapLayerPanel>();

      var expanded = CreateChild(root.transform, "Expanded");
      Stretch(expanded, Vector2.zero, Vector2.zero);
      var expandedBg = expanded.gameObject.AddComponent<Image>();
      expandedBg.color = PanelBackdrop;
      expandedBg.raycastTarget = false;
      var vlg = expanded.gameObject.AddComponent<VerticalLayoutGroup>();
      vlg.childAlignment = TextAnchor.UpperLeft;
      vlg.spacing = 8f;
      vlg.padding = new RectOffset(16, 16, 16, 16);
      vlg.childControlHeight = true;
      vlg.childControlWidth = true;
      vlg.childForceExpandHeight = false;
      vlg.childForceExpandWidth = true;

      CreateButton(expanded, "Collapse", "-", out Button collapseBtn);
      var layerGroup = expanded.gameObject.AddComponent<ToggleGroup>();
      layerGroup.allowSwitchOff = false;
      Toggle tAccess = CreateLayerToggle(expanded, "Access", layerGroup, true);
      Toggle tFlags = CreateLayerToggle(expanded, "Flags", layerGroup, false);
      Toggle tBuildingId = CreateLayerToggle(expanded, "BuildingId", layerGroup, false);
      Toggle oRail = CreateOverlayToggle(expanded, "Rail on road");
      Toggle oUnmarked = CreateOverlayToggle(expanded, "Unmarked crossing");
      Toggle oCrosswalk = CreateOverlayToggle(expanded, "Crosswalk");
      Toggle oGate = CreateOverlayToggle(expanded, "Gate / passage");
      Toggle oBridge = CreateOverlayToggle(expanded, "Bridge");
      Toggle tBuildings = CreateOverlayToggle(expanded, "Buildings");
      CreateButton(expanded, "ResetCamera", "Reset camera", out Button resetBtn);

      var collapsed = CreateChild(root.transform, "CollapsedChip");
      collapsed.anchorMin = new Vector2(0f, 1f);
      collapsed.anchorMax = new Vector2(0f, 1f);
      collapsed.pivot = new Vector2(0f, 1f);
      collapsed.sizeDelta = new Vector2(120f, RowHeight);
      var chipBg = collapsed.gameObject.AddComponent<Image>();
      chipBg.color = PanelBackdrop;
      chipBg.raycastTarget = false;
      CreateButton(collapsed, "Expand", "=", out Button expandBtn);
      collapsed.gameObject.SetActive(false);

      var panelSo = new SerializedObject(panel);
      panelSo.FindProperty("toggleAccess").objectReferenceValue = tAccess;
      panelSo.FindProperty("toggleFlags").objectReferenceValue = tFlags;
      panelSo.FindProperty("toggleBuildingId").objectReferenceValue = tBuildingId;
      panelSo.FindProperty("toggleBuildings").objectReferenceValue = tBuildings;
      panelSo.FindProperty("layerGroup").objectReferenceValue = layerGroup;
      SetOverlayBinding(panelSo, "overlayRailOnRoad", CityOverlay.RailOnRoad, oRail);
      SetOverlayBinding(panelSo, "overlayUnmarked", CityOverlay.UnmarkedCrossing, oUnmarked);
      SetOverlayBinding(panelSo, "overlayCrosswalk", CityOverlay.Crosswalk, oCrosswalk);
      SetOverlayBinding(panelSo, "overlayGate", CityOverlay.GatePassage, oGate);
      SetOverlayBinding(panelSo, "overlayBridge", CityOverlay.Bridge, oBridge);
      panelSo.FindProperty("resetCameraButton").objectReferenceValue = resetBtn;
      panelSo.FindProperty("collapseButton").objectReferenceValue = collapseBtn;
      panelSo.FindProperty("expandButton").objectReferenceValue = expandBtn;
      panelSo.FindProperty("expandedContent").objectReferenceValue = expanded.gameObject;
      panelSo.FindProperty("collapsedChip").objectReferenceValue = collapsed.gameObject;
      panelSo.ApplyModifiedPropertiesWithoutUndo();

      PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
      Object.DestroyImmediate(root);
      AssetDatabase.SaveAssets();
      Debug.Log("[City] Prefab saved: " + PrefabPath);
    }

    static void SetOverlayBinding(SerializedObject panelSo, string prop, CityOverlay overlay, Toggle toggle)
    {
      var p = panelSo.FindProperty(prop);
      p.FindPropertyRelative("overlay").intValue = (int)overlay;
      p.FindPropertyRelative("toggle").objectReferenceValue = toggle;
    }

    static RectTransform CreateChild(Transform parent, string name)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      return go.GetComponent<RectTransform>();
    }

    static Toggle CreateLayerToggle(RectTransform parent, string label, ToggleGroup group, bool isOn)
    {
      Toggle t = CreateToggleRow(parent, label);
      t.group = group;
      t.isOn = isOn;
      return t;
    }

    static Toggle CreateOverlayToggle(RectTransform parent, string label) => CreateToggleRow(parent, label);

    static Toggle CreateToggleRow(RectTransform parent, string label)
    {
      var row = CreateChild(parent, label);
      row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
      var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
      h.childAlignment = TextAnchor.MiddleLeft;
      h.spacing = 12f;
      h.padding = new RectOffset(8, 8, 4, 4);
      h.childControlWidth = false;
      h.childControlHeight = true;

      var toggleGo = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle), typeof(Image));
      toggleGo.transform.SetParent(row, false);
      toggleGo.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);
      var toggle = toggleGo.GetComponent<Toggle>();
      var toggleImage = toggleGo.GetComponent<Image>();
      toggleImage.color = ToggleOff;
      toggle.targetGraphic = toggleImage;

      var check = new GameObject("Check", typeof(RectTransform), typeof(Image));
      check.transform.SetParent(toggleGo.transform, false);
      var checkRect = check.GetComponent<RectTransform>();
      checkRect.anchorMin = Vector2.zero;
      checkRect.anchorMax = Vector2.one;
      checkRect.offsetMin = new Vector2(8, 8);
      checkRect.offsetMax = new Vector2(-8, -8);
      var checkImg = check.GetComponent<Image>();
      checkImg.color = ToggleOn;
      toggle.graphic = checkImg;
      toggle.transition = Selectable.Transition.ColorTint;

      var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
      textGo.transform.SetParent(row, false);
      var text = textGo.GetComponent<TextMeshProUGUI>();
      text.text = label;
      text.fontSize = 32;
      text.color = LabelOnPanel;
      text.alignment = TextAlignmentOptions.MidlineLeft;
      textGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
      return toggle;
    }

    static void CreateButton(RectTransform parent, string name, string caption, out Button button)
    {
      var row = CreateChild(parent, name);
      row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;
      var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
      go.transform.SetParent(row, false);
      Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
      var img = go.GetComponent<Image>();
      img.color = ButtonFill;
      button = go.GetComponent<Button>();
      button.targetGraphic = img;

      var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
      textGo.transform.SetParent(go.transform, false);
      Stretch(textGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
      var text = textGo.GetComponent<TextMeshProUGUI>();
      text.text = caption;
      text.fontSize = 30;
      text.color = LabelOnPanel;
      text.alignment = TextAlignmentOptions.Center;
    }

    public static void ApplyCanvasPlaneDistance(Canvas canvas, Camera cam)
    {
      if (canvas == null || cam == null) return;
      canvas.planeDistance = cam.nearClipPlane + 0.5f;
    }

    [MenuItem("City/Apply UI Canvas Plane Distance (TASK-010)")]
    public static void ApplySceneCanvasPlaneDistance()
    {
      var cam = Camera.main;
      var canvas = Object.FindAnyObjectByType<Canvas>();
      if (cam == null || canvas == null)
      {
        Debug.LogError("[City] No Main Camera or Canvas in scene.");
        return;
      }

      ApplyCanvasPlaneDistance(canvas, cam);
      EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
      Debug.Log("[City] Canvas planeDistance = " + canvas.planeDistance);
    }
  }
}
#endif