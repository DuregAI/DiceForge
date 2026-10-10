# DEMO hero hop polish

Focused presentation polish on the existing DEMO characters.

## Behavior

- Anticipation uses the first 12% of the existing move duration.
- Flight uses one continuous eased arc, with gradual heading alignment and a small body lean/stretch.
- Contact recovery uses the final 16%; the logical destination commits when the presentation finishes.
- The model has a separate runtime presentation parent, preserving formation scale, colliders and Animator bone paths.
- Reduced Motion keeps the existing short movement without lift, squash or lean.
- Cancellation resets the presentation transform.
- Animator transitions explicitly target layer 0. Optional reactions run only when that controller contains the requested state.

## Files

| File | Purpose |
| --- | --- |
| Assets/_Project/04_Views/Battle/Diorama/DioramaHopMotion.cs | Allocation-free hop timeline |
| Assets/_Project/04_Views/Battle/BoardLayoutTokenMover.cs | Playback, heading and cancellation |
| Assets/_Project/04_Views/Battle/StonesTokensView.cs | Enable for DEMO player heroes |
| Assets/_Project/04_Views/Battle/Diorama/GoblinLife.cs | Valid Animator layers and optional reactions |
| Assets/Tests/BattleRunner/DioramaHopPresentationTests.cs | Actual Bum prefab: formation, bone paths, pause, interruption, Reduced Motion and unavailable reaction |
| Assets/Tests/BattleRunner/DemoCampaignFlowTests.cs | Session-scoped capture destination and optional frame recording |

## Review

Play the DEMO in Unity. Inspect a normal hop in L1 and the two-step jump across the occupied intermediate cell in L4. Pause during a move, then try Reduced Motion.

hop-preview.gif encodes actual Game View screenshots from the first L1 move. It holds the first and final frames for inspection; GIF timing is approximate. Original PNG frames are in Campaign/HopFrames. It is not a generated mockup.

Native CLI test status drops earlier individual results across Play Mode reloads; hop-tests.json retains the full aggregate and the last individual test. Test fixture settings and the isolated campaign profile are restored after runs.
Final validation: Unity 6000.6.0f1 compilation passed; hop tests 2/2 passed; all-six-level campaign test 1/1 passed; git diff --check passed. Final console contains zero errors and no Animator transition warnings. Seven setup warnings remain (six lazy BattleRunner initializations and one missing BoardLayout reference).
