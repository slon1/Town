# ADR-001 — Офлайн-запекание города из OSM в модель и растровые слои

Статус: **Принято** (v1.3: `Railway.kind`). Охватывает фазы P1–P3; контракты P4+ уточняются отдельными ADR.

Область: только загрузка и разметка города. Агенты, поля управления и фреймворк частиц **вне объёма** (решение оператора).

## 1. Контекст

Игра — симуляция десятков/сотен тысяч частиц на полях и сетке. Городу нужны проходимость, семантика поверхности и растры, а не NavMesh и коллайдеры. Источник данных — `.osm` (OpenStreetMap, лицензия ODbL), выгруженный Overpass/openstreetmap.org. Районы небольшие (порядка 0,5–1 км).

## 2. Решение

Двухступенчатое запекание в Editor:

```
.osm  ──(P1)──►  CityModel (векторная модель, метры)  ──(P2)──►  CityBake (растры)
                      │                                              │
                      └──► меш зданий, gizmo (P4/P1)                 └──► текстура земли, поля (P4–P6)
```

Рантайм не читает `.osm` и не зависит от парсера. Он использует только ассеты `CityModelAsset` и `CityBakeAsset`.

Отвергнуто: Mapbox SDK (онлайн-тайлы, лимиты, не детерминировано); NavMesh/коллайдеры для симуляции (не масштабируется на 200k частиц); построение мешей дорог (лишнее — дороги живут в растре).

## 3. Система координат и единицы

- Единицы — **метры**. Мир Unity: `X` = восток, `Z` = север, `Y` = вверх. Север на карте = +Z.
- Проекция: локальная равнопромежуточная от центра bbox `(lat0, lon0)`:
  `x = R · (lon − lon0)·π/180 · cos(lat0·π/180)`, `z = R · (lat − lat0)·π/180`, `R = 6 371 008.8 м`. Вычисления в `double`.
- Bbox берётся из `<bounds>` файла, если есть; иначе — по узлам. В настройках импорта допустим явный crop-bbox.
- Мастер-сетка `CityGrid`: `origin` (метры, левый нижний угол, выровнен вниз до кратного `cell`), `cell` (по умолчанию **0,5 м**, параметр настроек), `w`, `h`. Индекс клетки `i + j·w`, `i` вдоль X, `j` вдоль Z. Центр клетки: `origin + (i+0.5, j+0.5)·cell`.

## 4. CityModel (P1)

Плоская сериализуемая модель (`[Serializable]`, `ScriptableObject` `CityModelAsset`). **Unity не сериализует `List<List<T>>` и полиморфизм**, поэтому: плоские массивы точек + массивы смещений (`ringStart[]`), без вложенных коллекций.

Содержимое:

| Сущность | Поля |
|----------|------|
| `Meta` | источник (имя файла), bbox lat/lon, `lat0/lon0`, размер в метрах, хэш настроек, хэш данных |
| `Road` (линия) | `osmId`, `HighwayClass`, `widthM` (разрешённая), `layer`, `isBridge`, `isTunnel`, `isBuildingPassage`, `isShared`, `sidewalkMode` (None/Left/Right/Both/Unknown), точки `Vec2d[]` |
| `Walkway` (линия) | `osmId`, `WalkwayKind` (Footway, Sidewalk, Crossing, Path, Steps, Cycleway, Pedestrian), `widthM`, `layer`, `isBridge`, `isTunnel`, `isBuildingPassage`, точки |
| `Area` (полигон с дырами) | `osmId`, `AreaKind` (Building, Water, Green, PedestrianArea, Other), кольца (outer CCW, holes CW), для зданий: `heightM`, `minHeightM`, `buildingType` (string) |
| `Barrier` (линия) | `osmId`, `BarrierKind` (Wall, Fence, Hedge, Other, Gate-узлы отдельно), точки |
| `Railway` (линия) | `osmId`, `RailwayKind` (Rail, LightRail, Subway, Tram, NarrowGauge, Other), `isBridge`, `isTunnel`, `layer`, точки |
| `Waterway` (линия) | `osmId`, `WaterwayKind` (Canal, River, Stream, Other), `isTunnel` (в т. ч. culvert), точки |
| `PointFeature` | `osmId`, `PointKind` (CrossingNode, TrafficSignal, Gate, Entrance), позиция |
| `Stats` | счётчики по классам, длины, предупреждения (сводка) |

