# Hero travel between levels

Completed: the sprite hero runs to the next level after a committed campaign victory. The map opens with the hero at the completed level, waits for the screen transition to finish plus 0.2 seconds, follows the authored path, and switches back to idle on arrival. The bridge is part of the first route. Image and shadow move together in reference-map coordinates with an unscaled-time speed of 190 reference pixels/second. UI scaling does not change travel duration.

Normal menu entry, defeat/draw, an uncommitted result, another chapter, a missing/previously completed successor, or finishing the sixth level do not trigger travel. The battle-return result is consumed once. The map loads the already committed progression before deciding whether to animate; travel itself performs no saves or reward grants. Interrupted travel is cosmetic: reopening the map places the hero at the saved current level.

During travel the level nodes and primary launch button are disabled, with an additional guard in MapFlowOrchestrator. Back remains usable. Hiding/disabling/destroying the map or refreshing its state cancels the coroutine and restores input state. The existing sprite presenter handles run/idle and left/right facing; no 3D camera or video player is used.

## Files

- Assets/_Project/03_UI/Map/WoodlandRoutesSO.cs: editable route data and victory/successor eligibility rule.
- Assets/_Project/Resources/Map/Chapter1_WoodlandRoutes.asset: five normalized waypoint paths, generated through BuildWoodlandRoutes.cs using Unity CLI.
- Assets/_Project/03_UI/Map/MapController.cs: travel coroutine, transition wait, input lock and cancellation.
- Assets/_Project/03_UI/Map/WoodlandMapView.cs: shared hero/shadow ground anchor and input lock.
- Assets/_Project/01_Gameplay/Map/MapFlowOrchestrator.cs: invoke travel once after loading the committed battle result.
- Assets/Tests/Progression/WoodlandTravelTests.cs and ProgressionTransactionTests.cs: eligibility, bridge route, endpoint preservation and one-time result handling regressions.

## Verification

- Unity 6000.6.0f1 compilation passed.
- 40/40 EditMode progression tests passed, including nine new travel-related cases. Tests use isolated temporary profiles; results in travel-test-results.json.
- VerifyWoodlandTravel.Check exercised all five routes in Play Mode on temporary UI states. All arrived at exact target anchors, played run frames, returned to idle and unlocked the launch button. The fourth route correctly used left-facing sprites. A navigation-submit attempt during travel did not launch a level. Hiding midway cancelled movement.
- The QA script restored the user's original map state (6/6). It did not modify profile progress or award anything.
- Actual Game View captures of departure, bridge crossing and arrival were inspected. bridge-travel-preview.webp is a compact sampled preview of those captures; its frame rate is lower than the live game. The sprite animation remains 24 fps in the game, with travel updated every frame.
- Play Mode was stopped after verification. Environmental animation remains separate work.
