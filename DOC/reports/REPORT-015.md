# REPORT-015 — тогл «Tram» в панели и Layers Viewer

- Ветка: `task/015-tram-ui` от `main` (после `p5-tram-raster` / TASK-014).
- Исполнитель (модель): **Composer**.
- Версии: ADR-003 v1.3 §5.1, ADR-004 v1.2. `DOC/adr` и `DOC/tasks` не редактировались.
- Unity **6000.5.9f1**.

## Что сделано

| Подшаг | Сделано | Самоприёмка |
|--------|---------|-------------|
| **15A** | Тогл «Tram» в `CityMapLayerPanel` (после Bridge, перед Buildings); prefab пересобран (`820` px высота); `City/Debug/Tram Overlay On/Off` → `ILayerView.SetOverlay(Tram)` | Play: API Off → тогл снят; Layer/Flags из контекстного меню `GroundView` → панель синхронна |
| **15B** | `LayersViewerWindow`: оверлей Tram `#B060FF` (приоритет 5), Bridge 6; Flags `#9A4DFF` (между Crosswalk и Road); тултип `overlay: Tram` при включённом чекбоксе | `tram_viewer.png`, палитра совпадает с `LayerComposer` |
| **15C** | Кадры `source=screen` 1080×2160; меню `City/Capture REPORT-015 Screenshots` | 4 кадра панели + `tram_viewer.png` |

## Кадры (состояние)

| Файл | Слой | Оверлеи | Buildings | focus (x,z) | ortho | pitch | Тогл Tram (панель) |
|------|------|---------|-----------|-------------|-------|-------|-------------------|
| `tram_panel_on.png` | Access | Tram | on | map center | start (~340) | 90° | on |
| `tram_panel_off.png` | Access | — | on | map center | start | 90° | off |
| `tram_panel_on_zoom130_48_25.png` | Access | Tram | on | (130, 48) | 25 | 90° | on |
| `tram_panel_off_zoom130_48_25.png` | Access | — | on | (130, 48) | 25 | 90° | off |
| `tram_viewer.png` | Access (viewer) | Tram | — | — | — | — | оверлей Tram в окне |

Логи: `[City] capture tram_panel_on …`, `tram_panel_off …`, zoom-варианты с `focus=(130,48) ortho=25`.

## Проверка обратной синхронизации (15A)

Без правок `GroundView`: меню **City/Debug/Tram Overlay Off** → тогл «Tram» в панели снимается, фиолетовые пути исчезают; **On** → тогл включён, пути видны. Смена слоя **GroundView → Layer/Flags** → панель переключается на Flags, оверлеи остаются согласованными.

## Ограничения

`git diff main...HEAD` — без изменений в Core, `GroundView`, `BuildingsView`, `CityInstaller`, `LayerComposer`.

## Тесты

EditMode **161/161** passed (включая TASK-014).

## Изменённые файлы

- `Assets/_Project/Scripts/Runtime/City/UI/CityMapLayerPanel.cs`
- `Assets/_Project/Scripts/Editor/City/CityPrototypeUiSceneSetup.cs`
- `Assets/_Project/Scripts/Editor/City/LayersViewerWindow.cs`
- `Assets/_Project/Scripts/Editor/City/CityReport015Tools.cs`
- `Assets/_Project/UI/CityMapLayerPanel.prefab`
- `Assets/_Project/Scenes/City_Prototype.unity` (экземпляр панели)
- `DOC/reports/img/tram_*.png`
- `DOC/reports/REPORT-015.md`

Меню: `City/Build Map Layer Panel Prefab`, `City/Replace Map Layer Panel Instance (TASK-015)`, `City/Debug/Tram Overlay On/Off`, `City/Capture REPORT-015 Screenshots (TASK-015)`, `City/Open Layers Viewer For REPORT-015`.