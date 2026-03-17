# Level Progression Summary

Campaign Chapter 1 battle progression now uses explicit per-level presets:

| Level | Mode ID | Map Size | Pieces | Dice Bag |
| --- | --- | ---: | ---: | --- |
| 01 | `level_01` | 6 | 1 | 1 die, values 1-3 |
| 02 | `level_02` | 8 | 2 | 1 die, values 1-4 |
| 03 | `level_03` | 10 | 3 | 1 die, values 1-5, low-value weighted |
| 04 | `level_04` | 12 | 5 | 1 die, values 1-5 |
| 05 | `level_05` | 15 | 7 | 2 dice, values 1-3 |
| 06 | `level_06` | 18 | 9 | 2 dice, values 1-4 |
| 07 | `level_07` | 20 | 11 | 2 dice, values 1-5 |
| 08 | `level_08` | 22 | 13 | 2 dice, values 1-6 |
| 09 | `level_09` | 24 | 15 | full 2-dice 1-6 bag with expanded doubles |

Implementation notes:

- Levels 1-4 use the smaller iso board theme.
- Levels 5-9 switch to the full 24-tile board theme and continue scaling path length.
- First 9 Chapter 1 battle nodes now point to `level_01` through `level_09`.
- Each level has explicit `GameMode`, `Ruleset`, `Setup`, `BattleMapConfig`, `BoardLayout`, and `DiceBag` assets for review in the editor.
