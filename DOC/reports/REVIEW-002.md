# REVIEW-002 — независимое ревью растеризации (TASK-002 / 2G)

**Дата:** 2026-11-25  
**Ветка:** `task/002-rasterization`  
**Исполнитель ревью:** отдельная сессия (тесты из ADR-001 §5, ADR-002 §3–§8, без правок продакшена)  
**Тесты:** `Assets/_Project/Tests/EditMode/RasterReview002Tests.cs` (18 случаев)  
**Прогон:** Unity 6000.5.9f1, EditMode, фильтр `RasterReview002` — **18/18 Passed**; сборка `ZombieCity.City.Core.Tests` — **72/72 Passed**

## Сводка

| Метрика | Значение |
|---------|----------|
| Новых edge-case тестов | 18 |
| Провалов | 0 |
| Расхождений с ADR | 0 (открытых) |
| **Вердикт** | **принять** |

## Проверенные случаи

| № | Тест | Ожидание (ADR) | Результат |
|---|------|----------------|-----------|
| 01 | `Review01_ThinAngleRoad_StillContinuous` | Толстая линия под малым углом не «рвётся» (режим A, min halfWidth) | PASS |
| 02 | `Review02_LineCenterOnCellBoundary_ModeA` | Ось линии на границе клеток; соседние центры попадают в капсулу | PASS |
| 03 | `Review03_RingVerticesOnCellCenters_PredictableFill` | Полигон с вершинами в центрах клеток; 2×2 м → 16 клеток, детерминизм | PASS |
| 04 | `Review04_BuildingHolesTouching_EvenOddCourtyard` | Две соприкасающиеся дыры (even-odd), двор без `Building` | PASS |
| 05 | `Review05_BarrierDiagonalThroughCellCorner_NoEightLeak` | Барьер (B) по диагонали угла клетки, width=0 — без 8-протечки | PASS |
| 06 | `Review06_GateAtWallEndpoint` | `Gate` на конце стены: `Gate`, не `Barrier` | PASS |
| 07 | `Review07_BridgeOverBridge_HigherLayerReplacesLower` | Два моста на одной геометрии, layer 2 поверх layer 1 | PASS |
| 08 | `Review08_BuildingPassageClippedAtGridEdge` | `BuildingPassage` у края bbox сетки снимает `Building` внутри | PASS |
| 09 | `Review09_BarrierZeroWidth_StillBlocks_ModeB` | `barrierWidth=0` — барьер всё ещё наносится (режим B) | PASS |
| 10 | `Review10_ObjectEntirelyOutsideGrid_Counted` | Объект вне сетки → `objectsOutsideGrid` | PASS |
| 11 | `Review11_RoadClearsWaterWithoutBridge_StatIncrements` | Дорога без моста снимает `Water`, `waterClearedByLinear>0` (ADR v1.1) | PASS |
| 12 | `Review12_SharedLivingStreet_SetsSharedBit` | `isShared` → бит `Shared` вместе с `Road` | PASS |
| 13 | `Review13_EntranceAndTrafficSignal_NotRasterized` | `Entrance` / `TrafficSignal` не рисуются | PASS |
| 14 | `Review14_BuildingHalfOutsideGrid_PartialFill` | Частичное обрезание здания по границе сетки | PASS |
| 15 | `Review15_WaterwayTunnel_NotInRaster` | `Waterway` + `isTunnel` → нет `Water` | PASS |
| 16 | `Review16_BridgeClearsBarrierUnderneath` | Мост снимает `Barrier` под собой | PASS |
| 17 | `Review17_GreenHoleAtOuterBoundary_EvenOdd` | Дыра у границы outer (even-odd) | PASS |
| 18 | `Review18_RailwayClearsWater_WithoutBridge` | Рельсы снимают `Water` без `isBridge` | PASS |

## Расхождения с ADR

Открытых расхождений не обнаружено. Во время написания тестов уточнялись только ожидания теста (не реализация):

- **Review03:** изначально неверная аналитика площади для кольца на центрах клеток; исправлено в тесте до 16 клеток для квадрата 2×2 м.
- **Review14:** грубая верхняя граница на число клеток заменена на сравнение с полной площадью контура и проверку пустой зоны западнее сетки.

Минимальные воспроизведения расхождений: *нет*.

## Заметки

- Слой `SidewalkGenerated` (ADR-002 §9) намеренно не проверялся — вне объёма P2.
- Карта района (`map.osm`) в 2G не требовалась; все случаи синтетические, как допускает TASK-002.
- Рекомендация: при будущих изменениях барьера (режим B) прогонять вместе с `RasterPrimitivesTests.LineB_Barrier_NoEightConnectedLeak_On200RandomSegments`.

## Вердикт

**принять** — реализация P2 соответствует ADR-002 по проверенным граничным случаям; регрессии в полной сборке Core-тестов нет.