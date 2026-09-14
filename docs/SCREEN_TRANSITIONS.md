# Cloud screen transitions

The shared `Diceforge.Transitions` assembly owns full-screen navigation. Eight textured cloud banks close from the edges, content changes while completely covered, then the clouds reveal the destination. Modal settings, legal information, confirmations and reward popups retain their local animations.

## Artist settings

Select `Assets/_Project/03_UI/Transitions/Resources/Transitions/CloudTransitionSettings.asset` in Unity. Values apply globally, including when starting directly in Battle or Tutorial.

| Field | Meaning | Default |
| --- | --- | --- |
| Close Duration | Total cloud closing time, seconds | 0.8 |
| Open Duration | Total cloud opening time, seconds | 0.95 |
| Covered Hold | Minimum hold after destination preparation, seconds | 0.12 |
| Stagger | Relative offset between cloud banks (0–0.4 of animation) | 0.15 |
| Cloud Texture / Tint | Shared cloud illustration and tint | authored texture / white |
| Coverage Color | Opaque fallback behind clouds | warm ivory |
| Reduced Motion | Short crossfade instead of moving clouds | false |
| Readiness Timeout | Timeout for an explicitly supplied readiness predicate | 30 s |

Main-menu title/button timings remain on MainMenuController > Atmosphere and were not changed. The old menu-only fade fields are no longer used. No per-scene transition object needs authoring; a persistent overlay is bootstrapped before the first scene.

## Integration contract

```csharp
using Diceforge.Transitions;

// Same-scene screen switch. Mutate content ONLY in the callback.
ScreenTransition.Switch(() => ShowDestination());

// Scene replacement, with payload prepared only after navigation is accepted.
ScreenTransition.LoadScene("Battle", () => SetBattleRequest(request));

// Optional readiness check for destinations with asynchronous setup.
ScreenTransition.Switch(() => BeginBuildingScreen(), () => screen.IsReady);
```

Both entry points return false while another navigation is active. Do not retry automatically or mutate navigation payloads before the accepted callback. Keep callbacks short; expensive scene work belongs in `LoadSceneAsync` or incremental destination initialization. Loading starts after a fully covered frame. After scene activation, two frames allow Start and UI layout to run; destinations doing longer asynchronous work must supply `ready`. A slow Unity scene load stays covered and shows Loading after two seconds. Unity scene loading is not treated as cancellable.

Startup code that restores the requested destination (for example return from a battle to its map) initializes it immediately under the existing cover; it must not start a nested transition. MainMenuController implements this in `OpenMapChapterImmediately`.

The lifecycle is Idle → Closing → Preparing → Opening → Idle. Callback or readiness exceptions display an error with Continue, log the cause, and let the user reveal the current screen. There is no automatic retry of callbacks with potential side effects. Invalid scene names are rejected before navigation. The UI layer persists across scene loads and releases its input callbacks on completion, disable and destruction.

Input is intercepted on UI document roots without changing enabled states or applying disabled tints. Direct board/keyboard polling checks `ScreenTransition.IsBusy`; new raw-input controllers must do the same. The battle update pauses while a transition is active. Time.timeScale is not changed; transition timing is unscaled. The input layer does not globally cancel third-party IMGUI or arbitrary scripts polling input.

Integrated routes: menu/map both ways, full-screen progression sections, tutorial entry/exit, battle entry, battle restart, battle results return, and standalone chest-screen return. Settings/legal and other dialogs are intentionally local overlays. All former `SceneManager.LoadScene` calls under `_Project` route through the service.

## Rendering and maintenance

Cloud movement uses UI Toolkit translate with DynamicTransform; sizes update only when viewport dimensions change. There are eight reused visual elements, one shared alpha texture and one opaque backing; no per-frame spawned objects. The backing forces alpha to one, guaranteeing full concealment independently of sprite margins and aspect ratio. A dedicated PanelSettings asset sorts above the game UI. The illustration is preloaded before first navigation.

Unity references: [asynchronous scene loading](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html), [sceneLoaded runs before Start](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager-sceneLoaded.html), [runtime transforms and DynamicTransform](https://docs.unity3d.com/6000.0/Documentation/Manual/UIE-move-elements-at-runtime.html).

## Validation

Isolated Play Mode assembly: `Diceforge.Transitions.Tests`: 9/9 tests passed. Covers concealed/exactly-once mutation, duplicate navigation, readiness wait, zero time scale, zero durations, failure dismissal, disable cleanup, invalid scenes, reduced-motion/missing-art fallback and UI input interception/release.

Unity 6000.6.0f1 integration checks: menu → map → menu; MainMenu → Tutorial → MainMenu → Battle → MainMenu restoring MapRoot. The same persistent service entity survived each scene load and returned to Idle. Inspected partial and full coverage at 1400 × 788 / 1000 × 562; full coverage has no visible underlying screen. Preview: `docs/Art/cloud-transition-preview.png`.

These checks do not constitute a full project test pass or target-device certification. Existing SpacetimeDB ThreadAbortException can appear during scene/editor teardown. Profile CPU/GPU on release targets, especially mobile, before shipping; transparent cloud overlap incurs overdraw. Validate intended portrait/ultrawide layouts and any new asynchronous destination readiness contract on those targets.

## Art source

Final asset: `Assets/_Project/03_UI/Transitions/Resources/Transitions/cloud-bank.png`. Generated with built-in Imagegen, preserving transparent alpha. Original generated file retained outside the repository.

Final prompt: "Use case: stylized-concept. Asset type: final game UI transition cloud sprite, genuinely transparent RGBA background. Create ONE large cohesive bank of fluffy sculpted cumulus clouds, roughly square silhouette with rounded irregular lobes on every side. Warm ivory lit tops, pale sage-gray and cream shaded undersides, subtle golden woodland sunlight, premium cozy fantasy game hand-painted 3D storybook look, soft voluminous detail and clean silhouette, not photorealistic weather. Entire central 60 percent is dense completely opaque cloud with no holes. Outer silhouette softly feathered only very slightly, no diffuse smoke. Cloud fills 85 percent of image, generous transparent margins, no cropping. No scenery, no text, no icons, no checkerboard, no background, no faces. This isolated cloud bank will be layered and moved inward from all four screen edges as a polished game scene transition."
