# Demo RC: airy narrative UI and RU/EN

Date: 2026-10-08. Unity 6000.6.0f1, UI Toolkit, connected Editor through Unity CLI.

## UI polish following the designer review

The layout is retained. The updated game captures below now show the calmer shared palette: milk surface `#F8F3E6`, dark green text `#263F36`, amber action `#E9B955`, and muted teal speaker names `#43877A`.

- `DemoPresentationTheme.uss` supplies the same color tokens to the HUD and narrative screens. `WoodlandHud.uss` and `DemoNarrativeShared.uss` use consistent neutral buttons, amber selected/primary states, warm focus edges, smaller corner radii, and a subtle warm panel edge.
- `DemoUiIcons.cs` draws pause, settings, help, close and next with a consistent two-pixel stroke. `DioramaHud.cs` and `DemoNarrativeController.cs` attach those icons; both UXML files include the shared theme. The final Continue button removes the icon from its hierarchy so the localized text controls its natural width.
- Turn status has less visual weight, and the long Russian L6 title fits on one line in landscape. Help sizes to its contents and retains scrolling on smaller screens. Topic rows remain actual buttons that return to the corresponding hint.
- The UI/UX reviewer accepted the fresh hint, help, intro and L6 captures; no required visual corrections remained in this pass. This is screenshot review, alongside the runtime checks below.

Before/after evidence is retained in `BeforeUiPolish/`. Polish-specific test records are `ui-polish-narrative-test-status.json` (catalogue and presentation), `ui-polish-presentation-final-status.json` (repeat after the Continue width correction), and `ui-polish-campaign-test-status.json` (six-level campaign). No new tests or packages were added for this styling pass.

The polish checks passed: catalogue/presentation **2/2**, final presentation repeat **1/1**, and final full campaign **1/1**. The repeat campaign regenerated its captures after the Continue correction. Final Editor state: outside Play Mode, not compiling; no compilation failure and zero errors in the current Editor console. Existing scene and Animator warnings remain.

## Delivered

- EN `luma` is **Jo**; RU remains **Лума**. Gameplay IDs and existing progression remain compatible.
- Complete companion translation: 138 dialogue events, 35 comic lines, seven scene titles, 21 frame titles and 12 speaker names. See [English source](../../DemoRC/v0.1/data/dialogues.en.json) and [authoring rules](../../DemoRC/v0.1/data/README.localization.md).
- One cream surface for hints and actions; larger transparent waist-up profiles. In portrait the profile rises above the surface and copy uses its full width. In landscape the profile occupies the right-hand region. Modern matching help/pause/settings buttons and Nunito Medium 500.
- One comic frame and one speaker line at a time. Navigation walks every line before changing frames. Intro/victory conversations show a large speaking profile.
- Language changes preserve the current line/frame and pending hint. HUD names, instructions and buttons refresh with the same `ui.language` preference.
- The DEMO camera fits the trail into the area between the upper HUD and the actual lower surface; longer translated hints cannot hide the tile centres. Character graphics ignore input. Comic/intro/victory sequences block gameplay and hide the HUD.
- Six speaking profiles were imported with native Sprite Editor data providers. Neutral portraits and profile bindings survive narrative reimport. [Art sources and prompts](../../Art/DemoNarrativeRedesign/README.md).

## Verification

The catalogue and presentation checks pass **2/2**: [raw status](narrative-test-status.json). Checks cover complete RU/EN data, preserved portrait/profile references, S00 line/frame ordering, language changes during comic/intro/victory, button hit-testing, shared hint USS, all eight tile centres above the lower surface, and HUD language consistency.

The test renders actual **1280×720 and 720×1280 Game View resolutions**. PNG dimensions are checked. It saves and restores the user's language, profile and Game View size; gameplay uses an isolated temporary profile store.

The full six-level campaign test passes **1/1**, including all S00–S06 scenes, per-level exits, named hero selection and exactly-once progression saves. [Raw status](campaign-test-status.json), [campaign captures](Campaign/). The clean run has zero console errors and no compilation failure. Existing scene initialization and missing Animator reaction-state warnings remain in the retained 3D cast. An initial Test Runner cleanup failure was cancelled and rerun cleanly; it is not reported as a gameplay pass.

## Actual game captures

These are Editor renders, not design mockups. For each screen, both languages and orientations are saved.

| Screen | English landscape | English portrait | Russian landscape | Russian portrait |
|---|---|---|---|---|
| Contextual hint | [EN](presentation-test-L1-hint-Jo-en-landscape.png) | [EN](presentation-test-L1-hint-Jo-en-portrait.png) | [RU](presentation-test-L1-hint-Jo-ru-landscape.png) | [RU](presentation-test-L1-hint-Jo-ru-portrait.png) |
| Comic S00 | [EN](presentation-test-S00-comic-Jo-en-landscape.png) | [EN](presentation-test-S00-comic-Jo-en-portrait.png) | [RU](presentation-test-S00-comic-Jo-ru-landscape.png) | [RU](presentation-test-S00-comic-Jo-ru-portrait.png) |
| Intro conversation | [EN](presentation-test-L1-intro-Jo-en-landscape.png) | [EN](presentation-test-L1-intro-Jo-en-portrait.png) | [RU](presentation-test-L1-intro-Jo-ru-landscape.png) | [RU](presentation-test-L1-intro-Jo-ru-portrait.png) |

## Scope and remaining art work

The original scene illustrations remain the source. Landscape preserves the foreground action and fills the side regions with enlarged, darkened scenery from the upper part of the same frame. A separately authored wide scene could improve this composition later. Profiles use authored speaking expressions and subtle breathing, with Reduced Motion support; lip-sync and a full emotion set are future art work.

Validation is in the Unity Editor; no device build was requested. 3D campaign model replacement remains paused as instructed. Existing Stage3 and Stage5 evidence is preserved; current campaign captures go under `Campaign/`.
