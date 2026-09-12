# Glimblehop — main menu slices v1

15 PNG assets for a static layered menu are saved in [Assets/_Project/07_Art/UI/GlimblehopMenu](../../../Assets/_Project/07_Art/UI/GlimblehopMenu). The live game menu and its scripts are unchanged.

- [Preview both layouts](preview.html): live English text over separate assets; hover/press demonstration and an audio icon toggle. Navigation buttons are illustrative.
- [Contact sheet](contact-sheet.png): exported artwork on a light background for alpha inspection.
- [Manifest](manifest.json): pixel sizes and source crop rectangles (top-left origin).

## Contents

| Asset | Purpose |
|---|---|
| background-landscape.png | 1672 x 941 opaque landscape plate |
| background-portrait.png | 941 x 1672 opaque portrait plate |
| logo.png | Transparent GLIMBLEHOP wordmark, 2025 x 709 |
| button-primary.png | Blank green button, 447 x 159 |
| button-secondary.png | Blank cream button, 378 x 157 |
| counter-panel.png | Blank brown capsule, 374 x 158 |
| settings.png | Settings control |
| audio-on.png / audio-off.png | Audio control states |
| avatar.png | Framed goblin portrait |
| coin.png | Wallet icon |
| play-arrow.png | Separate primary-action arrow |
| forest.png | Chapter icon |
| progress-active.png / progress-idle.png | Chapter progress markers |

## Unity import and assembly

Importer metadata follows the project's existing UI Toolkit texture schema: Texture (Default), sRGB, alpha transparency enabled, no mipmaps, no NPOT rescaling, bilinear filtering, clamp, uncompressed, maximum 4096. These are UI Toolkit background textures, not Sprite-mode assets. For uGUI, change import type to Sprite (2D and UI), Single, Full Rect before use.

Use the correct background for orientation. Position logo and controls separately; draw the tagline, PLAY, MY GOBLINS, HOW TO PLAY, level, wallet value and chapter caption using real text. Respect device safe areas. The supplied HTML is a composition guide, not a tested Unity UXML implementation.

Keep icons square without distortion and preserve the logo aspect ratio. Button borders are not authored for automatic nine-slicing: start with proportional scaling. Primary leaves and detailed wood grain will distort under large stretches. A dedicated 9-slice pass is needed for unrestricted button dimensions. Hover/press states can use a modest tint and scale rather than separate redrawn sprites.

## Fidelity and limits

The original concepts were flattened images. Built-in imagegen reconstructed hidden background and regenerated isolated UI, so these are matching artwork rather than pixel-identical PSD layer extraction. The first UI sheet contained painted checkerboard; it was rejected and replaced with an alpha extraction. 13 cutouts have real transparent pixels and were inspected on a light contact sheet; both backgrounds are opaque. Small antialiasing/soft alpha remains at artwork edges.

Characters, path, signs, tokens and foliage remain baked into the background plates. They cannot move independently in this kit. Sprite resolutions are listed honestly in the manifest; no artificial 4K upscaling was applied. Large desktop buttons may need a higher-resolution art pass if displayed well above native size.

This is a static menu asset handoff, not a rigged animation pack or completed menu integration. The preview uses system Georgia as a typography placeholder. Reuse an approved project font when implementing.

## Reproduce

Source PNGs are in Sources. Run `./docs/Art/Glimblehop-menu-kit-v1/Export-Slices.ps1` from PowerShell on Windows to repeat lossless crops, metadata and contact sheet export. Existing asset GUIDs are preserved. Fixed source dimensions/regions are intentional and must be reviewed if the source sheet changes.

Artwork used the built-in imagegen tool; no paid CLI fallback. Generation instructions are recorded in [generation-notes.md](generation-notes.md). Image editing was performed by imagegen; PowerShell only exports the requested slices and diagnostic contact sheet.
