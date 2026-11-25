# Самоприёмка TASK-001 / 1A

- Ветка: `task/001-osm-citymodel`
- Коммиты: `[TASK-001/docs]`, `[TASK-001/1A]`
- Исполнитель: Composer

## Критерии 1A

| Критерий | Результат |
|----------|-----------|
| Проект компилируется | да, ошибок 0 |
| Меню `ZombieCity/City/Import OSM...` | зарегистрировано (`MenuItem` найден); лог `[City] defaults: cellSize=0.5` |
| `CityBakeSettings.asset` | создан; YAML содержит roadWidths/walkwayWidths с дефолтами ADR |
| EditMode smoke-тесты | 2/2 Passed |
| Ровно 2 проектных asmdef | `ZombieCity.City.Core`, `ZombieCity.City.Core.Tests` |

## Замечания

- Bbox в `DOC/data/README.md` взят из `<bounds>` файла; уже, чем оценка в TASK.
- Предупреждения консоли — только сторонние (Zenject/URP shaders), не от нашего кода.
