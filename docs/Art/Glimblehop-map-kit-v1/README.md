# Glimblehop map kit v1 — static graphics ready for assembly

## Delivered
Seven textures in Assets/_Project/07_Art/UI/GlimblehopMap: clean landscape background, blank stone platform, completed badge, locked badge, blank title plaque, back button and separate glow. Importer metadata follows the existing UI Toolkit Texture schema: sRGB, alpha transparency, no mipmaps, clamp, no NPOT rescaling, uncompressed, maximum 4096. These are Texture assets, not Sprite-mode assets.

Reuse button-primary.png, counter-panel.png and play-arrow.png directly from Assets/_Project/07_Art/UI/GlimblehopMenu. Their alpha channels were checked. All labels and numbers must be live UI text. Scale art proportionally; unrestricted nine-slicing has not been authored.

## Verification
contact-sheet.png shows all six cutouts on light and dark backgrounds, visually inspected after extraction. assembled-preview.png composes the actual separate assets. No hero is included; 3D placement is a later stage. Preview typography is Georgia, not the final game font.

manifest.json records actual pixel sizes. layout-landscape.json records image-relative node centers with TOP-LEFT origin, number offsets and draft hero-foot offsets. Convert Y for the existing bottom-left convention. All layers must share the background bounds. All cutouts have real transparent pixels and opaque/high-opacity content. Soft edges are retained. Touch sizes still require Unity checks.

## Reproduction
Built-in imagegen produced the background and source sheet; prompts.json retains exact prompts and unsuccessful transparency retries. The user explicitly authorized programmatic removal. export-assets.py uses Pillow and NumPy to segment the neutral exterior checkerboard, preserve interior shading, export PNGs and metadata, and generate contact/composition previews. It rebuilds the glow analytically to remove checkerboard contamination in soft alpha. Run with bundled Python. Windows Georgia is used for preview text.

Sources/map-ui-sheet.png and Drafts/*-OPAQUE.png retain the original background as exporter inputs. They are NOT production assets. Production textures are in the Assets folder above. Artwork is regenerated to match the concept, not pixel-identical PSD extraction.

## Scope
This completes the core STATIC graphics for step 3. Water, bridge and foliage remain baked into the clean background. Animation masks, waterfall overlays, bridge-rail occlusion and hero belong to the later motion stage; this is not an animation pack. Portrait composition remains a separate layout pass.

No gameplay scripts, saves or active scenes changed. Next: Unity assembly with separate layers, live text and shared coordinates, then progress binding.
