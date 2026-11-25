# Самоприёмка TASK-002 / 2C

- Ветка: `task/002-rasterization`
- Исполнитель: Composer

## Test-first

| Коммит | |
|--------|--|
| `[TASK-002/2C-tests]` | 15/15 красные |
| `[TASK-002/2C]` | 15/15 зелёные |

## Покрыто

- Порядок слоёв: building clears Road/Sidewalk/Green; passage; bridge; tunnel/CrossingNode
- v1.1 waterClearedByLinear (road/walkway/railway)
- Mini-block golden + двор **3583425** (дыра без Building/id=0)
- Stats: cellsByBit, zero-cell buildings, conflicts, under bridges
- Детерминизм dataHash
