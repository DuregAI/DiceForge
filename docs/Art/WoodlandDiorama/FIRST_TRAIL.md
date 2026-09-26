# First trail: eight-space solo scene

## Playable first pass — 24 September 2026

`Assets/_Project/06_Scenes/Art/FirstTrailReference.unity` now uses the same eight-stone art composition as a playable solo prototype. In Play mode, choose one of the current step values in the bottom HUD, then click either friendly goblin or its highlighted cell. Movement follows the ordered cells; a goblin leaving cell 7 reaches the bridge. The party ends when both friends exit. The restart button restores both to cell 0. There is no second-side token or bot turn.

The rules run through `BattleRunner` with `GameMode.SoloTrail`, so the mode stays in the existing core. `FirstTrailPlayController` only binds the eight scene anchors, two visible characters, HUD, hit targets and jump presentation. The original multi-player battle modes remain unchanged. Rebuild the scene with **Diceforge > Woodland > Build first trail composition** after a Blender export.

The game-camera preview is `Captures/first-trail-playable.png`. Unity compilation and a Play-mode launch completed without runtime console errors; the HUD was checked at top, middle and bottom screen coordinates. A small core smoke check moved one friend from cell 0 to 3 to 7 and out, with player A retained and no B stones. Per the user's request, no automated test suite or full manual playthrough was run. Character animation is still the old goblin prefab and the HUD is a first functional pass. The new T-pose candidate is in a separate preview scene; it has not replaced gameplay characters.

## Earlier art notes

## Earlier eight-space art study

The latest user reference supersedes the seven-space count below: **eight spaces, two friendly goblins**, short open diagonal route, landscape art preview. The new reference emphasizes a rocky island surrounded by turquoise water, rounded conifer boughs, moss, warm sun and an out-of-focus distant forest. Supplied PNGs are visual references, not imported 3D models.

- Run `reference_first_trail.py` in Blender, then **Diceforge > Woodland > Build first trail composition** in Unity.
- Blender source: `First_Trail_Reference.blend`; export remains `Assets/_Project/07_Art/FirstTrail/FirstTrail.fbx`.
- Current Unity scene: `Assets/_Project/06_Scenes/Art/FirstTrailReference.unity`; anchors `Cell_0` through `Cell_7`.
- Current capture: `Captures/first-trail-reference-08-refined.png`.
- Added rounded conifers, surrounding animated water, simple lily pads and distant forest groups. The water is an artistic opaque surface; no physical depth/refraction yet.
- Fixed narrow-perspective camera approximates the previous orthographic framing. A separate `ReferenceGrade.asset` uses Gaussian depth of field (start 26.9, end 29, radius 1.5), leaving the route sharp. The shared hero profile remains unchanged.
- Existing unsaved scene was preserved as `FirstTrailBeforeReference_20260920_215825.unity` before rebuilding. Older scenes reference the same evolving FBX, so their geometry is not a frozen snapshot.
- This is a foundation pass, still well below reference material/sculpt quality. Next: one finished stone/shore/moss group, then propagate its style. Solo gameplay remains unconnected.
- Verification: successful Unity compilation and visual review of two game-camera captures; no automated tests, downloads or online research.

## Earlier approved direction (history)

Current approved direction: two friendly goblins, seven spaces on a short open path, no opponent or hazards, fixed landscape camera with no zoom. The goal will be to get both friends beyond the bridge.

The user approved the diagonal route and character scale. Preserve these during art revisions.

## Step 2 art pass

- `detail_first_trail.py` rebuilds the approved base, then adds sculpted ground, smooth creek geometry, grass tufts, gravel, stone weathering, camp ropes/bedroll/firewood and a separate pennant. Run this script for the detailed version; `build_first_trail.py` alone produces the earlier composition study.
- `FirstTrailArtMaterials` creates dedicated ground color/normal textures with dirt around the route and moss farther away, plus canvas/wood/stone materials. It does not overwrite the older hero level's materials.
- `FirstTrailWater.shader` supplies subtle moving creek highlights; `FirstTrailLife` animates the pennant. Both support the saved reduced-motion preference.
- Direct sunlight is reduced and fill light increased for this scene only. Camera and route anchors remain as approved.
- Current scene remains a visual study: no solo movement or victory flow yet. Character proportions and existing idle rig were retained; no new facial animation was created in this pass.

## Step 1 delivered

- Blender source: `First_Trail.blend`.
- Composition authoring script: `build_first_trail.py` (reuses the existing local modeling helpers and kit).
- Export: `Assets/_Project/07_Art/FirstTrail/FirstTrail.fbx`.
- Unity review scene: `Assets/_Project/06_Scenes/Art/FirstTrailComposition.unity`.
- Rebuild menu: **Diceforge > Woodland > Build first trail composition**. Saves a separate scene; refuses to replace unsaved scene edits.
- Two same-team goblins start on the near-left platform. Seven visible spaces lead diagonally to the bridge and far-right exit. Cell anchors are saved as `Cell_0` through `Cell_6`.

