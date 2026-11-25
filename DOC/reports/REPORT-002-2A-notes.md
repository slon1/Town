# Самоприёмка TASK-002 / 2A

- Ветка: `task/002-rasterization`
- Исполнитель: Composer

## Критерии 2A

| Критерий | Результат |
|----------|-----------|
| `CityGrid` + конвертации / InBounds / FromBBox | да |
| `CellFlags : ushort` с `Shared = 1<<14` | да |
| `railwayWidth=3`, `gateRadius=1.5` в config + CanonicalBytes | да |
| Золотые хэши обновлены | да (см. таблицу) |
| sidewalkMode vs ADR-002 §11 | совпадает, ADR-change-request не нужен |
| Тесты CityGrid зелёные | да |

## sidewalkMode (сверка P1)

`TagValueParser.ParseSidewalkMode`: пусто/`no`/`none` → None; `left`/`right`/`both`/`yes` → Left/Right/Both; иначе Unknown. Совпадает с ADR-002 §11.

## Хэши настроек (было → стало)

| | Было (P1) | Стало (2A) |
|--|-----------|------------|
| `CityBakeConfig` default `settingsHash` | `0x24472141C5F413C9` (из `CityModel_map.asset`) | `0x09A4C90D0BFD89D7` |
| `FixtureDataHash` (модель с фиксированным settingsHash) | `0x7C5E533F40D6361BUL` | без изменений |
| `CityModel_map` dataHash | `0x4ED5D7918533F31B` | `0x6863E96E666A2A2A` (из-за settingsHash в meta) |

Тесты: **29 Passed / 0 Failed**.

## Эталон двора P2

osmId **`3583425`** — см. `DOC/reports/img/courtyard_building_3583425.png`.
