# TASK-012 — Убрать диагностический цвет кнопки и сдать полный комплект кадров (P4, финал перед M2)

- **Исполнитель: COMPOSER (только).** Если вы не Composer — остановитесь и сообщите оператору.
- **Требуемые версии:** `ADR-004` v1.1 (§8, §10), `TASK-011`. Документы `DOC/adr` и `DOC/tasks` не редактировать.
- **Ветка:** существующая `task/007-camera-ui`; коммиты `[TASK-007/fix]`.
- **Не менять:** Core, `GroundView`, `BuildingsView`, `CityInstaller`, `LayerComposer`.

## Контекст

На присланных кадрах TASK-011 кнопка «Reset camera» **розовая (magenta)** почти на всех кадрах: диагностический цвет из `ui_diagnosis` остался в состоянии панели (в prefab/сцене либо утёк через меню съёмки). Для приёмки нужны штатные цвета панели; magenta допустим только на кадре `task010_ui_diagnosis.png`.

Принятое по TASK-011: `SetViewFrame` теперь работает (кадр 60° zoom показывает стены зданий), подложка панели непрозрачная, тоглы синхронны со слоем и оверлеями, перекрытия UI зданиями по глубине нет.

Не получены архитектором и нужны в этом пакете: `task007_panel_sync_flags.png`, `task007_camera_pitch_90.png`, `task007_camera_pitch_60.png`, `task010_ui_diagnosis.png`, `panel_buildings_toggle.png`.

## Подшаги

### 12A — вернуть штатный цвет «Reset camera»
- Найти, где задаётся magenta (prefab `CityMapLayerPanel.prefab`, сцена, меню съёмки `CityReport007ScreenshotMenu`) и как он попал в итоговое состояние.
- Диагностические изменения в меню съёмки делать **временно с обязательным восстановлением** (`try/finally`), чтобы они не утекали в последующие кадры и не сохранялись в prefab/сцену.
- Вернуть кнопке тот же стиль, что у остальных кнопок панели (тёмный непрозрачный фон, светлый текст).
- В рапорте: где был цвет, что исправлено, и подтверждение, что в **закоммиченных** prefab и сцене magenta нет (поиск по YAML на значение цвета кнопки).
- **Критерий:** в prefab и сцене нет диагностического цвета; Play Mode: кнопка штатная. Самоприёмка.

### 12B — пересъёмка комплекта
- Пересъёмка всех кадров по цепочке меню TASK-011; magenta должен быть **только** на `task010_ui_diagnosis.png`.
- Имена и состояния — как в таблице TASK-011/11E: `task007_panel_access_crosswalk.png`, `task007_panel_collapsed.png`, `task007_layer_flags.png`, `task007_layer_building_id.png`, `task007_camera_pitch_90.png`, `task007_camera_pitch_60.png`, `task007_camera_pitch_60_zoom.png`, `task007_panel_sync_flags.png`, `panel_buildings_toggle.png`, `task010_ui_diagnosis.png`.
- Для `task007_layer_building_id.png` взять состояние «слой BuildingId, **Buildings выключен**» (иначе крыши закрывают цвета id; кадр со включёнными зданиями на этом слое смысла не имеет); `panel_buildings_toggle.png` оставить как есть по таблице, но отличать от `layer_building_id` оверлеями/позицией не требуется.
- Ожидание по `task007_panel_sync_flags.png`: центр (88; 49), масштаб ≈ 43 px/м, на карте жёлтые полосы Crosswalk на переходах у перекрёстка, «Buildings» снят, слой Flags.
- Строка состояния для каждого кадра (слой / оверлеи / Buildings / focus / ortho / pitch) в рапорте. SHA256 (12 hex) кадров `pitch_90`, `pitch_60`, `pitch_60_zoom`.
- **Критерий:** комплект из десяти файлов в `DOC/reports/img/`, magenta только в диагностическом. Самоприёмка.

### 12C — рапорт
- Раздел «TASK-012» в `DOC/reports/REPORT-007.md`: что было, что исправлено, таблица состояний, SHA256, `git diff --stat` по запрещённым файлам (пусто), число тестов. Коммит, `git push origin task/007-camera-ui`.

## Definition of Done (проверяет архитектор)

1. В закоммиченных prefab и сцене нет диагностического цвета; на кадрах (кроме `task010_ui_diagnosis.png`) «Reset camera» штатная.
2. Все десять кадров 1080×2160, у каждого строка состояния; `layer_building_id` показывает цвета id.
3. `panel_sync_flags`: виден Crosswalk, масштаб/центр по ожиданию.
4. Core, `GroundView`, `BuildingsView`, `CityInstaller`, `LayerComposer` не изменены; тесты зелёные; ветка в `origin`.
