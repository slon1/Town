# Самоприёмка TASK-001 / 1C

- Ветка: `task/001-osm-citymodel`
- Исполнитель: Composer

## Критерии 1C

| Критерий | Результат |
|----------|-----------|
| XmlReader-парсер OSM 0.6 | да (`OsmXmlParser`) |
| Проекция ADR §3 | да (`LocalEquirectangularProjection`) |
| `map.osm` без исключений | nodes=2145 ways=304 relations=18 multipolygons=1, ~90 ms |
| Лог `[City]` со счётчиками | `ImportOsmMenu.ParseAndLog` |
| EditMode-тесты | 12/12 Passed (проекция, parse, map.osm) |

## Замечания

- Новый файл `ProjectionAndParseTests.cs` Unity не включил в asmdef (не попал в `.rsp`); тесты 1C влиты в `CityModelHashTests.cs`.
