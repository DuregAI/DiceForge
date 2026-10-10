# Stage 13 — World captions and island actions

Changed `WorldSelectionView.cs`: removed nameplate sprites from world names; added transparent keyboard-accessible island buttons. Woodland uses the same transactional chapter reset as Play again, reopening Chapter 1 from its first node with rewards retained. Mushroom Hollow and Frosty Pass open the existing review form. The feedback alternative was chosen; no mailing-list integration is added.

Changed `WorldSelection.uss`: world names are plain dark captions with a light shadow, without button frames or backgrounds. Island hit areas have subtle hover and keyboard focus feedback.

Changed `DemoCampaignFlowTests.cs`: verifies pointer reachability for all three islands in landscape and portrait; opening and cancelling feedback from both future islands without profile changes; restarting through the first island; save-failure rollback through the original replay button.

Validation: full six-level campaign PASS (1/1). RU/EN captures at 1280×720 and 720×1280. No feedback submitted. Test capture flags cleared. Result: `test-result.json`.
