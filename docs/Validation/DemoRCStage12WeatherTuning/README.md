# Weather tuning

- `Assets/_Project/03_UI/WorldSelection/WorldAtmosphere.cs`: independent weather-group travel above each island; precipitation follows its cloud. The smaller woodland cloud moves on its own. Rain particles increased 22 → 42; snow 20 → 34. Stronger, faster rain and larger outlined snowflakes improve visibility.
- `Assets/_Project/Resources/WorldSelection/WorldSelection.uss`: clouds enlarged roughly 25%, with responsive positioning kept inside the island columns.

Validation: full six-level campaign test passed 1/1, including animation visibility/pause/resume and existing navigation/replay checks. Compilation passed. Reviewed RU/EN captures at 1280×720 and 720×1280. `worlds-weather.mp4` is recorded Unity output (10 fps preview). Session capture flags cleared.
