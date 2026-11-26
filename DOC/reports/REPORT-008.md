# REPORT-008 — Цвета, видимость зданий, отсечение вне сетки

- Ветка / коммиты: `task/008-scene-fixes` от `3003f1a` (`p4-scene-core`). Порядок «тесты → реализация»: `e67ad3b` → `a0ccb0c` (8A), `8b5cf65` → `369b987` (8B). Скриншоты и этот рапорт — следующий коммит.
- Исполнитель (модель): Grok.
- Документы на диске: ADR-004 v1.1, ADR-003 v1.2. Не редактировались.

## Что сделано (по подшагам, с отметкой самоприёмки)

### 8A — цвета. Самоприёмка: да

- `ColorSpaceUtil` в Core, IEC 61966-2-1. Красные тесты: 3 из 3 упали (`Endpoints`, `ControlChannels`, `RoundTrip_AllBytes`). После формулы все 3 зелёные, включая туда-обратно на все 256 байт.
- Земля: `Texture2D(..., linear: false)` — sRGB, `RGBA32`, `Point`, без mip. `SetPixelData` не менялся.
- Здания: в меше по-прежнему `Color32` в sRGB. В фрагментном шейдере `SRGBToLinear` из URP (`Color.hlsl`). Палитра Core и тесты 6D не менялись (6/6 зелёные).
- Фон камеры не трогал. На кадре он уже `#101010` (медиана 5×5 = 16,16,16). Запись линейного значения сделала бы его темнее.

### 8B — `IBuildingsView` и отсечение. Самоприёмка: тесты да, счётчик района 0

- `IBuildingsView` в `Scene/`, `BuildingsView` включает `MeshRenderer`, контекстное меню Buildings/Show и Hide. `CityInstaller` биндит `IBuildingsView` на тот же объект, ссылка записана в `City_Prototype`.
- `BuildingMeshBuilder.Build(model, grid)`: AABB целиком вне `[origin, origin + size·cell]` не попадает в меш (`outsideGrid`). Касание границы и пересечение остаются целиком. Красные тесты: 2 из 6 упали (`WhollyOutside`, `Counter`), затем 6/6.
- Play Mode: `[City] buildings 69 skipped 0 rings 0 outsideGrid 0 verts 4060 tris 2300 ms 117,2`.

### 8C — два здания. Самоприёмка: да. `ADR-change-request` нет

Разбор `Assets/Source/map.osm` тем же парсером, что импорт. Временный скрипт не коммитился.

| | юго-запад, клин | северо-запад |
|---|---|---|
| osmId | 102523696 | 354802441 |
| имя | Kulturwerk des bbk Bildhauerwerkstatt | C-Trakt |
| кольца / вершины модели | 1 / 18 | 1 / 23 |
| ссылки OSM | 19, замкнут (первый = последний) | 24, замкнут |
| пропавшие узлы | 0 | 0 |
| выпуклый внешний контур | нет | нет |
| теги | `building=industrial`, `building:levels=1`, `craft=atelier`, `historic=factory`, `roof:shape=gabled`, источник Hausumringe | `building=school`, `building:levels=2` |
| AABB, м | x −136,4…−24,3, z −168,8…−51,4 | x −113,1…−33,0, z 108,3…148,3 |

Оба пересекают прямоугольник сетки (−170…170, −121…121), поэтому в меше остаются. По всему району зданий с AABB целиком вне сетки — 0. Импорт: `MissingNodeRefs=0`, `MissingWayMemberRefs=0`, `warningMissingRefs=0`, зданий с пропавшими узлами — 0. Контуры не дырявые: невыпуклость есть в самом OSM (Hausumringe). Поведение не менял.

### 8D — кадры. Самоприёмка: да

`capture_game_view`, `source=camera`, 1080×2160. RenderTexture камеры — формат по умолчанию (sRGB в Linear-проекте). Замер — медиана однородного участка 5×5.

Контрольный Game view: фиксированное разрешение 1080×2160 (`Camera.pixelWidth` 1080), `source=screen`, файл `scene_access_gameview.png`.

## Критерии приёмки

