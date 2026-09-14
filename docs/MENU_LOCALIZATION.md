# Menu languages

The main menu, settings and feedback form support English, Russian and Simplified Chinese. The wooden language badge beside Settings cycles EN → RU → zh-Hans → EN. Selection is stored in PlayerPrefs under `ui.language`; first launch defaults to English and unknown saved values fall back to English. No profile or campaign schema changes are involved.

Translations live in `Assets/_Project/03_UI/MainMenu/Resources/Localization/menu.json`. Each row contains the original English string (`en`) and translations (`ru`, `zh`). English strings currently serve as catalog keys: update both the source and catalog when changing wording. Bindings capture the original string once, and dynamic sound/feedback/version messages use `T`. Missing translations fall back to English. Feedback category identifiers sent to the backend stay unchanged; only their displayed labels are translated.

The localization view binds only MenuPanel, SettingsPanel and FeedbackModal. The legal document, map, battle, tutorial content and other progression windows are outside this iteration. A translated HOW TO PLAY button does not imply translated tutorial content.

English retains Alfa Slab One. Russian action buttons use Roboto Slab; Noto Sans CJK SC supplies body text and Chinese glyphs. Fonts are bundled locally, with their licenses alongside them; switching languages does not require network access. The logo remains Glimblehop in every locale. Flag badges are drawn as vectors and do not intercept pointer events from the parent button.

Validation: inspect all three menu states, Russian/Chinese settings, return to the English font, saved-language restoration on a fresh Play session, and mute/unmute text updates. The existing SpacetimeDB offline connection warning is unrelated to localization.
