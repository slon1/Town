# REPORT-005 — Layers Viewer: оверлеи и кропы (TASK-005)

- **Ветка:** `task/005-viewer` (база `task/004-rail-fix` @ `e1b8e37`)
- **Исполнитель:** Composer
- **Модель в рапорте:** Composer (Cursor Composer 2.5)

## Что сделано

| Шаг | Статус |
|-----|--------|
| 5A оверлеи + приоритет ADR-003 §5.1, тултип `overlay:` | да |
| 5B экспорт кропа + 4 пресета | да |
| 5C PNG + рапорт | да (скрин окна — ручной, см. ниже) |

Изменён только `Assets/_Project/Scripts/Editor/City/LayersViewerWindow.cs`.

## Core

`git diff --stat` по `Assets/_Project/Scripts/Runtime/City/Core` — **пусто**.

## Тесты

EditMode `ZombieCity.City.Core.Tests`: **121** тест, все зелёные.

## Файлы

| Файл | Назначение |
|------|------------|
| `DOC/reports/img/crop_panke_osloer.png` | пресет Panke / Osloer, 400×400 |
| `DOC/reports/img/crop_prinzenallee_88_49.png` | Prinzenallee (88; 49), 400×400 |
| `DOC/reports/img/crop_courtyard_3583425.png` | двор 3583425, 400×400 |
| `DOC/reports/img/crop_crossing_1192187174.png` | переход (пример osmId), 400×400 |
| `DOC/reports/img/viewer_overlays.png` | **ручной** скрин окна (Crosswalk + Unmarked, слой Access) — положить оператору/архитектору |

**Panke / Osloer:** центр по Waterway `28777932`; воды на кропе нет по дизайну (`tunnel=yes`, ADR-002 — туннель не растеризуется).

## Ограничения

- Оверлей на экране — один по приоритету; в тултипе перечислены все сработавшие условия.
- Пресет «Переход» в UI — любой из 11 osmId; в коммите для приёмки приложен один пример (`1192187174`).