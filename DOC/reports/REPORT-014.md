# REPORT-014 — трамвайные пути в растре и на сцене

- Ветка / коммиты: `task/014-tram-raster`, `54878ae`…`a221e17` (рапорт — следующий коммит). База `main` `324c388`.
- Исполнитель (модель): Grok
- Версии, с которыми делалась работа: ADR-001 v1.5, ADR-002 v1.3, ADR-003 v1.3, ADR-004 v1.2. `AccessClassifier.cs` не менялся (`git diff` пустой).

## Что сделано (по подшагам, с отметкой самоприёмки)

- **14A.** `CellFlags.Tram = 1 << 15`. `tramTrackWidth = 2.4` записан в `CanonicalBytes` сразу после `railwayWidth` и выставлен в `CityBakeSettings.asset`. Золотой `settingsHash` обновлён. В Core, Scene и Editor нет приведений флагов к `short`; `~` и `&` идут через `ushort`. Тест гоняет все 16 битов через `byte[]` little-endian и `ClassifyAll`. Самоприёмка: да.
- **14C, тесты классификатора.** Коммит `4dee801` стоит до растеризации. Четыре теста были зелёными сразу: классификатор не смотрит на бит Tram. Самоприёмка: да.
- **14B.** Красный прогон: 5 из 6 тестов упали, `TunnelTram_Skipped_NotStamped` уже был зелёным (туннель и раньше не рисовался). Затем штамп режима A сразу после Railway и до дорог. Дорога и переход Tram не снимают, здание снимает, мост не входит в маску очистки Tram. Трамвай воду не снимает. Самоприёмка: да.
- **14D.** `CityOverlay.Tram` в конце enum (`1 << 5`). Flags `#9A4DFF` ниже Crosswalk и выше Road/Sidewalk/Green. Оверлей `#B060FF`, приоритет 5, Bridge остаётся 6. Четыре теста были красными до `LayerComposer`, затем зелёные. Палитры Access и BuildingId не менялись. Самоприёмка: да.
- **14E.** У `GroundView` поле `initialOverlays`, в `City_Prototype` записано 32 (Tram). На старте Play Mode `GetOverlay(Tram) == true`. Панель не редактировалась. В Play Mode пройдены слои Access, Flags, BuildingId, тогл зданий и пять оверлеев панели. Ошибок в консоли нет, панель не падала. Самоприёмка: да.
- **14C, район.** Тест района красный до перебейка (клеток Tram было 0, `accessHash` уже совпадал). Модель `CityModel_map` переимпортирована из `Assets/Source/map.osm`. `git diff` модели — только `settingsHash` и `dataHash`. Перебейк. Самоприёмка: да.
- **14F.** Четыре кадра Game view 1080×2160 с панелью, `source=screen`. Самоприёмка: да.

## Критерии приёмки

| Критерий | Результат | Цифры |
|---|---|---|
| Tram не меняет доступ | да | `accessHash` `0x7EB6425EF2B7EC93` до и после |
| Компоненты связности | те же | PedestrianMask 10; Strict 18 / 11; ZombieMask 8 |
| Rail на трамвайных данных | 0 клеток | `railCleared=0`, в логе бейка строки Rail нет |
| Трамвай района | 4 линии, не туннели | `tramStamped=4`, `tramSkipped=0`, `tramCells=6586` |
| Панель из пяти оверлеев | не упала | консоль Play Mode без ошибок; тогл Tram в панели не появлялся |
| EditMode | 161 / 161 | `ZombieCity.City.Core.Tests` |

## Цифры

Модель: `CityModel_map` из `map.osm`. Сетка 680×484, клетка 0.5 м, origin (−170, −121).

