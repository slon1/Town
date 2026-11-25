# Самоприёмка TASK-001 / 1D

- Ветка: `task/001-osm-citymodel`
- Исполнитель: Composer

## Критерии 1D

| Критерий | Результат |
|----------|-----------|
| Классификация ADR §4 | `CityModelBuilder` |
| Кольца / мультиполигоны | `RingOps`, `MultipolygonAssembler` |
| Здания с дырами на map.osm | **1** из 69 (сверка контура — на gizmo после 1F) |
| Туннели | **1** (`tunnel=yes`; `building_passage` отдельно) |
| Переходы footway=crossing | **11** |
| Мосты | **0 мостов в данных, логика проверена синтетически.** В файле нет `bridge=*`, `bridge:structure`, `man_made=bridge` (только иные `man_made`). Решение оператора: Overpass не заказывать. В P2 — синтетический bridge-кейс в растровых тестах. |
| EditMode-тесты | 20/20 Passed |

## map.osm (после classify)

| Метрика | Значение |
|---------|----------|
| roads / walkways | 32 / 93 |
| buildings / holes | 69 / 1 |
| waterways | **3** (канал Panke и пр.) |
| railways | **4** |
| bridges / tunnels / crossings | 0 / 1 / 11 |
| multipolygons | 1 |
| incomplete MP | **0** (доля **0 %**, порог Overpass >10 % — не превышен) |
| unclosed MP | 0 |
