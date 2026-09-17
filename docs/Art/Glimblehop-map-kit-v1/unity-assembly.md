# Step 4 — Unity presentation assembly

Step 5 is now integrated into the normal game menu. See progress-integration.md for current wiring and runtime verification. The stylesheet now lives at Assets/_Project/Resources/Map/WoodlandMap.uss. Details below record the original isolated presentation stage.

Open **Glimblehop > Map > Preview Woodland Map**. The window creates the production `WoodlandMapView` with the new stylesheet. Preview-only controls sit outside the game surface. Set Completed to 0, 2 or 6 and use the viewport buttons. Actions report requests without loading battles or writing saves.

`WoodlandMapView` accepts a StyleSheet, exposes Root, SetProgress, LevelRequested (1-based level number), BackRequested and Dispose. This presentation can be attached to the menu UIDocument in step 5. No legacy chapter or save migration was performed here; the existing game map remains active until binding is implemented.

## Typography and layout

- Uses the project's AlfaSlabOne-Regular, normal weight, consistent with main actions in the current menu.
- Live text: title 27, chapter/counter caption 23, level/progress numbers 31, action 28 reference units. No text is part of a texture.
- Authoring canvas 1672 x 941. One uniform top-left-origin transform fits the entire canvas into its host. Terrain, platforms and chrome share it, so resizing cannot move nodes relative to the path. Additional space uses a dark forest matte.
- Primary art remains 447 x 159 with `contain`; title/back/counter also preserve aspect. Styles are scoped under woodland-map, without menu button class inheritance or slice stretching.
- Preview verified at approximately 1280 x 720, 1024 x 768 and 844 x 390. Device-pixel rounding can produce sub-unit differences. Portrait is letterboxed, not a newly designed portrait composition. Small portrait touch targets are not certified.

## Actual Unity checks

Compiled in connected project editor 6000.6.0f1 (ProjectVersion.txt matches; AGENTS baseline is older). No project upgrade performed.

Measured with UI Toolkit MeasureTextSize: title 284.8 / 310.4 available; PLAY LEVEL 3 212.8 / 268; BACK TO MENU 248.8 / 268. Resolved font was AlfaSlabOne-Regular. Uniform button ratio remained approximately 2.81. State checks: 0 and 2 completed each enable one node; 6 completed enables none. Keyboard NavigationSubmit on the primary button emitted LevelRequested(3).

No reliable Unity pixel capture was obtained; checks above are real editor geometry/text/event checks, not a claimed screenshot review. Generated static PNG previews in this directory predate this UI and are not engine captures. Existing unrelated compiler warnings remain.

Hero, flowing water, progression binding and scene navigation remain their planned later steps.
