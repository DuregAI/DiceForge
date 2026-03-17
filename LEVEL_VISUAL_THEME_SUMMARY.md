# Level Visual Theme Summary

| Level | Board Size | Visual Family | Tilemap Prefab | Theme Asset | Background | Palette |
| --- | --- | --- | --- | --- | --- | --- |
| Level_01 | 6 tiles | Meadow | Grid_Level_01 | Theme_Level_01 | Background_MeadowGround | tile_blue / tile_yellow / tile_green / Start_flag_tile / tile_tree |
| Level_02 | 8 tiles | Meadow | Grid_Level_02 | Theme_Level_02 | Background_MeadowGround | tile_blue / tile_yellow / tile_green / Start_flag_tile / tile_tree |
| Level_03 | 10 tiles | Meadow | Grid_Level_03 | Theme_Level_03 | Background_MeadowGround | tile_blue / tile_yellow / tile_green / Start_flag_tile / tile_tree |
| Level_04 | 12 tiles | Swamp | Grid_Level_04 | Theme_Level_04 | Background_SwampGround | swamp / tile_green / tile_yellow / Start_flag_tile / tile_tree |
| Level_05 | 15 tiles | Swamp | Grid_Level_05 | Theme_Level_05 | Background_SwampGround | swamp / tile_green / tile_yellow / Start_flag_tile / tile_tree |
| Level_06 | 18 tiles | Swamp | Grid_Level_06 | Theme_Level_06 | Background_SwampGround | swamp / tile_green / tile_yellow / Start_flag_tile / tile_tree |
| Level_07 | 20 tiles | Snow | Grid_Level_07 | Theme_Level_07 | Background_WinterGround | snow_basic / tile_blue / tile_yellow / Start_flag_tile / snow_tree |
| Level_08 | 22 tiles | Snow | Grid_Level_08 | Theme_Level_08 | Background_WinterGround | snow_basic / tile_blue / tile_yellow / Start_flag_tile / snow_tree |
| Level_09 | 24 tiles | Snow | Grid_Level_09 | Theme_Level_09 | Background_WinterGround | snow_basic / tile_blue / tile_yellow / Start_flag_tile / snow_tree |

Notes:
- Levels 01-03 use the Meadow family.
- Levels 04-06 use the Swamp family.
- Levels 07-09 use the Snow family.
- `TM_Tiles`, `TM_Ground`, and `TM_TileShadow` are trimmed to each authored `BoardLayout_Level_*` footprint.
- `TM_DecoFlat` uses edge-ring placement so decor stays near the board silhouette without overlapping path tiles.