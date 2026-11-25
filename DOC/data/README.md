# Данные района (OSM)

- **Источник:** экспорт напрямую с [openstreetmap.org](https://www.openstreetmap.org/) (OSM XML 0.6).
- **Файл в проекте:** `Assets/Source/map.osm`
- **Лицензия:** © OpenStreetMap contributors, [ODbL](https://www.openstreetmap.org/copyright).

## Bbox

Из элемента `<bounds>` файла `map.osm`:

| | |
|--|--|
| minlat | 52.5545480 |
| minlon | 13.3776620 |
| maxlat | 52.5567240 |
| maxlon | 13.3826830 |

Примечание: в `DOC/tasks/TASK-001-osm-to-citymodel.md` указан приблизительный bbox ≈ 52.553081, 13.377045, 52.557431, 13.387088 (~680×480 м). Фактические границы файла **уже**, чем эта оценка. Для импорта и отчётов использовать значения из `<bounds>` выше.

## Дата выгрузки

Приблизительно **2026-11-25** (по LastWriteTime файла на диске на момент TASK-001/1A; точная дата экспорта с сайта неизвестна).
