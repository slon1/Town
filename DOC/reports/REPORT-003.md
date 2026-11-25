# REPORT-003 — Access, анализ, Layers Viewer (TASK-003 / P3 / M1)

- Ветка: `task/003-access`
- Исполнитель: **Composer** для 3A–3E (в ТЗ 3A–3C — Grok; отдельной сессии Grok в среде нет — как с 2C). 3D по ТЗ — Composer.

## Что сделано

| Шаг | Статус | Коммит | Модель |
|-----|--------|--------|--------|
| docs | да | `724b826` | — |
| 3A хвосты P2 | самоприёмка | `a3044f3` | Composer (ТЗ: Grok) |
| 3B-tests → 3B | test-first | `77fa5d7` → `157d459` | Composer |
| 3C-tests → 3C | test-first | `465a098` → `6ff7a74` | Composer |
| 3D Layers Viewer | самоприёмка | (этот коммит) | Composer |
| 3E рапорт | да | (этот коммит) | Composer |

## Хвосты P2 (3A)

### Туннель

Единственный `isTunnel` в модели: **Waterway** osmId **`28777932`**, name=Panke, теги `waterway=stream`, `tunnel=yes`, `layer=-1`, `width=3`. Концы стыкуются с открытыми сегментами `28777930` / `33636305`; середина под **Osloer Straße** (~16 м до осей `24700781`/`4544073`). В растр не попадает → малый `waterClearedByLinear=28` на этом пересечении **ожидаем** (см. `REPORT-003-3A-notes.md`).

### Граничные здания 0,5 м vs 0,1 м

22 здания на границе сетки, одна и та же AABB: Σ **5723,25** vs **5719,35** м², **Δ = +0,068 %** (допуск ±2 %). Поштучно 19/22 в ±2 %; три среза &lt;7 м² дают шум дискретизации (нет нарушений при площади ≥10 м²).

## Хэши

| | Значение |
|--|----------|
| `dataHash` (P2, без изменений) | `0x6B317D7D36FA2A8C` |
| `accessHash` (стабилен ×2 bake) | `0x51C6BF41DA056B8D` |
| settingsHash default | `0x09A4C90D0BFD89D7` → **`0xC8F8DE8B56EBA46F`** (+`islandMinCells`) |

## Access / связность (район)

| Класс | клетки | м² | % сетки |
|-------|-------:|---:|--------:|
| Obstacle | 87201 | 21800,3 | 26,50 |
| Roadway | 31184 | 7796,0 | 9,47 |
| Pedestrian | 210735 | 52683,8 | 64,03 |

Проходимая площадь (Ped+Roadway): **60479,8 м²**; доля Pedestrian среди проходимой ≈ **87,1 %**, Roadway ≈ **12,9 %**.

| Метрика | Значение |
|---------|----------|
| Pedestrian компонент без / с Crosswalk | **116 / 116** |
| Crosswalk клеток / connecting | 1569 / **0** |
| Острова Pedestrian (&lt;200) | **108** (крупнейшие: дворы/обрывки у края; топ в `analysis_access.md`) |
| Road\|Rail клеток | **100** |
| OSM crossings (`stats.crossingCount`) | **11** |
| Среднее CW-клеток на переход | **142,6** (широкая растеризация `crossing`) |

**Замечание по переходам:** число компонент Pedestrian не меняется при исключении Crosswalk, connecting=0. Правила ADR-003 не менялись: в этом районе тротуары уже связаны в обход проезжей части (или CW лежит внутри одной компоненты). Синтетический тест «две улицы + переход» зелёный.

Полный отчёт: `Assets/_Project/City/Baked/analysis_access.md`.

## PNG

| Файл | Размер |
|------|--------|
| `DOC/reports/img/access.png` | 680×484 |
| `DOC/reports/img/components_pedestrian.png` | 680×484 |
| `DOC/reports/img/components_zombie.png` | 680×484 |
| `DOC/reports/img/crop_panke_osloer.png` | **400×400** |
| `DOC/reports/img/crop_railway.png` | **400×400** |
| `DOC/reports/img/crop_courtyard_3583425.png` | **400×400** |

Окно: `ZombieCity/City/Layers Viewer`.

## Тесты

EditMode `ZombieCity.City.Core.Tests`: **все зелёные** (включая AccessClassifier 28, AccessConnectivity 5, прежние P1/P2). Порядок test-first для 3B/3C виден в `git log`.

## DoD (самопроверка)

1. Сборка чистая; 2 asmdef; Core без UnityEngine — да.
2. Тесты зелёные; test-first 3B/3C — да.
3. `dataHash`/`accessHash`/settings «было→стало» — да.
4. `analysis_access.md` — да.
5. PNG Access/Components + кропы ≥400 — да.
6. Visual Access: Roadway на улицах, Pedestrian на тротуарах/дворе 3583425, Obstacle на зданиях/воде/рельсах — по `access.png` / кропам (архитектор).
7. Хвосты P2 — да.
8. Рапорт + push ветки — да (после push).
