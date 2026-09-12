# Generation instructions and source provenance

Tool: built-in imagegen. Reference: approved Glimblehop menus in ../Glimblehop-v1. All requests preserve warm clay/wood art direction and exclude dice.

## Background plates

Edit the approved menu into a clean background plate at its original orientation. Preserve exact scene placement, three goblins, forest/rest/potion signs, tokens 1/3/5, path and warm lighting. Remove all screen-space UI: logo and boot, subtitle, profile, counters, gear, buttons including their shadows, chapter labels/dots and audio. Inpaint the landscape left area as calm defocused moss-green forest. In portrait, restore the top forest and bottom wooden tabletop. Keep scene composition; no new objects. Output opaque PNG.

Landscape generated source: exec-432ecf19-9b5a-45ea-bcb1-30e95a6ee276.png.
Portrait generated source: exec-53282a79-e36c-4ea6-8821-3476fe662cf8.png.

## Logo

Extract/recreate ONLY the exact GLIMBLEHOP wooden logo as standalone UI asset with true transparent background. Preserve spelling, cream carved letters on dark wood, small leaves and boot with motion strokes. No tagline, scene, characters or controls. Center complete silhouette with transparent padding on wide canvas. Keep clean antialiased edges.

Generated source: exec-52cb14e6-cde7-4e4c-b950-c07c482168e1.png.

## UI sheet

Create 12 separate frontal UI objects in a 3-column by 4-row grid with transparent gutters and no labels. Row 1: blank green primary, blank cream secondary, blank brown capsule. Row 2: settings gear, audio-on, leaf coin. Row 3: framed goblin portrait, cream play arrow, cream forest icon. Row 4: active progress dot, idle progress dot, muted audio. Match approved menu wood materials. No text baked into blank controls.

Initial sheet exec-10a619a5-93d1-4d82-b505-1bf2c7bfc1dd.png contained an opaque checkerboard and was rejected. Follow-up: BACKGROUND REMOVAL ONLY. Remove all painted checkerboard and export true transparent RGBA; preserve all 12 objects, layout, colors and shapes, keep interiors opaque and outside pixels transparent. No replacement background.

Final source: exec-2ac6719b-e55b-47fd-aead-69b6eeb63e71.png. Final image dimensions 1254 x 1254; export rectangles use actual output dimensions, not the requested nominal size.
