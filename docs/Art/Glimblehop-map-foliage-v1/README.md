# Woodland foliage, first review version

Three copies of one generated shrub sit at reference-space ground anchors (174,476), (687,637), (1432,603). They share the map stage scaling and sit below nodes and hero. PickingMode.Ignore preserves clicks. Each rotates around its base at a different phase; no frame-by-frame allocations or texture rebuilding. The 30 Hz schedule pauses when the map hides and when the view is disposed.

Inspector: MainMenuUIDocumentAndController > Map Controller > Foliage Settings. Enabled, Sway Degrees, Speed, Gust Strength, Size and Opacity update during Play Mode. Use Sway Degrees = 0 for stationary visible plants; Enabled = false hides them. To retain Play Mode values, Copy Component, stop Play, Paste Component Values, save scene. This copies the entire Map Controller, including water settings.

Asset: Assets/_Project/Resources/Map/Foliage/woodland-shrub.png. Built-in imagegen generated the source. The tool returned a baked neutral checkerboard; export_foliage.py removes that neutral backdrop, crops and downsizes the selected source. Other generated variants were discarded. ImportFoliage.cs sets alpha, clamp and bilinear sampling without mipmaps or compression. Original map art and progression are unchanged.

Visual approval is intentionally left to the user. Check placement, brightness and wind strength in Game view.
