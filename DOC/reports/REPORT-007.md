# REPORT-007 — камера и UI-панель (P4)

- Ветка / коммиты: `task/007-camera-ui` от `main` `3003f1a`. Подшаги `7A` (`4b3311d`, `5f26f4b`), `7B` (`a159033`), `7C` — в этом коммите. Запушено в `origin/task/007-camera-ui`.
- Исполнитель (модель): Composer
- Версии: ADR-004 v1.1, ADR-003 v1.2, TASK-007 v1.1. `DOC/adr` и `DOC/tasks` не редактировались.

## Что сделано (по подшагам, с отметкой самоприёмки)

- **7A.** `ICameraRig`, `CameraRig`, `CameraRigSettings`, `CameraUiInstaller`; удалён `CityOverviewCamera.cs` (единственное снятое из 006). Сцена: `CameraRig` на Main Camera, installer в SceneContext. Input System (`Mouse`/`Touchscreen`), pan по плоскости `y=0`, границы AABB углов кадра, `ResetView()`. Самоприёмка.
- **7B.** `CityMapLayerPanel`, `CityMapPanelSafeArea`, prefab `Assets/_Project/UI/CityMapLayerPanel.prefab`, Canvas Screen Space – Camera (1080×2160, match 0.5), EventSystem + `InputSystemUIInputModule`, панель на сцене. Меню `City/Setup City Prototype UI In Scene` (MCP). Самоприёмка.
- **7C.** Скриншоты `DOC/reports/img/task007_*.png` (Game View 1080×2160, `capture_game_view`), этот рапорт. Самоприёмка.

## Критерии приёмки

| Критерий | Результат | Доказательство |
|---|---|---|
| Core / 006 код не менялся (кроме удаления overview) | да | `git diff main...HEAD` — нет `Core/`, `GroundView`, `BuildingsView`, `CityInstaller`, `LayerComposer` |
| asmdef по-прежнему два | да | только `ZombieCity.City.Core` + EditMode-тесты |
| EditMode-тесты | да | **121** passed (`ZombieCity.City.Core.Tests`) |
| Камера + UI в `City_Prototype` | да | Play Mode, панорама/зум мышью, панель |
| Скриншоты 1080×2160 | да | 6 файлов в `DOC/reports/img/` |
| `CameraUiInstaller` в SceneContext | да | сцена + отчёт |
| Ветка на origin | да | `task/007-camera-ui` |

## Скриншоты (7C)

| Файл | Содержание |
|---|---|
| `task007_panel_access_crosswalk.png` | панель развёрнута, Access, оверлей Crosswalk |
| `task007_panel_collapsed.png` | панель свёрнута |
| `task007_layer_flags.png` | слой Flags |
| `task007_layer_building_id.png` | слой BuildingId |
| `task007_camera_pitch_90.png` | камера pitch 90° (`CameraRigSettings`) |
| `task007_camera_pitch_60.png` | камера pitch 60° (`CameraRigSettings`) |

## Файлы (новые / существенные)

**Runtime:** `Scene/ICameraRig.cs`, `CameraRig.cs`, `CameraRigSettings.cs`, `CameraUiInstaller.cs`; `UI/CityMapLayerPanel.cs`, `CityMapPanelSafeArea.cs`.

**Assets:** `City/Scene/CameraRigSettings_Default.asset`; `UI/CityMapLayerPanel.prefab`; сцена `City_Prototype.unity`.

**Editor:** `CityPrototypeUiSceneSetup.cs` (prefab + setup сцены), `CityReport007ScreenshotMenu.cs` (скриншоты для рапорта).

**Удалено:** `CityOverviewCamera.cs` (+ `.meta`).

## Цифры

- Unity **6000.5.9f1**, URP, Extenject 9.2, Input System package **1.20.1** (`activeInputHandler: Input System`).
- Модель района: `CityModel_map` / `CityBake_map` (без перепека в 007).
- Меш на старте сцены: зданий **69**, вершин **4060**, треуг. **2300** (лог `[City] buildings ...`, типично ~90–150 мс).

## Смены сцены (MCP)

- Снят `CityOverviewCamera`, добавлен `CameraRig` на Main Camera.
- `CameraUiInstaller` на `SceneContext`, ссылки на rig и `CameraRigSettings_Default`.
- Меню **`City/Setup City Prototype UI In Scene`**: Canvas, EventSystem, экземпляр prefab панели.

## Ввод и касание

- Активен **только Input System** (`Project Settings → Player → Active Input Handling = Input System Package`).
- Мышь: панорама ЛКМ, зум колёсиком; UI блокирует жесты (`IsPointerOverGameObject`).
- **Касание / щипок:** код реализован (`Touchscreen`, pinch); **на устройстве и Device Simulator не проверял** (эмуляции pinch на мыши нет).

## Производительность (GC)

- `CameraRig.Update` и `CityMapLayerPanel`: без LINQ, без `new List`/массивов в цикле; поля кэшируются, pinch обходит `touches` без аллокаций.
- `CityMapPanelSafeArea.Update` только сравнивает `safeArea` / размер экрана.
- Deep Profiler в 007 не гонялся; ожидаем 0 B managed GC в кадре на pan/zoom/кликах UI (кроме первого кадра Unity UI). Для приёмки архитектору — при необходимости Deep Profile 10 с панорамы в Play Mode.

## Отклонения от ТЗ и почему

- Скриншоты сняты через **Unity MCP `capture_game_view`** 1080×2160, не вручную из закреплённого Game View; после `ApplyReport007State` — пауза ~2 с до кадра (стабилизация pitch/текстуры).
- Тогл «Здания» (`IBuildingsView`) **не добавлен** — по TASK-007 до TASK-008.

## ADR-change-request

Нет.

## Известные проблемы / ограничения

- Автоматический захват через меню `City/Capture REPORT-007 Screenshots` без MCP менее надёжен, чем `ApplyReport007State` + `capture_game_view`; для архитектора при сомнении — повторить шесть кадров по таблице выше.
- Pitch 60° меняет только `CameraRigSettings_Default.asset` в Play Mode; после проверки в ассете снова **90°** (если редактировался в play).

## Вопросы

Нет.

## Как проверить

1. Ветка `task/007-camera-ui`, сцена `Assets/_Project/Scenes/City_Prototype.unity`.
2. Play, Game View 1080×2160: панель слева, слои и оверлеи, Reset camera, сворачивание.
3. EditMode: `ZombieCity.City.Core.Tests` — все зелёные.
4. При необходимости пересобрать UI: `City/Build Map Layer Panel Prefab`, `City/Setup City Prototype UI In Scene`.