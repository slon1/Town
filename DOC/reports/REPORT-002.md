# REPORT-002 — CityModel → растры (TASK-002 / P2)

- Ветка / коммиты: `task/002-rasterization` (на `origin`)
- Исполнитель: Composer; 2G — независимое ревью ([2G independent raster review](7a6e786c-54d0-4d64-8366-edc0c288dc62))

## Что сделано

| Шаг | Статус | Коммит |
|-----|--------|--------|
| docs ADR/TASK | да | `9ea2c7d`, `fd7bbaf` (v1.1 water) |
| 2A CityGrid/CellFlags | самоприёмка | `a55d5f2` |
| 2B-tests → 2B | самоприёмка, test-first | `676992f` → `4dc9417` |
| 2C-tests → 2C | самоприёмка, test-first | `d1f0325` → `effcc99` |
| 2D Bake asset | самоприёмка | `161e156` |
| 2E PNG | самоприёмка | `c079d55` |
| 2G review | **принять** | `d7a8277` |

## Критерии DoD

| # | Результат |
|---|-----------|
| 1 asmdef×2, Core noEngine | да |
| 2 тесты зелёные; test-first 2B/2C в git | **72 Passed / 0 Failed**; порядок тесты→код соблюдён |
| 3 dataHash стабилен | `0x6B317D7D36FA2A8C` = `0x6B317D7D36FA2A8C` |
| 4 сводка bake | ниже |
| 5 сверка площадей | здания: vector 23020 м² vs raster 18040 м² (**−21,6 %**); дороги len·width 10435 vs Road 8137 м². Занижение mode A (центры) + clip bbox + перекрытия у дорог. Порог ±2 % для зданий **не выполнен** — на решение архитектора. |
| 6 PNG | `raster_flags.png`, `raster_buildings.png` — двор 3583425 виден |
| 7 REVIEW-002 | **принять** (18/18 edge-case) |
| 8 рапорт + push | да |

## Хэши настроек (P1 → P2)

| | Было | Стало |
|--|------|-------|
| settingsHash default | `0x24472141C5F413C9` | `0x09A4C90D0BFD89D7` |
| map model dataHash | `0x4ED5D7918533F31B` | `0x6863E96E666A2A2A` |
| bake dataHash | — | `0x6B317D7D36FA2A8C` |

## Сводка bake (map.osm)

| | |
|--|--|
| Сетка | **680×484**, cell=0,5 м |
| Время | 1283 ms / 823 ms (Editor Debug) |
| buildings | 69 / zeroCells=0 / conflicts=0 |
| underBridges | 0 (в данных нет `isBridge`; логика — синтетика) |
| waterClearedByLinear | **28** |
| Road / Building / Water cells | 32547 / 72161 / 6027 |

Эталон двора: osmId **3583425** — дыра без `Building` / `buildingId=0`.

## PNG

- [`img/raster_flags.png`](img/raster_flags.png)
- [`img/raster_buildings.png`](img/raster_buildings.png)

## 2G

`DOC/reports/REVIEW-002.md` — вердикт **принять**; 18 новых тестов, 0 открытых расхождений с ADR.

## git log --stat (main..HEAD)

```
d7a8277 [TASK-002/2G] Независимое ревью: RasterReview002Tests и REVIEW-002
c079d55 [TASK-002/2E] PNG-экспорт слоёв raster_flags/raster_buildings
161e156 [TASK-002/2D] CityBakeAsset, Bake Grid меню и ICityMap
effcc99 [TASK-002/2C] CityRasterizer: слои ADR-002, waterClearedByLinear, buildingId
d1f0325 [TASK-002/2C-tests] красные тесты CityRasterizer по ADR-002 §4/§8
4dc9417 [TASK-002/2B] примитивы растеризации A/B: even-odd, capsule, barrier
676992f [TASK-002/2B-tests] красные тесты примитивов растеризации A/B по ADR-002 §8
fd7bbaf [TASK-002/docs] ADR-002 v1.1: дорога/путь/рельсы снимают Water
a55d5f2 [TASK-002/2A] CityGrid, CellFlags и railwayWidth/gateRadius в bake-config
9ea2c7d [TASK-002/docs] ADR-002 и TASK-002 растеризации
```

Полный `--stat`: `git log --stat main..HEAD` на `task/002-rasterization`.

## Отклонения

- Площадь зданий raster vs vector −21,6 % (> ±2 % DoD).
- Мостов в районе 0.
