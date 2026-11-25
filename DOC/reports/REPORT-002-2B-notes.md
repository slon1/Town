# Самоприёмка TASK-002 / 2B

- Ветка: `task/002-rasterization`
- Исполнитель: Composer

## Порядок test-first (git)

| Коммит | Содержание |
|--------|------------|
| `676992f` `[TASK-002/2B-tests]` | красные тесты + stub `NotImplementedException` |
| (этот) `[TASK-002/2B]` | `RasterPrimitives` A/B |

Перед реализацией: **10/10 Failed**. После: **10/10 Passed**.

## Критерии 2B

| Критерий | Результат |
|----------|-----------|
| Fill even-odd (scanline по центрам) | да |
| Capsule line режим A (`halfWidth=max(w/2,cell/2)`) | да |
| Capsule line режим B (пересечение с клеткой) | да |
| Площадь / even-odd / линии §8 | да |
| Барьер ≥ 200 random seed=42 без 8-утечек | да |

## API

`RasterPrimitives.FillPolygonEvenOddA` / `StampPolylineCapsuleA` / `StampPolylineCapsuleB` — операции `ushort[]`, индекс `i+j·w`.
