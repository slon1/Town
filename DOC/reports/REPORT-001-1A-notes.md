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

## Git после 1A

```
## task/001-osm-citymodel...origin/task/001-osm-citymodel
 M Assets/Settings/Build Profiles/Android™.asset
 M ProjectSettings/ProjectSettings.asset
?? Assets/Scenes/Game.unity
?? Assets/Scenes/Game.unity.meta
?? ProjectSettings/SceneTemplateSettings.json
```

Коммиты 1A запушены; оставшийся dirty — шум Editor вне объёма 1A (не трогали).

## Asmdef (ровно 2 проектных)

- `Assets/_Project/Scripts/Runtime/City/Core/ZombieCity.City.Core.asmdef`
- `Assets/_Project/Tests/EditMode/ZombieCity.City.Core.Tests.asmdef`

## Замечания

- Bbox в `DOC/data/README.md` взят из `<bounds>` файла; уже, чем оценка в TASK.
- Предупреждения консоли — только сторонние (Zenject/URP shaders), не от нашего кода.
