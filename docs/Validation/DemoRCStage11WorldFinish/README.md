# Stage 11 — Chapter-one finale and world selector

The final level now plays a three-line cliffhanger before showing the world selector. Tish notices a light in Mushroom Hollow; Jo recalls that nobody lives there; Bum sees someone waving from the window. The final dialogue action is “Explore worlds” / “К новым мирам”. Existing story skip remains available.

The selector follows the supplied composition: large floating islands, a heavy rounded Cyrillic-capable title, ivory leaf plates, dotted connections, green 6 / 6 completion badge and a gold replay button. The two future worlds remain locked. Both languages use live text rather than text baked into illustrations. Portrait layout retains the three worlds and places secondary actions below replay; smaller heights can scroll.

Actions:
- Play again: starts Chapter 1 again and preserves earned rewards; failed saves keep the completed profile intact.
- Visit website: opens https://glimblehop.com/.
- Write a review: opens the existing rating/comment form. Opening and cancelling do not submit or alter progression.
- Home: returns to the main menu; completed progress reopens the world selector.

Files:
- `Assets/_Project/03_UI/WorldSelection/WorldSelectionView.cs`: atlas loading, responsive layout, localization and action wiring.
- `Assets/_Project/03_UI/WorldSelection/WorldSelectionVisuals.cs`: native home, lock, check and dotted-route drawings.
- `Assets/_Project/Resources/WorldSelection/WorldSelection.uxml` and `.uss`: composition, dimensions, type and responsive styling.
- `WorldIslandsV2.png`, `WorldPlates.png` and region JSON files in the same resource directory: separated illustrated sprites and plates.
- `GlimbleRoundBlack.ttf` and its OFL file: static weight 1000 derived from [Google Fonts Nunito](https://github.com/google/fonts/tree/main/ofl/nunito), renamed Glimble Round.
- `Assets/_Project/03_UI/Dialogue/DemoNarrativeController.cs` and `Resources/WorldSelection/WorldTeaser.json`: modal story transition and English/Russian lines.
- `worlds.json`: localized website/review labels.
- `Assets/Tests/BattleRunner/DemoCampaignFlowTests.cs`: full campaign, cliffhanger gating, localized screenshots, action reachability, URL interception, review cancellation, home/reopen and transactional replay checks.

Art prompts and reference provenance are in `art-provenance.json`.

Verification uses Unity 6000.6.0f1, an isolated temporary player profile and the existing full-campaign test. The website action is intercepted during testing; the review form is opened and cancelled, never submitted. Screenshots are captured from Unity at 1280 × 720 and 720 × 1280 in both languages. No release build or publication is included in this change.

The review form receives styling scoped to the world selector: a compact ivory panel, stars that fit the available width, and fixed bottom actions outside the scrolling content. Existing feedback screens elsewhere keep their presentation. The campaign test also asserts that all five stars fit horizontally and both review actions remain reachable.

Final validation (2026-10-10, Asia/Krasnoyarsk): full-campaign test PASSED (1/1), including all six levels, cliffhanger gating, RU/EN layout captures, website URL interception, review cancellation, saved-completion reopening, and replay save-failure rollback. Final C# compilation and Unity Console report zero errors. Reviewed final world and portrait review screenshots. Test session flags cleared; Editor left out of Play mode, timeScale = 1, captureFramerate = 0. Full machine-readable result: `test-result.json`.
