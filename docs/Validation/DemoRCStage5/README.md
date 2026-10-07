# Demo RC Stage 5 — персонажи, промежуточная проверка

Дата: 7 октября 2026. **P1 ещё не закрыт**: портреты подключены, пять процедурных 3D-кандидатов собраны отдельно для сравнения. Финальные модели, дополнительные реакции и приёмка игровых стеков впереди.

- [campaign-flow.json](campaign-flow.json): существующий тест `DemoCampaignFlowTests.MapToAllSixLevelsSavesExactlyOnceAndKeepsNamedHeroes` прошёл **1/1**, полный проход L1–L6 со сценами и сохранением. Используется временный изолированный профиль; язык и Random state восстанавливаются тестом.
- [L1-luma-portrait-landscape.png](L1-luma-portrait-landscape.png): настоящий Unity Game View 1920×1080 после подключения портретов. Лума отображается в подсказке, соседние ячейки атласа не видны, цветного непрозрачного контура нет. Это landscape-проверка UI; portrait-поле с финальными моделями ещё не проверено в этом этапе.
- [unity-assets.json](unity-assets.json): пять кандидатов с восьмикостным generic rig, одним материалом, idle loop и root motion off; CharacterWalk сохраняет bone positions/scale, вращения отличаются от idle. Для Жука Walk намеренно равен idle. Все 12 neutralPortrait ссылаются на правильные Sprite.
- Финальный readback: **5/5** модельных проверок и **12/12** ссылок портретов; position/scale delta = 0 у всех моделей, максимальная разница вращения idle/walk = 8.477° у гоблинов. Компиляция без ошибок; Unity Console после проверки — 0 errors, 2 warnings. `git diff --check` проходит, все новые Unity-ассеты имеют `.meta`.
- Blender FBX повторно импортирован: **5/5** по [fbx_validation.json](../../Art/DemoRCCharacters/fbx_validation.json).

Предыдущие снимки Stage 3 восстановлены после теста; новый портретный снимок хранится здесь. [Импорт, промпты и выбор сервиса](../../Art/DemoRCCharacters/README.md).

Следующее действие — пробный Бум через Meshy 7.1/T2 после подключения доступа. Затем art review, очистка сетки/UV, skinning, настоящие idle/walk/reactions и проверка выбранной Лумы рядом с Бумом, отдельного Рыжа и Жука на клетке 4 в landscape/portrait. Доступ внешней генерации сейчас не настроен, live-запросы и покупки не выполнялись.
