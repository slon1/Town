# REPORT-013 — зум колёсиком: усиление и привязка к указателю (P4)

- Ветка: `task/013-zoom` от `main`.
- Исполнитель (модель): **Composer**.
- Версии: ADR-004 v1.1 §7, TASK-013. `DOC/adr` и `DOC/tasks` не редактировались.
- Unity **6000.5.9f1**.

## Что было

Зум колёсиком линейный: `ortho *= (1 − scroll·zoomSpeedWheel·0,01)` без нормализации засечек Windows (±120) и без смещения фокуса к курсору. Одна засечка почти не меняла масштаб.

## Что сделано

| Подшаг | Сделано | Самоприёмка |
|--------|---------|-------------|
| **13A** | `scrollNotchUnit` (120) + `zoomPerNotch` (0,15); `notches = scroll.y / unit` (тачпад → unit 1); `ortho *= Exp(−zoomPerNotch·notches)`; после зума `EnsureViewLimits` + `ClampFocusToBounds` в `Update` | Таблица засечек в Play (см. ниже) |
| **13B** | `ApplyZoomAtScreen`: якорь по лучу ∩ `y=0`, сдвиг `_focusXZ`; щипок — центр между пальцами, `notches = ln(ratio)·pinchSensitivity` | Кадры до/после 4 засечек в центре (focus стабилен при зуме в центр карты) |
| **13C** | Поля в `CameraRigSettings_Default`; GC bench 600 кадров; меню `City/Log Wheel Notch Table`, `City/Capture TASK-013 Zoom To Pointer` | GC **0 B**; EditMode-тесты — см. ниже |

## Диагностика scroll (13A)

- Windows Input System: типичная засечка **`scroll.y = ±120`** → `unit = scrollNotchUnit = 120` → **`notches = ±1`**.
- Плавная прокрутка тачпада: **`abs(scroll) < 30`** → **`unit = 1`**, дельта трактуется как дробные засечки (без гигантского шага); per-frame clamp **±12** засечек.

## Таблица «засечки → orthographicSize»

Play `City_Prototype`, `ResetView()`, экран зума `(443,289)`, Game View **885×525** (стартовый ortho по aspect окна **100,95**):

| Засечки | orthographicSize |
|--------:|-----------------:|
| 0 | 100,95 |
| 3 | 64,37 |
| 6 | 41,04 |
| 9 | 26,17 |
| 12 | 16,69 |

До `minOrthoSize = 15` при этом старте: **13-я** засечка упирается в clamp (12 → 16,69). При узком Game View обзор меньше, чем при portrait 1080×2160 (`ResetView` даёт больший ortho); оператору на целевом портрете путь до 15 м короче.

Лог: `[City] wheel notch table screen=(443,289) | 0->100,95 | … | 12->16,69`.

## Кадры «до/после» зум к указателю (13B)

| Файл | focus | ortho | Примечание |
|------|-------|-------|------------|
| `task013_zoom_to_pointer_before.png` | (88, 49) | 40 | 1080×2160, центр экрана |
| `task013_zoom_to_pointer_after.png` | (88, 49) | 21,95 | +4 программные засечки в центре; focus не сдвинут (курсор на центре карты) |

## GC (13C)

`City/Run GC bench (600 frames)`: **frames=600 nonZero=0 totalBytes=0 maxFrameBytes=0**.

## Ограничения

`git diff main...HEAD` — без изменений в Core, `GroundView`, `BuildingsView`, `CityInstaller`, `LayerComposer`.

## Тесты

EditMode: **145/145** passed (см. `unity command "test run"` / Test Runner).

## Изменённые файлы

- `Assets/_Project/Scripts/Runtime/City/Scene/CameraRig.cs`
- `Assets/_Project/Scripts/Runtime/City/Scene/CameraRigSettings.cs`
- `Assets/_Project/City/Scene/CameraRigSettings_Default.asset`
- `Assets/_Project/Scripts/Editor/City/CityReport013Tools.cs`
- `DOC/reports/img/task013_zoom_to_pointer_*.png`
- `DOC/reports/REPORT-013.md`