Правила интерпретации тегов (значения по умолчанию лежат в `CityBakeSettings`, не в коде):

- **Дорога для транспорта** (`Road`): `highway` ∈ motorway, trunk, primary, secondary, tertiary, unclassified, residential, living_street, service и `*_link`. Ширина: тег `width` (парсить «6», «6.5», «6 m») → иначе `lanes · 3.0` → иначе по таблице: primary 10, secondary 9, tertiary 8, residential 6.5, living_street 5, service 4, link 5, прочие 6. `living_street` получает `isShared = true`.
- **Пешеходное** (`Walkway`): `highway` ∈ footway (включая `footway=sidewalk|crossing`), path, pedestrian, steps, cycleway, track (кроме `track` с `motor_vehicle=yes|designated` — он `Road`, ширина 3.0). Ширина: `width` → иначе footway 2.0, sidewalk 2.0, crossing 3.0, path 1.5, pedestrian 4.0, steps 2.0, cycleway 2.0, track 3.0. `highway=pedestrian` с `area=yes` — это `Area(PedestrianArea)`.
- **Мосты/туннели**: `bridge=yes|viaduct|*` (кроме `no`) → `isBridge`; `tunnel=yes|culvert` → `isTunnel`; **`tunnel=building_passage`** → `isBuildingPassage` (не туннель: проход сквозь здание). Также `covered=yes` для линий — игнорируется. `layer` — целое, по умолчанию 0.
- **Здания**: `building` ≠ `no`. Высота: `height` (парсить число, «m», допускается запятая как десятичный разделитель) → иначе `building:levels · 3.0` + `roof:levels · 1.5`(необязательно, из настроек) → иначе `defaultBuildingHeight` (12 м). Минимум 3 м. `building:part` пока игнорируется (учитывать счётчиком).
- **Зелень**: `leisure` ∈ park, garden, playground, pitch; `landuse` ∈ grass, forest, meadow, village_green, recreation_ground; `natural` ∈ wood, scrub, grassland. **Вода**: `natural=water`, `waterway=riverbank`; линии `waterway=canal|river|stream` хранятся как `Waterway`; в P2 растеризуются шириной `waterwayWidth` (по умолчанию 4 м; осознанное упрощение); `tunnel`/`culvert` → `isTunnel`, в растр не попадают.
- **Барьеры**: `barrier` ∈ wall, fence, hedge, retaining_wall, guard_rail, city_wall, ... (список в настройках). Узлы `barrier=gate|entrance|lift_gate|bollard` — `PointFeature` (ворота открывают проход; линия барьера в модели **не разрезается**, проём делается только в растре, бит `Gate`).
- **Железная дорога**: `railway` ∈ rail, light_rail, subway, tram, narrow_gauge (список в настройках); `subway` и туннели уходят в `isTunnel` и в растр не попадают. Значение тега сохраняется в `RailwayKind` (в канонических байтах — `int32`, сразу после `osmId`).
- Остальные теги игнорируются; игнорируемые объекты не логируются поштучно.
- **Один way — одна сущность по смыслу тега `highway`.** Значение `highway` однозначно определяет `Road` либо `Walkway`; теги доступа (`foot`, `bicycle`, `motor_vehicle`, `access`) в P1 **не учитываются** (кроме исключения для `track`). Way с `building` даёт `Area`, независимо от наличия других тегов; `barrier`/`railway`/`waterway` — свои сущности; один way может породить несколько сущностей разных видов, но не две сущности одного вида.
- `sidewalk=*`, `cycleway=*` на дороге: только `sidewalkMode`; отдельные `Walkway` не создаются.
- `building:part` игнорируется (счётчик в статистике); part без родительского `building` не создаёт здание.
- Формат входа: **только OSM XML 0.6**. `.pbf`, `.o5m`, osmChange не поддерживаются.

