# REPORT-003 / 3C — связность (самоприёмка)

- **Порядок:** `[TASK-003/3C-tests]` → `[TASK-003/3C]`.
- **`islandMinCells`:** 200 → `CanonicalBytes`; settingsHash **`0x09A4C90D0BFD89D7` → `0xC8F8DE8B56EBA46F`**.
- **Район:** Pedestrian компонентов 116 (островов 108); Zombie 114 (107).
- **Crosswalk:** клеток 1569; without/with = **116/116**; connecting = **0**. Переходы не снижают число компонент (сеть тротуаров уже связана в обход / CW не мост между разными id). Не правил правила — см. итоговый REPORT-003.
- **Road|Rail:** 100 клеток.
- **dataHash** bake без изменений: `0x6B317D7D36FA2A8C`.