| Поле | Было | Стало |
|---|---|---|
| `settingsHash` / `configHash` | `0xBAB661E880FB0E7C` | `0xA6925426B4FF0579` |
| `dataHash` модели | `0xBE60B87FBF94BC4C` | `0x2EFD0FFE52E8C18F` |
| `modelDataHash` бейка | `0xD000205361689643` | `0x2EFD0FFE52E8C18F` |
| `dataHash` бейка | `0xF638028B4A17E60C` | `0x060F15F16A92530C` |
| `accessHash` | `0x7EB6425EF2B7EC93` | `0x7EB6425EF2B7EC93` |

После переимпорта `modelDataHash` бейка совпадает с `dataHash` модели. До этого в ассете бейка лежал более старый `0xD000205361689643`.

Клетки Tram: 6586, 1646.5 м². Класс доступа этих клеток: Obstacle 12, Roadway 82, Pedestrian 6492. Двенадцать клеток Obstacle — пересечение с водой; бит воды трамвай не снимает, поэтому `accessHash` не сдвинулся.

Длины четырёх линий в модели:

| osm id | длина, м |
|---|---:|
| 4751005 | 749.88 |
| 143866699 | 749.49 |
| 1348391980 | 202.24 |
| 1348391992 | 202.89 |

Сумма 1904.49 м. При ширине 2.4 м без наложений это было бы около 4570 м². В растре 1646.5 м²: соседние пути слились в одну полосу, здания бит Tram снимают. Ширину не менял.

Кадры: `DOC/reports/img/tram_access_full.png`, `tram_left_crop.png` (`SetViewFrame(-120, 78, 25)`), `tram_right_crop.png` (`130, 48, 25`), `tram_flags_crossing.png` (Flags, `88, 49, 25`, оверлеи выключены).

`git log --stat 324c388..HEAD` (порядок тестов и реализации):

```
54878ae [TASK-014/14A] Tram flag, tramTrackWidth, 16-bit round-trip
4dee801 [TASK-014/14C-tests] Tram does not change access (green before raster)
627b660 [TASK-014/14B-tests] Tram raster expectations (red before implementation)
4713ee1 [TASK-014/14B] Stamp tram tracks after railways, before roads
22e865c [TASK-014/14D-tests] Tram overlay and Flags palette (red before composer)
de9a28a [TASK-014/14D] Tram Flags color and overlay priority
fd0b1e4 [TASK-014/14E] Start the scene with the Tram overlay on
3717eb5 [TASK-014/14C-tests] District tram cells and accessHash (red before rebake)
a221e17 [TASK-014/14C] Reimport model and rebake; accessHash unchanged, tram cells stamped
```

## Отклонения от ТЗ и почему

Нет. Прежняя формулировка «`modelDataHash` не менять» в задаче заменена: меняются `settingsHash` и `modelDataHash`, `accessHash` нет.

## ADR-change-request

Нет.

## Известные проблемы / ограничения

- Смежные пути при 2.4 м сливаются. Ширина оставлена как в ADR.
- В палитре Flags цвет Tram совпадает с цветом Rail (`#9A4DFF`). На районе клеток Rail нет. Оверлей Tram другого цвета, `#B060FF`.
- Панель по-прежнему знает пять оверлеев. Шестой бит она не обходит, поэтому не падает. Тогл Tram — TASK-015. В палитру Layers Viewer Grok не лез.
- `CityOverlay` по-прежнему в Core: тестовая сборка не ссылается на `Assembly-CSharp`.

## Вопросы к архитектору/оператору

Нет.

## Как проверить

1. EditMode: фильтр `Tram` и `AllSixteenBits`. Районный тест `AfterRebake_TramCellsPositive_AccessHashUnchanged`.
2. Сцена `City_Prototype`, Play Mode: слой Access, оверлей Tram уже включён. Фиолетовые полосы вдоль Osloer Straße слева и справа от перекрёстка и у остановок.
3. Слой Flags без оверлеев: та же полоса цветом `#9A4DFF`, переход поверх неё белый.
4. `ZombieCity/City/Analyze Access` — секция `## Tram` в `analysis_access.md`.
