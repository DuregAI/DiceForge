# GlimbleHop loading screen

## Preview locally without building Unity

Double-click `preview.html` in this folder. It works directly from disk in Edge/Chrome; no server or installation is required.

- Play / Pause: simulate downloading over 15 seconds.
- Slider: inspect any stage, including moving backwards.
- Restart: grow the ivy again from zero.
- Finish: bloom completely and fade away. The empty canvas afterwards is expected in this visual preview.

Controls and simulated progress exist only in this preview. The shipped template uses Unity's actual loading callback and reserves 100% for successful initialization. The preview shares the production CSS, JavaScript and artwork. Its embedded markup mirrors the template; update it when changing template markup.

## Check the real game

Build and Run a Web build in Unity. The selected WebGL template is `PROJECT:GlimbleHop` (Player Settings > Resolution and Presentation > Web Template). For repeat loading checks, open the browser's developer tools, disable cache, and throttle the network before reloading. The Unity Editor Play button does not run the HTML loading screen. Opening a built game's index.html directly from disk is not a valid Web build test; use Build and Run's local server.

## Files

- `Assets/WebGLTemplates/GlimbleHop/index.html`: Unity template and loader wiring.
- `TemplateData/loading.css`: layout and motion; separate portrait artwork on narrow screens.
- `TemplateData/loading.js`: wooden SVG sign, growing ivy, leaves, blossoms, completion and error handling.
- `TemplateData/loading-garden.webp` and `loading-garden-portrait.webp`: optimized original illustrations.
- Source PNGs and generation prompts are archived in this folder. Images were created with the built-in image generation tool using the existing heroes as identity/style references.

Validation: browser visual inspection at desktop and phone sizes; local-file preview controls; monotonic real progress; 99% initialization cap; success fade; error and retry states. A full Unity Web rebuild has not been run for this revision.
