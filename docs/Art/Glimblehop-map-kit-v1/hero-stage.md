# Map hero — completed first presentation stage

Update: the user subsequently supplied animation footage. The map now uses a sprite adaptation instead of this 3D presenter. See ../Glimblehop-map-hero-sprites-v1/README.md. The implementation below remains in the project as a fallback/history; it is not active on the map.

The map now uses the project's existing 3D goblin with its skeletal breathing/head idle. A short greeting turn plays when the map opens. WoodlandHeroPresenter renders the model into a transparent 384 × 384 texture at up to 30 fps; the texture scales uniformly with the map. A separate model-only prefab references existing meshes, materials and the Goblin_Board animator controller, without battle movement scripts. No new package or generated character asset was required.

## Placement and lifetime

- The figure follows the current available level. At 6/6 it remains beside the final stone.
- Levels 1–4 use the stone top; levels 5–6 use nearby ground below the title, keeping the numbers visible. The sixth-level figure stands to the left, away from the large background rock.
- The portrait and its shadow ignore pointer picking. Fonts and button sizes are unchanged.
- A remote studio on layer 30 owns the model, two local lights and the portrait camera. It is destroyed, with the render texture released, when the map closes, its owner is disabled/destroyed, or the scene changes.
- BuildWoodlandHero.cs is a manually invoked authoring helper outside Assets. It creates Resources/Map/WoodlandHero.prefab from GoblinValidationPrefab without modifying the battle prefab.

## Changed runtime files

- Assets/_Project/03_UI/Map/WoodlandHeroPresenter.cs — portrait rendering, greeting and resource ownership.
- Assets/_Project/03_UI/Map/WoodlandMapView.cs — noninteractive hero image/shadow and current-level placement.
- Assets/_Project/03_UI/Map/MapController.cs — starts/stops the presentation with the map.
- Assets/_Project/Resources/Map/WoodlandMap.uss — figure/shadow styles.
- Assets/_Project/Resources/Map/WoodlandHero.prefab — model-only resource.

## Verified in Unity 6000.6.0f1 through Unity CLI

- Final compilation completed without errors.
- Actual Game View inspected: hero-live-map.png and hero-level-1/5/6.png.
- VerifyWoodlandHero.AllAnchors checked states 0/6 through 6/6 using the actual nontransparent rendered silhouette: no texture-edge clipping, no overlap with title or any level number, within the map stage. It changes presentation only and restores the real map state in finally.
- VerifyWoodlandHero.Check confirmed animator time advances and the Head bone rotates (approximately 2.2 degrees across the sampled half-second), a single portrait camera exists, and camera/studio/image resources are cleared after Back.
- Live PLAY opened the map at the user's existing 3/6, launched C1_04 in Battle, and returned without reporting a result or awarding rewards. Hero and animation were recreated; the cleanup test passed again after return.
- Existing text measurements still fit: title 285/310 px; PLAY LEVEL 4 215/268 px; progress 73/216 px.
- Git diff whitespace check passed. No progression logic was changed; this stage did not simulate victories or alter the user's save.

The map still uses a static background. Walking along the route, river motion and other environmental effects are separate later work. This stage delivers the placed, animated 3D figure; it does not claim a completed walking/path system or a mobile performance benchmark.
