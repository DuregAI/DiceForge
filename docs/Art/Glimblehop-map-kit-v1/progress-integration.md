# Step 5 — live menu and campaign integration

The normal main-menu PLAY action now opens WoodlandMapView via MapController for the authored Chapter1 map. No scene wiring was replaced. The runtime stylesheet moved with its existing GUID to Resources/Map/WoodlandMap.uss; relative image/font URLs continue to resolve. Preview window uses the same Resources path.

Chapter1 contains the existing C1_01 through C1_06 battle presets in a single chain, with the approved background-relative positions. Original standard battle rewards are preserved. Side chest/shop nodes and links to levels 7–9 are removed from this chapter; their independent battle presets/assets remain available. MapDefinition validates six unique battle nodes and ordered links before a chapter is used.

## Save compatibility

MapProgressService normalizes only woodland chapters. It keeps all historical completed/unlocked IDs (including removed side nodes and levels 7–9), run ID, rewards and transaction receipts. The current pointer is moved to the first uncompleted retained level, which is unlocked if necessary. All six completed means an empty pointer and BACK TO MENU. Gaps in old debug progress are preserved rather than marking unplayed levels completed. UI counts actual retained completions and marks each node independently.

Normalization is committed only when needed, against a detached profile snapshot. A failed write does not mutate the live profile. Selection is checked by the orchestrator as well as by the UI; future or completed woodland nodes cannot launch even if an older debug save lists them unlocked.

## Verification — 2026-09-18 local date

Used Unity CLI bundled at C:/Program Files/Unity Hub/resources/cli/unity.exe with com.unity.pipeline installed by the user. Project editor: 6000.6.0f1. CLI requires access to the local Pipeline descriptor; approved elevated commands were used. No editor/package upgrade was made by this task.

- Compilation completed with failed=false and no compilation errors.
- 31/31 EditMode progression tests passed. Results: progression-test-results.json.
- Four new regressions cover historical IDs and receipts, non-contiguous progress, final-level victory/reload, and migration write failure. Existing tests cover loss/draw, duplicate results and retry/restart persistence.
- All six scene battle preset references and the runtime stylesheet resolved.
- Live menu PLAY opened six-node map using the existing profile: 1/6, only level 2 enabled.
- Live primary button launched Battle with MapFlowRuntime.SelectedNodeId=C1_02.
- Return without reporting a battle result reopened the map at 1/6; QA did not grant rewards or complete a level.
- Back button hid the map through the existing transition. Play Mode was stopped after verification.
- unity-live-map.png is an actual Game View capture obtained through CLI source=screen and visually inspected. Fonts, text bounds and undistorted art were also checked in the runtime UI tree.

VerifyMapIntegration.cs is an explicit CLI verification helper outside Assets; it is not shipped or auto-executed. Run individual entries with run_script --file docs/Art/Glimblehop-map-kit-v1/VerifyMapIntegration.cs --entry VerifyMapIntegration.OpenMap (while playing MainMenu). Start, ValidateContent, Inspect, Launch, ReturnWithoutResult, OpenAndBack and Stop support the other checks.

An existing DiagnosticsRuntime cleanup error appears on exiting Play Mode, predating this integration; it is outside the map changes. Earlier malformed-map errors in the console refer to the interrupted draft config, now corrected and guarded by validation. Tests passed against the corrected six-node asset.

Hero placement, sprite animation and post-victory route traversal are implemented and verified; see ../Glimblehop-map-hero-sprites-v1/travel-stage.md. Remaining planned work includes environmental motion. Current water and bridge are static background art. Portrait-specific composition/localization remain separate work.