This scene is a visual composition study. It does not run a match, accept moves, or grant rewards. Existing battle maps remain separate. Caption text is provisional; no fake playable controls are shown.

## Following steps

2. After user feedback on this frame: refine terrain, stones, wood, vegetation, water, character appearance and light. Keep the approved framing and route readable.
3. Implement the actual solo flow through the existing gameplay architecture: select a move, select a friend, move along the route, finish when both reach the exit. Do not simulate solo mode by leaving an invisible opponent in the battle.

No automated tests, downloads or online research were run for this composition milestone. Validation was limited to compilation, opening the scene and a game-camera capture. Ask the user before additional tests or external downloads/research.

## Foreground shore pass

`detail_reference_shore.py` is now included by the reference build script. The near bank uses irregular beveled polygonal stones, overlapping low shelves, three stone tones with a generated repeating fracture texture, cascading moss and fleshy leaf groups. Thin broken ripples mark contact with the water. A small rock/plant group sits below the route; eight anchors and both goblin placements are unchanged. Distant rocks remain the earlier placeholders for comparison. Current capture: `Captures/first-trail-shore-refined.png`. This is still an art iteration, not a finished reference-quality level. No automated tests or external assets were used.

## Soil-to-stone transition pass

The near bank now has an uneven earthen lip, fourteen surface-fitted moss patches, sparse flowers and exposed roots. The creek outlet is excluded from the lip. Moss uses upward-facing normals and a muted olive material. Route geometry and gameplay are unchanged. Capture: `Captures/first-trail-moss-edge-final.png`. Reviewed in the Unity game camera; no tests or downloads. Further sculpt/material refinement is still needed to reach the supplied reference.

## Free water prototype

`StylizedSurface.shader` is an original local URP transparent shader, shared by lake and creek materials. It reconstructs opaque scene depth for shallow/deep color and contact foam, animates procedural caustic-like surface patterns and river streaks, and adds directional-light highlights with simplified sky tint. It requires the camera depth texture (already enabled in HeroPipeline). It has no planar reflections, refraction, physical waves or fluid simulation; caustics are an artistic surface approximation, not projected underwater lighting.

`detail_reference_water.py` adds a submerged shelf, pebbles, a raised creek surface and a curved outlet cascade. Old rigid foam arcs are removed. Reduced-motion initialization covers both materials and cascade via FirstTrailLife. The existing floor remains the creek bed; a fully sculpted channel is still future work. Shore Sand uses the original stone microtexture rather than the large fracture texture. Latest capture: `Captures/first-trail-free-water-final.png`. Saved Unity art scene: FirstTrailReference. No external assets, purchases, downloads or automated tests. Scene built and visually inspected; console error read was empty after the water refinement.

## Creek, waterfall and foreground — 21 September

The creek now has a lowered bed sculpted into a subdivided forest floor; the old island top cap is removed to expose the channel. Surface height is .045 Blender units. The outlet has framing stones, a curved sheet ending below lake level and a separate animated impact-foam material. Flow streaks follow the creek UV direction. Near-camera branches sit at the two lower corners, outside the route. ReferenceGrade now uses Bokeh depth of field: focus distance 25, focal length 300, aperture 1; reviewed in the game camera at 16:9. This replaces the earlier Gaussian settings. Current capture: `Captures/first-trail-waterfall-final.png`.

These remain stylized effects, not a fluid simulation. Visual review and console check only; no automated tests, external downloads or gameplay changes.

## Two trial path stones — 24 September

Cells 2 and 3 now use `detail_reference_tiles.py`: broader flat landing faces, more squared handmade outlines, uneven narrow bevels and small moss/pebbles at their feet. `TrialStone0/1` materials are assigned only to these cells by FirstTrailCompositionBuilder. Cell IDs, positions, route order and goblin assets remain untouched. Rebuild with `reference_first_trail.py` in Blender and `Diceforge > Woodland > Build first trail composition` in Unity. Review capture: `Captures/first-trail-trial-stones-square.png`. This is a sample for comparing against the remaining original stones. Visual check in Unity completed; no automated gameplay tests were run.

## Eight path stones — 24 September

The approved flatter, squarer trial shape is now used for all eight cells. Cell 6 keeps its extra 0.12 height over the bridge. `detail_reference_tiles.py` and the Unity builder assign alternating TrialStone0/1 materials without changing IDs or route positions. Review capture: `Captures/first-trail-all-stones-2.png`. Unity visual verification: eight unique anchors, two goblins, no current console errors. No gameplay tests run; this remains an art preview without playable solo flow.
