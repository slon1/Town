# REPORT-009 — слияние веток 008 и 007 (git-гигиена)

- Ветка: `task/007-camera-ui` (итоговая вершина — коммиты merge + `[TASK-007/7B]` + этот рапорт).
- Исполнитель (модель): **Composer**
- Unity **6000.5.9f1**, Pipeline/MCP для проверок.

## 9A — разведка

| Параметр | Факт |
|---|---|
| `git status --short \| wc -l` | **25** (в ТЗ указано 109 — расхождение зафиксировано) |
| Ветка 007 | `76224d1` = `origin/task/007-camera-ui` |
| Ветка 008 | `3044246` = `origin/task/008-scene-fixes` |
| `merge-base(007,008)` | `3003f1a` (`p4-scene-core`) |
| Worktree | только `J:/work/Town` на 007; **`Town-008` отсутствует** |
| Stash | 5 записей (старые не трогали) |
| Локальные изменения | в основном шум Editor/TMP/`_hold`/DOC в stash; tracked-изменений Scene/UI до stash не было |

**Классификация локальных строк (до 9B):** (а) ProjectSettings, TMP, export.osm; (б) untracked meta сцены 007; (в) DOC/PLAN/ADR untracked в stash — **без незакоммиченной работы по 007/008 в tracked-файлах**.

## 9B — подготовка

- Теги `backup/007-before-merge` (`76224d1`), `backup/008-tip` (`3044246`) → `origin`.
- `git stash push -u -m "noise before merge TASK-009"` (stash@{0}).
- Рабочее дерево чистое перед merge.

## 9C — 008 в `main`

- `git checkout main`, `pull --ff-only`, `merge --no-ff task/008-scene-fixes`.
- Merge-коммит: `a911344`.
- Тег **`p4-scene-fixes`** на `a911344`; `git push origin main --tags`.
- Unity (на дереве после merge): EditMode **145/145**; Play `City_Prototype`: `[City] buildings ... outsideGrid 0 ...` (лог консоли).

## 9D — `main` в 007

- `git merge main` → конфликт **1 файл**: `Assets/_Project/Scenes/City_Prototype.unity`.
- Разрешение: объединены поле `buildings` у `CityInstaller` (из 008) и блок `CameraUiInstaller` (из 007); ручная правка одного hunks (UnityYAMLMerge не вызывался — результат проверен открытием сцены и Play без Missing Script).
- `git grep` маркеров конфликта: пусто.
- Merge-коммит: `5fb81c7`. EditMode **145/145**; Play: камера, панель, `outsideGrid` в логе.

## 9E — тогл «Здания»

- `CityMapLayerPanel`: `IBuildingsView`, тогл «Здания», синхронизация по `Changed` / `SetIsOnWithoutNotify`.
- Prefab пересобран меню `City/Build Map Layer Panel Prefab` (script eval).
- Скриншот: `DOC/reports/img/panel_buildings_toggle.png` (1080×2160, BuildingId, здания скрыты).
- Дополнен `DOC/reports/REPORT-007.md`.

## 9F — порядок в репозитории

| Stash | Содержимое в коммитах? | Комментарий |
|---|---|---|
| `stash@{0}` noise TASK-009 | нет (снят с дерева) | Editor/TMP/DOC; не применяли |
| `stash@{1}` wip-008 | неизвестно | не анализировали построчно |
| `stash@{2}` wip 007 held for 008 | частично | 007/008 уже в ветках |
| `stash@{3–4}` noise 002 | неизвестно | исторические |

Worktree **`Town-008`**: нет → `git worktree remove` **не выполнялся**.

## Итоговый граф (фрагмент)

```
*   5fb81c7 (task/007-camera-ui) Merge branch 'main' into task/007-camera-ui
|\  
| *   a911344 (tag: p4-scene-fixes, main) Merge branch 'task/008-scene-fixes'
| |\  
| | * 3044246 (task/008-scene-fixes, tag: backup/008-tip)
* | 76224d1 (tag: backup/007-before-merge, origin/task/007-camera-ui)
|/  
* 3003f1a (p4-scene-core)
```

## Проверки Unity (007 после 9D–9E)

| Проверка | Результат |
|---|---|
| EditMode | **145 passed, 0 failed** |
| Play `City_Prototype` | без ошибок; `outsideGrid 0` в логе зданий |
| Камера / панель / слои | OK (Play + MCP) |
| Тогл «Здания» | `IBuildingsView.Visible` + скриншот |

## Ограничения / для оператора

- Второй worktree из ТЗ не найден — проверить вручную при необходимости.
- Ручное слияние YAML сцены вместо `git mergetool` + UnityYAMLMerge — при аудите перепроверить сцену в Editor.
- Локально снова могут появиться untracked TMP после импорта (не коммитили в 009).
- Решение по **удалению stash** — за оператором (stash не трогали).

## Вопросы

Нет.