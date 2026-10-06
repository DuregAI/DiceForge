# Demo RC v0.1 — L4–L6 gameplay

Source: `docs/DemoRC/v0.1/01_GAME_DESIGN.md` and `data/levels.json`, version 0.1.

## Implemented

- L4: Tish/Luma/Bum, one action chosen from 1/2, static Bark on cell 4, technical limit 64 turns.
- L5: the same friends, two separate steps 1/2 in either order, Ryzh on cell 3. He advances +1 once per completed team turn, including an explicit pass. If his next cell contains a friend, he retires permanently. Natural exit and yielding do not increment either team's exit count or finish the level.
- L6: the same friends and two separate steps, static Bark on 4, technical limit 64 turns.
- Every friend exits independently; completion requires all three. Invalid landing costs no action. An unplayable remainder waits for a short explanation and one explicit pass.
- Obstacle occupancy uses the existing destination-blocking rules; intermediate occupancy permits jumping. Demo movement goes directly to its destination with a jump arc, without a visual landing on the obstacle.
- Hazard animation waits for the final friend animation; input stays blocked until both finish. Restart cancels pending presentation and restores friends and the obstacle.
- Named buttons, remaining actions, obstacle guidance, invalid-landing X and an OUT ring beyond cell 7. Scores, exited indicators and renewed step buttons update after presentation.

## Validation

`rules-and-routes.xml`: 22/22 passed, including all 12 authored routes (manual/minimal L1–L6), landing restrictions, alternative/separate action consumption, permanent yielding and explicit no-move pass.

`regression.xml`: 119/120 passed across BattleRunner, TokenPlacement and Progression. The six-level integration test used an isolated temporary profile, exercised normal map entry, completion/unlocking, profile reload and receipt retry without duplication. It also verified restart during queued selection and during the wait for Ryzh's animation.

The remaining failure is the existing `DioramaLightingIsSavedForEveryComposition` expectation: FirstTrail8's landscape and portrait lighting references are empty in the original prefab. Gameplay changes do not address baked lighting.

`presentation-and-campaign.xml`: 23/23 passed after the compact HUD and direct-jump changes. The integration test repeated the full L1–L6 campaign and checked that a jump over an occupied intermediate cell never visually lands there. Unity's Console contained no errors after this run, and the Editor returned to MainMenu.

Screenshots are captured from the actual Game View by the integration test: [L4 start](level-04-start.png), [blocked landing](level-04-blocked-landing.png), [L5 with one action left](level-05-one-action-left.png), [L6 start](level-06-start.png). They show the compact HUD, named controls, hazard guidance and the exit ring beyond the last cell.

## Scope still outstanding

These changes complete the mechanics stage, not the whole Release Candidate. S00–S06 story scenes, wedding presentation, dialogue/tutorial event integration, learning evidence, context help, mid-level checkpoints, final character art, location decoration and device/portrait acceptance remain separate stages. Bum/Ryzh/Bark use clearly distinguishable gameplay proxies. No WebGL build or device performance claim is made by this report.

## Reproduce

Open MainMenu and start the numbered levels through the map. `Diceforge → Demo RC → Configure levels 04-06` writes the authored presets and creates missing proxy assets; existing art is retained. The woodland rollout/rebalance preserves demo hero counts. Run the DemoCampaignTests, DemoHazardTests and DemoCampaignFlowTests in EditMode; the latter enters PlayMode and restores the original profile/language afterwards.
