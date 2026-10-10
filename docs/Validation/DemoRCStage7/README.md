# GlimbleHop 0.3.2-release: move presentation and island camera

Date: 2026-10-08. Unity 6000.6.0f1, URP 17.6.0, Input System 1.20.0. Implementation and verification use the connected Editor through Unity CLI.

## Delivered

- DEMO move availability uses quiet rounded stone-shaped amber rims. The destination has a stronger edge and a light surface wash. Smooth evenly spaced capsule dashes replace the floating solid line; blocked routes and interrupted destination edges use terracotta. The flat blocked cross sits at the exposed front corner of the stone. Exit feedback uses the same quiet shape, and Rusty's next-step arrow is shorter and thinner.
- Touch keeps a route for the selected available friend. Desktop board/button hover can preview another friend. Preview geometry is cached and markers/materials are reused. Blocked learning notifications fire only when the preview selection changes, avoiding a save every frame. Disabled input clears stale hover.
- One-finger/mouse dragging temporarily orbits the island: yaw up to 28 degrees, pitch up to 6 degrees. Pinch and wheel request up to 18 percent zoom. The fit can limit effective zoom to preserve all playable tile edges in the free HUD viewport. Releasing returns smoothly to the original framing. A very small slow idle sway has no roll; Reduced Motion removes it.
- Press/hold, drag, and two-finger gestures do not move heroes. A short release tap is delivered once. Gestures begun on UI, canceled touches, transitions, pause, help, and narrative screens are blocked. Losing focus restores the camera. Counter plaques face the orbiting camera.
- The WebGL canvas owns touch pan/pinch and cancels the browser's wheel default while letting Unity receive the events. RU/EN and the approved dialogue/comic design remain intact. A scoped flex correction keeps three hero buttons inside the action surface and clear of the remaining-action label.

## Changed files

- `DioramaMovePreview.cs`, `DemoMovePreviewMaterial.mat`, `DioramaBoard.cs`: pooled stone rims, smooth capsule route, landing/exit feedback and camera-aware plaques.
- `DioramaCameraController.cs`, `BoardDebugView.cs`, `DioramaHud.cs`: temporary orbit/zoom, fit bounds, release-tap arbitration, persistent hero previews and stale-hover clearing.
- `BattleDebugController.cs`, `DemoNarrativeController.cs`, `DemoTrailHazardView.cs`: wheel ownership, help gesture blocking, deduplicated learning events and calmer hazard arrow.
- `WoodlandHud.uss`: scoped action-row sizing correction. WebGL `index.html` and `loading.css`: browser gesture defaults confined to the game canvas.
- `DioramaCameraGestureTests.cs`, `DemoMovePreviewGeometryTests.cs`, existing presentation/campaign tests and the Editor-only test asmdef: behavioral checks and real render/input evidence. Native import generated the accompanying metadata.
- `ProjectSettings.asset`: application version `0.3.2-release`. This validation folder contains current evidence; previous validation folders are preserved.

## Verification

- Camera geometry/input arbitration: **5/5 passed**, [raw result](camera-tests.json).
- Route geometry: **2/2 passed**, [raw result](geometry-tests.json).
- Real Input System mouse/touch gestures and bilingual narrative presentation: **2/2 passed**, [raw result](presentation-input-tests.json). The runner aggregate includes both tests; after the domain reload its detailed result list retains the last presentation test. Real input captures are [orbit](camera-orbit-en-landscape.png) and [dotted route](dotted-route-en-landscape.png).
- Full six-level campaign: **1/1 passed**, [raw result](campaign-tests.json), including once-only progression, named heroes, hazards, blocked preview notification deduplication and action-control bounds. [Campaign captures](Campaign/).
- Actual Game View renders cover English/Russian and 1280x720 / 720x1280 for the narrative screens. Test devices/profile/language and Game View settings are restored. These are Editor images, not generated mockups.
- The art director reviewed L1, L4, L5, L6 and camera orbit. Move palette, route and camera framing were accepted. The action-panel overlap was fixed and re-reviewed; the final cross-position adjustment has a fresh campaign capture and was accepted in the final art review.

These are targeted Editor checks, not a whole-project test run or a physical-device browser test. Existing scene/Animator warnings in the retained 3D cast remain. The camera inspection feature is enabled for DEMO. User changes present before this task, including deleted performance-test run resources, were preserved.

## Release and publication

Release version: **0.3.2-release**, WebGL, Development Build disabled. Existing MainMenu, Battle and Tutorial scenes and the GlimbleHop WebGL template are retained.

The release build **Succeeded** in 444.265 seconds with **0 errors / 22 warnings**. The warnings include existing obsolete-API/serialization/Animator messages and disabled development Pipeline access in the Player. See [native build report](build-report.json).

The archive is `Builds/Glimblehop-0.3.2-release.zip` (119,464,412 bytes), with `index.html` at its root, all 11 files nonempty and ZIP integrity verified. The WebGL HTML declares `0.3.2-release`; canvas gesture handling is present. [Release manifest and SHA-256 per file](release-manifest.json).

**Publication was not completed.** The native upload reached 91 percent and returned HTTP 408 after about ten minutes. A second streamed HTTP transfer using native Editor authentication also stopped after about ten minutes at 87 percent. [Attempt 1](publication-attempt-1.json), [attempt 2](publication-attempt-2.json), [final result](publication-result.json).

Public metadata and latest-frame checks after both failures confirm the same existing GlimbleHop game/project IDs, `visibility=unlisted`, `status=valid`, unchanged update timestamp and the previous published build `c4d802c6-dcb4-4ff5-bac9-d24b6dd656fb`. No new game was created and no successful publish is claimed. Browser/Windows automation could not initialize (`helper_unknown_error`).

The verified release ZIP is ready for upload through the existing game's browser editor. Publication of that ZIP with Unlisted visibility remains the outstanding step. The hosted service replaces the custom web template; a read-only inspection of its current runtime confirmed native touch/wheel callbacks call `preventDefault` when Unity consumes the event. A physical-device browser gesture check remains outstanding.
