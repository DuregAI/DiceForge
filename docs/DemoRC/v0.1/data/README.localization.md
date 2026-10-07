# Demo narrative languages

The Russian master is `dialogues.json` plus the dialogue lines and headings in `../03_LEVEL_SCRIPT_STORYBOARDS.md`. The English runtime source is `dialogues.en.json`. The original draft `text.en` fields in the Russian authoring specification are retained for provenance; the importer uses the complete English companion instead.

The companion contains 138 dialogue events, 35 comic lines, seven scene titles, 21 frame titles, and 12 speaker names. A localized name never changes a speaker ID. In particular, `luma` displays as **Лума** in Russian and **Jo** in English; `bum` is **Бум / Boom** and `ryzh` is **Рыж / Rusty**. On L1–L2 the label is **Совет Лумы / Jo's advice**, because she has not joined the playable party yet.

Run **Diceforge → Demo RC → Import story and dialogue** outside Play Mode after changing either language source. The importer rejects missing/duplicate/extra English IDs, empty translations, and mismatched source versions before updating the catalog. It preserves both the existing neutral portraits and the larger dialogue profiles.

Russian `ru` and `title` fields remain compatible with existing consumers. Runtime presentation should call `DemoDialogueEvent.Text(russian)`, `DemoStoryLine.Text(russian)`, `DemoStoryScene.Title(russian)`, and `DemoStoryFrame.Title(russian)` with the existing language setting. Use the localized catalog speaker name; do not render internal IDs as names.

English copy uses **tile** for a landing position, **step** for a numbered choice, and **turn** for the whole turn. L1–L4 use one chosen step; L5–L6 use two separate steps. A blocked landing does not consume a step. Friends may share a tile, and a step may reach or pass the exit. The beetle and Rusty rules remain unchanged.
