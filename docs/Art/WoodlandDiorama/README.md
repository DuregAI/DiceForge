# Woodland battle diorama

The battle presentation uses a Blender-authored miniature kit in Unity 6000.6.0f1 / URP 17.6.0. Gameplay continues through BattleLauncher and BattleRunner. No currency, reward, dice or movement rules are replaced.

## Authoring

- `Woodland_SourceKit.blend` contains the source meshes, shared skeleton, animation actions and painted palette.
- Run `build_blender.py`, then `bake_palette.py`, then `build_island.py` inside Blender to rebuild exports. These scripts create a separate source scene, leaving other scenes intact.
- Exported FBX assets and the palette are in `Assets/_Project/07_Art/WoodlandDiorama`. Keep FBX coordinate conversion transforms intact: Blender exports contain a conversion scale/rotation. Unity places them inside neutral wrapper transforms.
- Unity menu **Diceforge > Woodland > Build assets and layouts** creates game prefabs, Animator controllers, both orientation layouts, combined decoration meshes, URP materials and pipeline profiles.
- **Bake both orientations** writes per-composition lightmaps and character light probes. It requires a clean saved scene and runs outside Play Mode. Progress is recorded in `Validation/lighting.txt`. Layouts sharing the same cell count reuse the same lighting because their authored compositions are identical.
- **Enable for current maps** assigns the new themes after validation. Original Tilemap themes remain as separate assets; assigning one back to a map restores the old presentation.

## Runtime contracts

`IBoardGeometry` resolves cell poses, formation offsets, waiting/exit anchors and bounds. The Tilemap adapter and `DioramaBoard` share this contract. `DioramaLayout` preserves each logical cell ID in both landscape and portrait arrangements.

Logical stone identity belongs to `TokenPlacementResolver`. At most three stationary models are visible per occupied cell/team; the badge shows the total count. A hidden stone is temporarily made visible at its source for a move. Animation completion reconciles visibility against authoritative state. Orientation changes cancel movement and reapply existing assignments, without reassigning stone IDs.

The new UI is UI Toolkit. Labels follow the existing English/Russian/Simplified Chinese language preference. The old HUD adapter remains active for legacy controller bindings but its visual tree is hidden. Tutorial steps attach to the new document. Input uses gameplay colliders, supports mouse/touch and excludes UI hits. Pause and reduced-motion controls are in the rest-stop menu.

## Validation

Run **Diceforge > Woodland > Run regression tests**. Results persist across domain reloads in `Validation/results.xml` and `Validation/progress.txt`. The suite covers existing battle termination, progression and placement tests, plus diorama visibility limits, hidden stone movement, layout consistency and special moves.

Visual captures are kept in `Captures`. Check desktop, portrait, landscape and tablet framing, maximum stacks, hover routes, pause, tutorial and result overlays after changes to layout or UI.

Device performance must be measured in a player build. Editor screenshots and triangle counts do not establish Android/iOS frame rate, thermal behaviour or touch accuracy. iOS builds require an available Mac/Xcode toolchain and device.

## Art budgets

Desktop target: 60 FPS at 1080p, at most 600k visible triangles and 200 draws. Mobile target: stable 30 FPS, at most 250k triangles and 120 draws. These are production targets, not certified performance results. Decoration uses a shared palette atlas and combined meshes; goblins have two LODs. Lightmaps are shared for matching layouts. First reduce decorative effects and shadow cost; retain input target sizes and legible counts.
