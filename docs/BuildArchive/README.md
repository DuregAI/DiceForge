# Test build 1.3-alpha

MusicLibrary_Default now references only Goblin Woodland Adventure (all contexts). Old music files remain in Assets but are not dependencies of enabled build scenes or Resources. To restore the previous playlist, copy MusicLibrary_Default.asset.txt from this folder over Assets/_Project/08_Audio/MusicLibrary_Default.asset, preserving the existing meta file.

finish-flag_old.png and its meta are archived here, outside Assets. Old background-landscape.png and background-portrait.png remain in the art folder but are not scene/Resources dependencies. background-blink.png and hat-blink-source.png are required for eye animation and must remain available.

New texture import limits: flag 512; logo, settings panel and transition cloud 1024. Source images are preserved. Non-power-of-two textures still import as RGBA32 on the current target, so the confirmed texture saving comes from reduced dimensions, not GPU compression. Music uses Vorbis quality 0.6 and Compressed In Memory.

Checked in Unity: menu renders, the new track plays, and the scene/Resources dependency scan contains only that MP3. Before: 57 MP3 dependencies, about 222 MiB of source files. After: one MP3, about 2.04 MiB source. These are source sizes, not measured WebGL download sizes. The user will build and test the actual website release.
