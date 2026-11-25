# Самоприёмка TASK-001 / 1E

- Ветка: `task/001-osm-citymodel`
- Исполнитель: Composer

## Критерии 1E

| Критерий | Результат |
|----------|-----------|
| Меню → `CityModel_map.asset` | да, `Assets/_Project/City/Baked/CityModel_map.asset` |
| Перезапись с сохранением GUID | да (`guidStable=True`) |
| Progress bar | да |
| Сводка `[City]` | полный блок (bbox, классы, длины, предупреждения, хэши) |
| Хэш стабилен (2 прогона) | **да** `0x4ED5D7918533F31B` = `0x4ED5D7918533F31B` |
| Время импорта | **~0,47 с** (Editor; цель &lt; 10 с) |

## Цифры импорта map.osm

| | |
|--|--|
| buildings / holes | 69 / 1 |
| roads / walkways | 32 / 93 |
| waterways / railways | 3 / 4 |
| bridges / tunnels / crossings | 0 / 1 / 11 |
| incomplete MP | 0 (0 % от 1 multipolygon; порог Overpass >10 % не достигнут) |
| dataHash | `0x4ED5D7918533F31B` |

## Мосты (из 1D, решение A)

0 мостов в данных (`bridge=*`, `bridge:structure`, `man_made=bridge` отсутствуют), логика проверена синтетически.

## Среда

- Unity 6000.5.9f1, Editor
- CPU: Intel Core i7-3770 @ 3.40 GHz (4 cores)
- Code Optimization: **Debug**
- Время: ~465 ms на импорт (parse+classify+write)
