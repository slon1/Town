# Самоприёмка TASK-001 / 1F

- Ветка: `task/001-osm-citymodel`
- Исполнитель: Composer

## Критерии 1F

| Критерий | Результат |
|----------|-----------|
| `CityModelGizmoViewer` без Editor API | да |
| DrawGizmo drawer + палитра TASK | да (`CityModelGizmoDrawer`) |
| Сцена `Dev_CityModel.unity` | создана через Editor API (MCP/CLI eval), объект `CityModelViewer` + ссылка на `CityModel_map.asset` |
| Север = +Z | да (вид Top: синяя ось вверх) |
| Масштаб vs bbox | sizeXM≈339 м, sizeZM≈242 м — совпадает с meta модели |
| Форма района | узнаваемый план (застройка, канал, рельсы) |
| Скриншот | `DOC/reports/img/Dev_CityModel_gizmo.png` |

## Здание с дырой (из 1D)

На скриншоте у центра (под transform gizmo) виден прямоугольный контур здания с внутренним кольцом — похоже на двор, не на ошибочную сшивку.