Геометрия:
- Кольца замкнуты, без дублирующих подряд точек; outer — против часовой стрелки, holes — по часовой (в проекции X-восток/Z-север).
- **Мультиполигоны** (`type=multipolygon`): сшивать `outer`-ways в замкнутые кольца (учитывая разные направления и порядок), `inner` присваивать содержащему outer (point-in-polygon по вершине/точке внутри). Незамкнутые или с отсутствующими членами — пропускать с предупреждением и счётчиком; не падать.
- Теги здания берутся с relation, если на relation есть `building`; иначе с outer-way (стандартный случай «старых» мультиполигонов).
- Way-здание, являющееся единственным outer мультиполигона с тегом на relation, не дублируется.

Сборки: типы модели и алгоритмы лежат в `asmdef` `ZombieCity.City.Core` (без UnityEngine); `[Serializable]`-классы из Core сериализуются Unity штатно в `CityModelAsset`, расположенном в `Assembly-CSharp`.

Детерминизм: все выходные массивы сортируются по `(вид сущности, osmId)`. Хэш данных — **FNV-1a 64** по каноническому байтовому представлению:
- порядок: `Meta` (без времени и путей) → сущности по видам в порядке перечисления в таблице выше → внутри вида по `osmId`; поля в порядке объявления;
- целые — little-endian (`int32`/`int64`), `bool` — 1 байт, enum — `int32`;
- координаты — **целые миллиметры**: `round(value · 1000)` в `int64` (устраняет различия `Math.Cos` между платформами);
- строки — UTF-8 с префиксом длины `int32`;
- массивы — префикс длины `int32`, затем элементы.
Хэш настроек считается аналогично по `CityBakeConfig.CanonicalBytes()` — явный список семантических полей (без GUID, имён ассетов и данных Inspector); таблицы-словари сортируются по ключу.

## 5. CityBake — растры (P2)

Хранится в `CityBakeAsset` (`ScriptableObject`): `CityGrid`, `byte[] flags` (ushort little-endian, размер `w·h·2`), `byte[] buildingId` (ushort LE, 0 = нет), таблица зданий (id → osmId, высота, тип, bbox в клетках), метаданные и хэши модели/настроек. Для масштаба прототипа (≈ 1360×960 при 0,5 м) хранение в `byte[]` внутри SO допустимо; при росте карты — отдельный ADR.

### 5.1. CellFlags (ushort, биты — контракт)

| Бит | Имя | Смысл |
|-----|-----|-------|
| 0 | `Road` | Проезжая часть |
| 1 | `Sidewalk` | Явный тротуар / пешеходная дорожка / пешеходная зона / лестница |
| 2 | `Crosswalk` | Пешеходный переход (может совмещаться с `Road`) |
| 3 | `Building` | Здание (непроходимо) |
| 4 | `Water` | Вода |
| 5 | `Barrier` | Барьер (непроходимо) |
| 6 | `Rail` | Железнодорожное полотно |
| 7 | `Green` | Парк/газон/лес |
| 8 | `Bridge` | Клетка принадлежит мосту (слой ≥ 1) |
| 9 | `SidewalkGenerated` | Тротуар, сгенерированный по тегу `sidewalk=*` (приближение) |
| 10 | `BuildingPassage` | Проход сквозь здание (снимает `Building`) |
| 11 | `Cycleway` | Велодорожка (проходима для пешеходов; бит не заменяет `Sidewalk`) |
| 12 | `Steps` | Лестница |
| 13 | `Gate` | Проём в барьере (ворота/вход) |
| 14 | `Shared` | Смешанная улица (`isShared`, `living_street`): вместе с `Road` даёт класс доступа `Pedestrian` |
| 15 | резерв | |

Клетка без битов = «открытое место» (двор, площадь без разметки): **проходимо по умолчанию**; запретом служат только `Building`, `Water`, `Barrier`, `Rail` (если не перекрыты `Bridge`/`Gate`/`BuildingPassage`).

### 5.2. Порядок наложения (низкий → высокий приоритет)

