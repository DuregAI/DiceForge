# Demo RC v0.1 — L1–L3

Implemented named SoloTrail campaign entry through the existing map, BattleLauncher, BattleRunner, diorama HUD and reward transaction path. L1 has Tish and step 1; L2 offers steps 1/2 as alternatives for one action; L3 adds Luma with independent exit. A successful alternative refreshes the whole offer, while invalid input retains it. Named token selection resolves a shared origin without substituting another friend. Restart cancels queued selection and result presentation.

The isolated-profile integration test passed on 2026-10-06 (`campaign-flow.xml`): normal map launch, all three level completions, exact-once receipts, reload of saved progress and cancellation of queued selection. Core routes and identity tests also passed. Earlier regression files retain the diagnostic history, including outdated two-sided L1 expectations and collider/turn assumptions subsequently corrected.

See the stage-two report for the current six-level configuration and broader validation. Story/dialogue integration and mid-level checkpoints are not included in this stage.
