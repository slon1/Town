# REPORT-006 — сцена: DI, меш зданий, земля со слоями

- Ветка / коммиты: `task/006-scene-core` от `main` `4219709` (M1 влит fast-forward `0df03d3..4219709` и запушен). Подшаги ниже. Рапорт — коммит следом за `5f7f1f3`.
- Исполнитель (модель): Grok 4.7
- Версии: ADR-001 v1.4, ADR-002 v1.2, ADR-003 v1.2, ADR-004 v1, TASK-006. ADR и TASK не редактировались. Чекбокс P4 не ставился.

## Что сделано (по подшагам, с отметкой самоприёмки)

- 6A. Сцена `City_Prototype`: `SceneContext` + тестовый installer. Play Mode, Unity 6000.5.9f1, URP, Extenject 9.2: лог `[City] DI ok`, ошибок нет, кадры шли. Самоприёмка. В 6F installer и проба удалены.
- 6B. `Area.heightSource`. Красные тесты `f09225f` (4 из 6 упали; `HeightTag` совпал с нулём enum, сериализация прошла сразу, как только поле появилось). Реализация `cd56988`. На районе: HeightTag 0, Levels 57, Default 12 (69 зданий). `roof:levels` без `building:levels` — `Levels`, как строка REPORT-001. Самоприёмка.
- 6C. Исходники LibTessDotNet в `Core/ThirdParty/LibTessDotNet/` (без asmdef, без UnityEngine). Тест площади квадрата с дырой: 100 − 4 = 96. Самоприёмка.
- 6D. `BuildingMeshBuilder`. Красные тесты `4cd3e49` (5 из 6; детерминизм на пустой заглушке совпал). Реализация `bd79560`, повтор после намотки в `5f7f1f3` — 6/6. Самоприёмка.
- 6E. `LayerComposer`. Красные тесты `54ecae4` (5/5). Реализация `a176ca6` — 5/5. Самоприёмка.
- 6F. `GroundView`, `BuildingsView`, `CityInstaller`, ортокамера pitch 90°. Сцена играется, земля Access и крыши видны, смена слоя контекстным меню обновляет текстуру, `Default` оранжевые. Самоприёмка.
- 6G. Скриншоты и этот рапорт. Самоприёмка.

## Критерии приёмки

| Критерий | Результат | Цифры |
|---|---|---|
| Extenject smoke | да | `[City] DI ok`, 0 ошибок |
| heightSource района | да | 0 / 57 / 12 |
| LibTess в Core, дыра | да | площадь 96 |
| Меш: крыша +Y, стены наружу, без дна | да | тесты + крыши видны сверху |
| Слои и оверлеи | да | 5 тестов; скриншоты |
| Default оранжевые | да | `scene_default_buildings.png` |
| Север сверху, текстура не зеркалится | да | up камеры +Z, строка 0 = min j; коридор горизонтальный |
| asmdef по-прежнему два | да | Core и EditMode-тесты |

## Цифры

Меш района: зданий 69, пропущено 0, колец пропущено 0, вершин 4060, треугольников 2300, построение 95,1 мс (лог `[City] buildings ...` на старте сцены).

Смена слоя Access → BuildingId: `GC.GetTotalMemory` до/после = 0 байт managed. Буфер `uint[]` и `Texture2D` выделяются один раз. `SetPixelData` копирует в натив, это не mono-heap.

| хэш | было | стало |
|---|---|---|
| modelDataHash | `0xD000205361689643` | `0xBE60B87FBF94BC4C` |
| settingsHash | `0xBAB661E880FB0E7C` | `0xBAB661E880FB0E7C` |
| FixtureDataHash | `0x7C5E533F40D6361B` | `0x8145898624330C7B` |

`dataHash`/`accessHash` бейка не менялись: растр не перепекался.

## git log --stat

Порядок тестов → реализация. База `4219709`.

```
f4d375f [TASK-006/6A] smoke-check Extenject
  7 files changed, 396 insertions(+)

f09225f [TASK-006/6B-tests] красные тесты heightSource
  CityEnums.cs, CityModelTypes.cs, HeightSourceTests.cs
  4 files changed, 139 insertions(+)

cd56988 [TASK-006/6B] heightSource
  CityModel_map.asset, Builder, Hasher, TagValueParser, CityModelHashTests
  5 files changed, 124 insertions(+), 8 deletions(-)

9f98ce4 [TASK-006/6C] LibTessDotNet
  8 исходников + LICENSE + LibTessHoleTests
  22 files changed, 4234 insertions(+)

4cd3e49 [TASK-006/6D-tests] красные тесты экструзии
  4 files changed, 322 insertions(+)

bd79560 [TASK-006/6D] экструзия
  BuildingMeshBuilder.cs  1 file, +290/−2

54ecae4 [TASK-006/6E-tests] красные тесты композитора
  6 files changed, 181 insertions(+)

a176ca6 [TASK-006/6E] композитор
  LayerComposer.cs  1 file, +170/−1

5f7f1f3 [TASK-006/6F] сцена
  GroundView, BuildingsView, CityInstaller, CityOverviewCamera,
  шейдеры, материалы, удалены smoke-скрипты, намотка меша
  27 files changed, 891 insertions(+), 81 deletions(-)
```

## Отклонения от ТЗ и почему

- `CityLayer` и `CityOverlay` лежат в Core, не в `Scene/`. Композитор и EditMode-тесты не могут ссылаться на `Assembly-CSharp`. `ILayerView` в сцене использует эти же типы. Значения и имена — ADR-004 §6.
- Лицензия вендора — SGI Free Software License B из заголовков файлов. ADR-004 §9 пишет MIT. Файл `LICENSE.txt` повторяет заголовок исходников.
- Куб 10×10×10 без дна — 10 треугольников (крыша 2 + стены 8) и 20 вершин, не 12. 12 было бы с полом.
- Проход шейдера без `LightMode`: в этом URP рисуется `SRPDefaultUnlit`, тег `UniversalForward` кадр не рисовал. Границы плоского квада расширены на 1 м по Y, иначе рендер его отсекал.

## ADR-change-request

Нет. Контракты не менялись. Расхождение MIT/SGI и место enum — в отклонениях, работа продолжена.

## Известные проблемы / ограничения

- Камера только кадрирует карту (pitch 90°, ширина под 1080×2160). Жестов и UI нет — TASK-007.
- При ландшафтном Game View карта не на весь кадр: orthographicSize посчитан под портрет 0,5.

## Вопросы

Нет.

## Как проверить

Сцена `Assets/_Project/Scenes/City_Prototype.unity`. Play: в консоли строка `[City] buildings 69 skipped 0 ...`. На объекте `CityRoot/Ground` контекстное меню Layer/Access, Layer/Flags, Layer/BuildingId. Крыши `Default` оранжевые.

PNG 1080×2160: `DOC/reports/img/scene_access.png`, `scene_flags.png`, `scene_buildingid.png`, `scene_default_buildings.png`.