1. `Green` → 2. `Water` → 3. `Road` → 4. `Sidewalk`/`Cycleway`/`Steps` (явные; бит `Road` в клетке не снимают — разрешение конфликта в классах доступа, §5.4) → 5. `Crosswalk` (поверх `Road`, оба бита остаются) → 6. `SidewalkGenerated` (только в клетки без `Building/Water/Barrier/Rail`, и не дальше `sidewalkWidth` от полотна, и не там, где в радиусе `sidewalkSearchRadius` уже есть явный `Sidewalk`) → 7. `Rail` → 8. `Building` (снимает `Road`, `Sidewalk`, `Green`, но не клетки мостов и `BuildingPassage`) → 9. `BuildingPassage` (снимает `Building`, добавляет `Sidewalk`-подобную проходимость) → 10. `Barrier` → 11. `Gate` (снимает `Barrier` в клетке) → 12. Мосты (layer ≥ 1, `isBridge`): записываются поверх, **снимают** `Water`, `Building`, `Road`/`Sidewalk` нижнего слоя, ставят свой класс + `Bridge`.

Туннели (`isTunnel`) в растр поверхности **не попадают**. 2D-ограничение осознанное: информация о том, что лежит под мостом, теряется; в статистике P2 выводится число клеток под мостами.

### 5.3. Растеризация

- Линии — «толстые» (capsule / расстояние до отрезка ≤ width/2), **консервативные**: минимальная ширина — одна клетка, диагональные тонкие линии не должны пропускать 8-соседей (иначе барьеры «текут»).
- Полигоны — scanline с правилом even-odd по всем кольцам (дыры обрабатываются естественно). Без триангуляции.
- Порядок обхода объектов детерминирован (по `osmId`).
- Все параметры (ширины, радиусы) — в `CityBakeSettings`.
- Дорога с `isShared` ставит вместе с `Road` бит `Shared`.

### 5.4. Классы доступа (P3)

Производный растр `access` (`byte[]`, один байт на клетку) вычисляется из `flags`; хранится в `CityBakeAsset`.

| Значение | Имя | Правило (проверять сверху вниз) |
|----------|-----|---------------------------------|
| 0 | `Obstacle` | есть `Building` / `Water` / `Barrier` / `Rail` и нет перекрывающих `Bridge` / `Gate` / `BuildingPassage` |
| 1 | `Roadway` | есть `Road` и нет `Sidewalk` / `SidewalkGenerated` / `Crosswalk` / `Steps` / `Cycleway` / `Shared`. **Доступна только зомби** (в будущем) |
| 2 | `Pedestrian` | всё остальное проходимое: тротуары, переходы (в т. ч. поверх `Road`), велодорожки, лестницы, проходы, мосты-пешеходные, зелень, открытые места |

Конфликт «`Road` + явный `Sidewalk`» (перекрытие полотна и тротуара из-за ошибок ширины) разрешается в пользу `Pedestrian`. Мост с `Road` — `Roadway`. **Принято решение:** пешеходный переход — зона пешеходов (`Pedestrian`), хотя под ним `Road`; велодорожка — `Pedestrian`.

Разметка зон — **основной визуальный результат M1**: окно просмотра красит `Roadway` / `Pedestrian` / `Obstacle` разными цветами.

## 6. Контракты рантайма (вводятся в P2, используются позже)

- `readonly struct CityGrid` (origin, cell, w, h, конвертации мир↔клетка, `InBounds`).
- `interface ICityMap { CityGrid Grid {get;} CellFlags GetFlags(int i, int j); ushort GetBuildingId(int i, int j); }` + реализация поверх `CityBakeAsset`. Регистрация в Extenject — в P4.

## 7. Последствия

- (+) Рантайм простой и быстрый; один источник правды для сцены, полей и симуляции.
- (+) Смена района = смена `.osm` + перезапуск bake.
- (−) Тротуары по тегам `sidewalk=*` — приближение; многоуровневость (мосты) упрощена в 2D.
- (−) Качество зависит от полноты OSM; в отчёте bake показываются счётчики предупреждений.

## 8. Лицензия

Данные OSM — ODbL. В прототипе достаточно указать «© OpenStreetMap contributors» в `DOC/data/README.md` и в About/титрах позже.
