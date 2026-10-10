# Stage 12 — Animated world selection

Changes requested against the approved mockup:
- Plates and buttons preserve their source aspect ratio (`contain`) rather than stretching; nameplates have more height and completion badge has its own fixed height.
- Home button has a cream rim, inset outline, shaded lower edge and outlined brown house, drawn natively with Painter2D.
- Localized heading is live text set along a shallow arc, with measured glyph advances and tangent rotation. Both English and Russian use the existing heavy Nunito-derived face. Footer uses a darker bold treatment.
- Background replaced with a reference-matched mountain valley, atmospheric layers, and blurred foliage/pine silhouettes at the foreground corners.
- Three islands gently float and rock independently. Fair-weather clouds drift over the first island, rain falls under the second island’s cloud, and rotating snowflakes descend under the third.
- Animation runs at approximately 30 scheduled updates per second using real time, pauses when hidden, and is disposed with the screen. UI actions, labels, and hit targets remain stationary.

Implementation files:
- `WorldSelectionView.cs`: title/atmosphere ownership and show/hide/disposal.
- `WorldCurvedTitle.cs`: measured, localized curved typography.
- `WorldAtmosphere.cs`: clouds, procedural precipitation and island motion.
- `WorldSelectionVisuals.cs`: outlined, layered home button.
- `WorldSelection.uss`: plate proportions, type, weather placement and responsive composition.
- `WorldBackgroundV2.png`, `WorldClouds.png`, `WorldCloudsRegions.json`: background and cloud artwork, stored under `Assets/_Project/Resources/WorldSelection`.
- `DemoCampaignFlowTests.cs`: animation progress and visibility lifecycle checks alongside the existing campaign, localization, review, website and replay checks.

Artwork was made with the built-in Imagegen tool using the attached mockup. Exact prompts and final asset paths are in `art-provenance.json`. Existing source atlases were not resampled or destructively edited. Cloud regions are runtime sprite rectangles.

Validation targets: RU/EN at 1280 × 720 and 720 × 1280, full six-level campaign with an isolated profile, visible motion / hidden pause / reopen resume, existing end-dialogue and feedback flows. The video is encoded from actual Unity captures, not generated footage. No release build or upload is part of this request.

Final verification: PASS (1/1 full-campaign test). New animation assertions verified actual movement, stopped updates while hidden, and resumed updates after reopening. Reviewed RU/EN captures at both target resolutions. All 24 motion captures have distinct hashes; `worlds-motion.mp4` is a 10 fps capture preview of the approximately 30 Hz UI animation. Final compilation and Console: zero errors. Existing BattleRunner/BoardLayout fixture warnings remain. Test flags cleared; Editor restored to not playing, timeScale 1 and captureFramerate 0.
