# Menu languages

The main menu, settings, legal notice and feedback form support English, Russian and Simplified Chinese. The cloth language flags beside the sound button cycles EN → RU → zh-Hans → EN. Selection is stored in PlayerPrefs under `ui.language`; first launch defaults to English and unknown saved values fall back to English. No profile or campaign schema changes are involved.

Translations live in `Assets/_Project/03_UI/MainMenu/Resources/Localization/menu.json` and `Localization/legal.json`. Each row contains the original English string (`en`) and translations (`ru`, `zh`). English strings currently serve as catalog keys: update both the source and catalog when changing wording. Bindings capture the original string once, and dynamic sound/feedback/version messages use `T`. Missing translations fall back to English. Feedback category identifiers sent to the backend stay unchanged; only their displayed labels are translated.

The localization view binds MenuPanel, SettingsPanel, LegalPanel and FeedbackModal. Map, battle, tutorial content and other progression windows are outside this iteration. A translated HOW TO PLAY button does not imply translated tutorial content.

English retains Alfa Slab One. Russian action buttons use Roboto Slab; Noto Sans CJK SC supplies body text and Chinese glyphs. Fonts are bundled locally, with their licenses alongside them; switching languages does not require network access. The logo remains Glimblehop in every locale. Flag badges are drawn as vectors and do not intercept pointer events from the parent button.

Validation: inspect all three menu states, Russian/Chinese settings, return to the English font, saved-language restoration on a fresh Play session, and mute/unmute text updates. The existing SpacetimeDB offline connection warning is unrelated to localization.

Release font fix: assign the bundled font directly to localized TextElements, because .gh-settings-title explicitly assigns Luckiest Guy and overrides inherited fonts. On returning to English, clear that inline override. Verified with FontEngine: Luckiest Guy lacks НАСТРОЙКИ and 设置; bundled Noto covers all 428 distinct non-whitespace characters in the Russian/Chinese menu and legal catalogs. Russian and Chinese headings and Chinese legal were inspected in Play Mode. A fresh WebGL player build was not produced in this check.
