# Validation status — 2026-09-20

The implementation remains under validation; the default map themes have not been switched.

- Unity 6000.6.0f1 loaded the project and compiled the current scripts without errors.
- Asset/layout rebuild completed after replacing full-grove UV unwrapping with packing of imported secondary UVs.
- EditMode regression: **87 passed, 0 failed, 0 skipped**. See `results.xml` for the actual cases and timing.
- Lighting bake completed for all **22 compositions**. `lighting.txt` records each result. A separate lighting-integrity test passed after script reload: all layout prefabs retain both profiles, lightmaps, probes, serialized sun flags and valid renderer bindings (`lighting-integrity.txt`). This additional check is separate from the 87-test XML run.
- Updated game captures: `../Captures/landscape-lit.png` and `../Captures/portrait-lit.png`, level 05, same party across orientation change. Three visible goblins represent each seven-stone stack. Lighting currently appears too dark compared with the concept and still needs artistic adjustment.
- Largest landscape environment: **254,564 triangles**, before characters. This exceeds the initial mobile budget of 250,000. Goblin LOD0: 9,304 triangles; LOD1: 4,278. Counts are asset geometry, not measured visible draws or player performance.

Still required: tablet, tutorial and results verification; adapt tutorial panel placement above the new HUD; mobile geometry reduction; artistic refinement against the concept; Android/iOS device performance and lifecycle testing. No real-device FPS, memory, thermal or iOS build result is claimed.