| критерий | результат | цифры |
|---|---|---|
| Сборка, два asmdef, Core без UnityEngine | да | ошибок компиляции нет |
| Тесты, порядок тесты → код | да | 8A 3/3 красные затем зелёные; 8B 2/6 красные затем 6/6; 6D 6/6 |
| Цвета ±3 | да | таблица ниже, камера RT ошибка 0 |
| BuildingId при скрытых зданиях, `IBuildingsView` в контейнере | да | `scene_buildingid_v2.png`, поле `buildings` у `CityInstaller` |
| Целиком вне сетки не рисуются; `outsideGrid` > 0 | правило да, число нет | тесты клипа зелёные; на районе `outsideGrid` 0, оба примера пересекают сетку |
| Скриншоты v2, рапорт, `origin` | кадры и рапорт в коммите 8D | push — в том же шаге |

## Цифры

Меш: 69 зданий, пропущено 0, колец 0, `outsideGrid` 0, 4060 вершин, 2300 треугольников, 117,2 мс.

Цвета, медиана 5×5 (ожидание / камера RT / Game view):

| | ожидание | камера RT | Game view |
|---|---|---|---|
| Roadway | `#D04040` | `#D04040` (910,698) | `#CF4040` |
| Pedestrian | `#40C080` | `#40C080` (126,698) | `#40BF80` |
| Obstacle | `#202020` | `#202020` (829,698) | `#202020` |
| крыша Levels/HeightTag | `#C5CCD8` | `#C5CCD8` (213,615) | `#C4CCD6` |
| крыша Default | `#FF8A3D` | `#FF8A3D` (412,660) | `#FE8A3D` |
| фон камеры | `#101010` | `#101010` (290,2) | `#101010` |

Самый тёмный цвет палитры стен `#8F98A6` сверху не виден (стены ребром). Временный квад с тем же шейдером и `Color32(0x8F,0x98,0xA6)` дал ровно `#8F98A6`. Квад не сохранён.

## Отклонения от ТЗ и почему

- Конвертация в шейдере, не в `Color32`: так просил оператор, округление 8 бит в тёмной части не запекается.
- `outsideGrid` на районе 0. Критерий «> 0» опирался на два здания, которые сетку пересекают, а не лежат целиком снаружи. Отсекать пересекающие ADR-004 §4 запрещает.

## ADR-change-request

Нет. Пропавших узлов нет.

## Известные проблемы / ограничения

- Сверху стены не занимают пикселей. Проверка `#8F98A6` сделана квадом, не крышей района.
- Game view на 1–2 единицы темнее RT по некоторым каналам (масштаб окна), всё в допуске ±3.

## Вопросы к архитектору/оператору

Оставляем пересекающие сетку здания целиком, как в ADR-004 §4? Тогда `outsideGrid` для этого района честно 0. Если «клин вне карты» надо обрезать по прямоугольнику — это уже другая геометрия, без правки ADR не делал.

## Как проверить

1. Сцена `Assets/_Project/Scenes/City_Prototype.unity`, Play Mode. В консоли строка `[City] buildings ... outsideGrid 0`.
2. На Ground: Layer/Access, Flags, BuildingId. На Buildings: Buildings/Hide — под крышами виден слой id.
3. Кадры: `DOC/reports/img/scene_access_v2.png`, `scene_flags_v2.png`, `scene_buildingid_v2.png`, `scene_default_buildings_v2.png`, контрольный `scene_access_gameview.png`.
4. EditMode: `ColorSpaceUtilTests`, `BuildingClipTests`.

## git log --stat

```
e67ad3b [TASK-008/8A-tests] красные тесты ColorSpaceUtil
 ColorSpaceUtil.cs          | 18 +
 ColorSpaceUtilTests.cs     | 35 +
 2 files, 53 insertions

a0ccb0c [TASK-008/8A] sRGB-текстура земли и SRGBToLinear в шейдере зданий
 ColorSpaceUtil.cs          | 51 +-
 ColorSpaceUtil.cs.meta     |  2 +
 GroundView.cs              |  3 +-
 BuildingsVertexColor.shader|  5 +-
 ColorSpaceUtilTests.cs.meta|  2 +
 5 files, 58 insertions, 5 deletions

8b5cf65 [TASK-008/8B-tests] красные тесты отсечения зданий вне сетки
 BuildingMeshBuilder.cs     |  9 +
 BuildingClipTests.cs       | 97 +
 2 files, 106 insertions

369b987 [TASK-008/8B] отсечение зданий вне сетки и IBuildingsView
 City_Prototype.unity       |  1 +
 BuildingMeshBuilder.cs     | 54 +-
 BuildingsView.cs           | 48 +-
 CityInstaller.cs           |  2 +
 IBuildingsView.cs          | 11 +
 IBuildingsView.cs.meta     |  2 +
 BuildingClipTests.cs.meta  |  2 +
 7 files, 111 insertions, 9 deletions
```
