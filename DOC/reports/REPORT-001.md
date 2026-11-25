# REPORT-001 — OSM → CityModel (TASK-001 / P1)

- Ветка / коммиты: `task/001-osm-citymodel` (все запушены в `origin`)
- Исполнитель (модель): Composer

## Что сделано (по подшагам, с отметкой самоприёмки)

| Шаг | Статус | Коммит |
|-----|--------|--------|
| 1A каркас | самоприёмка | `e98c3bb` (+ `4524f36`) |
| 1B модель+хэш | самоприёмка | `6c9b621` |
| 1C парсер+проекция | самоприёмка | `9ff8dbc` |
| 1D классификация | самоприёмка | `0de23f0` |
| 1E импорт-ассет | самоприёмка | `77861c5` |
| 1F gizmo | самоприёмка | `c8691ae` |
| 1G тесты+фикстуры | самоприёмка | (коммит 1G) |

### Порядок TDD в 1D (`git log --stat`)

**Не соблюдён.** Один коммит `0de23f0` сразу добавил тесты (+187 строк в `CityModelHashTests`) **и** реализацию (`CityModelBuilder` 747+, `MultipolygonAssembler`, `RingOps`, `TagValueParser`). Отдельного «красные тесты → реализация» в истории нет.

## Критерии приёмки

| Критерий | Результат | Доказательство |
|----------|-----------|----------------|
| Сборка, ровно 2 asmdef | да | `ZombieCity.City.Core`, `ZombieCity.City.Core.Tests` |
| EditMode-тесты зелёные | **да, 22/22** | Test Runner, assembly `ZombieCity.City.Core.Tests` |
| Импорт, хэш стабилен | да | `0x4ED5D7918533F31B` = `0x4ED5D7918533F31B` |
| Статистика + доля incomplete MP | да | 0 % (0 incomplete / 1 MP); порог >10 % не достигнут |
| Gizmo + скриншот | да | `DOC/reports/img/Dev_CityModel_gizmo.png` |
| Core без UnityEngine | да | `noEngineReferences` |
| Контракты ADR-001 | да | |
| Рапорт + ветка запушена | да | этот файл; `origin/task/001-osm-citymodel` |

## Район (map.osm)

| | |
|--|--|
| Bbox (из `<bounds>`) | **52.554548, 13.377662 … 52.556724, 13.382683** |
| Размер района | **339,45 м × 241,96 м** |
| dataHash (2 прогона) | `0x4ED5D7918533F31B` / `0x4ED5D7918533F31B` |
| buildings / holes | 69 / 1 |
| roads / walkways | 32 / 93 |
| waterways / railways | 3 / 4 |
| bridges / tunnels / crossings | 0 / 1 / 11 |
| incomplete MP | 0 (0 %) |
| building:part ignored | 0 (в map.osm) |

**Мосты:** 0 в данных (`bridge=*`, `bridge:structure`, `man_made=bridge` отсутствуют); логика проверена синтетически (`tunnel_bridge_layer.osm.txt`). Overpass не заказывали (решение A).

### sidewalkMode на Road

| Mode | Count |
|------|------:|
| None | 22 |
| Unknown | 10 |

### Walkway kinds

| Kind | Count |
|------|------:|
| Footway | 47 |
| Sidewalk | 16 |
| Cycleway | 17 |
| Crossing | 11 |
| Steps | 1 |
| Path | 1 |

### Источник высоты зданий

| Источник | Count |
|----------|------:|
| `height` / `building:height` | 0 |
| `building:levels` / `roof:levels` | 57 |
| default (12 м) | 12 |

### Waterway / Railway

| | |
|--|--|
| Waterway kinds | Stream: **3** |
| Railway | **4** линии (`isBridge=0`, `isTunnel=0` на этом файле; отдельного `RailwayKind` в модели нет) |

## Gizmo

Скриншот: [`DOC/reports/img/Dev_CityModel_gizmo.png`](img/Dev_CityModel_gizmo.png) — вид Top, север = +Z (синяя ось), здание с двором-дырой у центра gizmo, водоток (синий) и железнодорожный коридор.

## Матрица покрытия 1G (до / после)

| Требование 1G | До 1G | После 1G | Тест / фикстура |
|---------------|-------|----------|-----------------|
| Проекция ±0,05 м, N=+Z, E=+X | да | да | `Projection_*` |
| width/height форматы | да | да | `TagParser_WidthHeightFormats` |
| outer CCW / holes CW | да | да | `RingOps_OuterCcw_HolesCw` |
| MP с дырой | частично (map) | да | `multipolygon_hole.osm.txt` |
| MP два outer-ways | нет | да | `multipolygon_two_outers.osm.txt` |
| Неполный MP + warning | нет | да | `incomplete_multipolygon.osm.txt` |
| building_passage vs tunnel | нет | да | `tunnel_bridge_layer.osm.txt` |
| bridge + layer | нет | да | там же |
| living_street → isShared | нет | да | `living_track_waterway_part.osm.txt` |
| track / track+motor | нет | да | там же |
| waterway → Waterway | нет | да | там же (Canal) |
| building:part + счётчик | нет | да | там же |
| Хэш золотой фикстуры | да | да | `CityModel_DataHash_MatchesGoldenFixture` |
| Хэш при перестановке XML | нет | да | `hash_order_a/b.osm.txt` |
| Фикстуры `Fixtures/*.osm.txt` | нет | да | 7 файлов на диске |

**Недостающие до 1G (закрыты):** дисковые фикстуры; dedicated waterway / building:part / living+track; XML-reorder hash; incomplete MP; two-outer MP; tunnel/bridge layer.

**Остаточные пробелы (не блокер 1G):** отдельный тест «самопересечение / дыра у границы outer» из заметки 1D ADR-кейсов — в чеклисте 1G явно не требуется; Railway без kind-enum.

## 1G итог тестов

**22 Passed / 0 Failed** (`ZombieCity.City.Core.Tests`):

- `CityModelHashTests` — 20
- `SmokeTests` — 2

В т.ч. новые: `Builder_MultipolygonWithHole_FromFixture`, `Builder_MultipolygonTwoOuterWays_FromFixture`, `Builder_IncompleteMultipolygon_FromFixture`, `Builder_TunnelBridgeLayer_FromFixture`, `Builder_LivingTrackWaterwayBuildingPart_FromFixture`, `Builder_Hash_XmlElementReorder_SameHash`.

## Отклонения / ограничения

- TDD-порядок 1D в git не зафиксирован (см. выше).
- Мостов в данных района нет.
- Railway без `RailwayKind` в модели (только флаги bridge/tunnel).

## Как проверить

1. Меню `ZombieCity/City/Import OSM...` → `CityModel_map.asset`
2. Сцена `Assets/_Project/Scenes/Dev_CityModel.unity` — gizmo плана
3. Test Runner → EditMode → `ZombieCity.City.Core.Tests` → 22 зелёных

## Origin

Все коммиты ветки `task/001-osm-citymodel` (1A…1G) запушены в `origin/task/001-osm-citymodel` (после push 1G: `git status` = синхрон с remote).
