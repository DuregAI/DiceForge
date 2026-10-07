using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DemoNarrativePresentationTests
    {
        private readonly Dictionary<FieldInfo, object> savedProfileFields = new();
        private string testDirectory, originalLanguage;
        private bool hadLanguage;
        private UnityEngine.Random.State randomState;
        private UnityEditor.EditorWindow gameView;
        private object gameViewSizeGroup;
        private int originalSizeIndex;
        private readonly List<int> addedResolutionIndices = new();
        private static Type Transition => Type.GetType("Diceforge.Transitions.ScreenTransition, Diceforge.Transitions", true);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Play-mode instructions must be yielded directly to the test runner.
            yield return new EnterPlayMode();
            yield return null;
            Assert.That(Application.isPlaying, Is.True, "Presentation checks require a running Play Mode scene.");
            foreach (UnityEngine.Object client in UnityEngine.Object.FindObjectsByType(
                Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbLocalDevRuntime")))
                UnityEngine.Object.DestroyImmediate(((Component)client).gameObject);
            randomState = UnityEngine.Random.state;
            originalLanguage = PlayerPrefs.GetString("ui.language", "en");
            hadLanguage = PlayerPrefs.HasKey("ui.language");
            PlayerPrefs.SetString("ui.language", "en");
            Type service = Runtime.Type("Progression.ProfileService");
            foreach (string name in new[] { "_profile", "_store", "_lastSavedJson", "<LoadError>k__BackingField" })
            {
                FieldInfo field = service.GetField(name, Runtime.Members);
                savedProfileFields.Add(field, field.GetValue(null));
            }
            testDirectory = Path.Combine(Path.GetTempPath(), "DiceforgeNarrativeTests", Guid.NewGuid().ToString("N"));
            object store = Activator.CreateInstance(Runtime.Type("Progression.AtomicProfileStore"), Runtime.Members, null,
                new object[] { Path.Combine(testDirectory, "player_profile.json") }, null);
            object profile = Runtime.New("Progression.PlayerProfile");
            Runtime.Set(profile, "playerGuid", Guid.NewGuid().ToString());
            Runtime.Set(profile, "selectedAvatarId", Runtime.Static("Progression.AvatarService", "GetDefaultAvatarId"));
            object chapter = Runtime.New("Progression.ChapterProgress");
            Runtime.Set(chapter, "chapterId", "Chapter1");
            Runtime.Set(chapter, "runId", Guid.NewGuid().ToString("N"));
            object state = Runtime.Field(chapter, "state");
            Runtime.Set(state, "currentNodeId", "C1_01");
            Runtime.Call(state, "Unlock", "C1_01");
            ((IList)Runtime.Field(profile, "chapters")).Add(chapter);
            service.GetField("_profile", Runtime.Members).SetValue(null, profile);
            service.GetField("_store", Runtime.Members).SetValue(null, store);
            service.GetField("_lastSavedJson", Runtime.Members).SetValue(null, null);
            service.GetField("<LoadError>k__BackingField", Runtime.Members).SetValue(null, null);
            Runtime.Static("Progression.ProfileService", "RebuildCache");
            Runtime.Static("Progression.ProfileService", "Save");
            Runtime.Static("Map.MapFlowRuntime", "ClearRunContext");
            gameView = UnityEditor.EditorWindow.GetWindow(EditorType("UnityEditor.GameView"));
            originalSizeIndex = (int)Runtime.Get(gameView, "selectedSizeIndex");
            Type sizesType = EditorType("UnityEditor.GameViewSizes");
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            gameViewSizeGroup = Runtime.Get(sizes, "currentGroup");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (gameView != null)
            {
                Runtime.Call(gameView, "SizeSelectionCallback", originalSizeIndex, null);
                foreach (int index in addedResolutionIndices.OrderByDescending(i => i))
                    Runtime.Call(gameViewSizeGroup, "RemoveCustomSize", index);
                addedResolutionIndices.Clear();
                gameView.Repaint();
                yield return null;
            }
            foreach (var pair in savedProfileFields) pair.Key.SetValue(null, pair.Value);
            savedProfileFields.Clear();
            Runtime.Static("Progression.ProfileService", "RebuildCache");
            Runtime.Static("Map.MapFlowRuntime", "ClearRunContext");
            if (hadLanguage) PlayerPrefs.SetString("ui.language", originalLanguage);
            else PlayerPrefs.DeleteKey("ui.language");
            UnityEngine.Random.state = randomState;
            foreach (UnityEngine.Object client in UnityEngine.Object.FindObjectsByType(
                Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbLocalDevRuntime")))
                UnityEngine.Object.DestroyImmediate(((Component)client).gameObject);
            yield return new ExitPlayMode();
            if (!string.IsNullOrEmpty(testDirectory) && Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }

        [UnityTest]
        public IEnumerator StoryKeepsItsLineAcrossLanguagesAndHintLeavesActionsAccessible()
        {
            Runtime.Static("Map.MapFlowRuntime", "RequestReturnToMap");
            Transition.GetMethod("LoadScene").Invoke(null, new object[] { "MainMenu", null, null });
            yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && Find("Map.MapFlowOrchestrator") != null,
                "The main menu did not load.");
            yield return Until(() => !(bool)Transition.GetProperty("IsBusy").GetValue(null), "The menu transition did not finish.");
            Component map = Find("Map.MapFlowOrchestrator");
            Runtime.Call(map, "StartChapter", "Chapter1");
            yield return Until(() => Find("MapController") != null && !(bool)Runtime.Get(Find("MapController"), "IsHeroTravelling"),
                "Map travel did not finish.");
            Runtime.Call(map, "OnNodeSelected", "C1_01");
            yield return Until(() => SceneManager.GetActiveScene().name == "Battle" && Find("View.DioramaHud") != null &&
                Find("View.BattleDebugController") != null, "The first campaign level did not launch.");
            yield return Until(() => !(bool)Transition.GetProperty("IsBusy").GetValue(null), "The battle transition did not finish.");

            Component battle = Find("View.BattleDebugController");
            yield return Until(() => Runtime.Get(battle, "DemoNarrative") != null &&
                (bool)Runtime.Get(Runtime.Get(battle, "DemoNarrative"), "IsStoryVisible"), "The opening comic did not appear.");
            object narrative = Runtime.Get(battle, "DemoNarrative");
            var doc = (UIDocument)Runtime.Field(narrative, "document");
            VisualElement root = doc.rootVisualElement;
            var hud = (UIDocument)Runtime.Get(Find("View.DioramaHud"), "Document");
            Assert.That(Runtime.Get(narrative, "StoryId"), Is.EqualTo("S00"));
            Assert.That(Runtime.Get(battle, "PresentationCanInteract"), Is.False, "A comic must block gameplay.");
            AssertStory(narrative, root, 0, 0, "Tish", "Everything will be just right today.");

            Runtime.Call(narrative, "AdvanceStory");
            AssertStory(narrative, root, 0, 1, "Jo", "Good is good enough.");
            yield return ChangeStoryLanguage(narrative, root, "ru", 0, 1, "Лума", "Можно просто хорошо");
            yield return ChangeStoryLanguage(narrative, root, "en", 0, 1, "Jo", "Good is good enough.");
            yield return CapturePresentations(narrative, "S00-comic-Jo", root.Q<Label>("storyHeading"), "Jo", "Лума");
            AssertNoCyrillic(root.Q<Label>("storyHeading").text + root.Q<Label>("storyText").text);
            AssertInteractable(root.Q<Button>("storyNext"));
            AssertInteractable(root.Q<Button>("storySkip"));

            // Submit once through the actual button binding, rather than only calling its handler.
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = root.Q<Button>("storyNext");
                root.Q<Button>("storyNext").SendEvent(submit);
            }
            yield return null;
            AssertStory(narrative, root, 1, 0, "Kloch", "Just borrowing it until the celebration!");
            Assert.That(root.Q<Label>("storyPage").text, Is.EqualTo("2 / 3"), "A new frame must update the comic page.");
            Runtime.Call(narrative, "SkipStory");
            yield return Until(() => (bool)Runtime.Get(narrative, "IsStoryVisible") && Runtime.Get(narrative, "StoryId") == null,
                "Skipping the comic must open the level introduction.");
            AssertStory(narrative, root, 0, 0, "Tish", "I'll find the lens before Mira ties another bow on me.");
            Runtime.Call(narrative, "AdvanceStory");
            AssertStory(narrative, root, 0, 1, "Jo", "Start at the gate. One familiar step will take you toward the stream.");
            yield return CapturePresentations(narrative, "L1-intro-Jo", root.Q<Label>("storyHeading"), "Jo", "Лума");
            Runtime.Call(narrative, "SkipStory");
            yield return Until(() => (bool)Runtime.Get(narrative, "Ready") && (bool)Runtime.Get(battle, "PresentationCanInteract"),
                "Finishing the introduction must allow gameplay.");
            Assert.That(Runtime.Get(battle, "PresentationSelectedDie"), Is.EqualTo(0), "L1 initially selects its only available step.");
            Assert.That(((IEnumerable)Runtime.Get(battle, "PresentationDice")).Cast<int>().First(), Is.EqualTo(1));
            yield return Until(() => (string)Runtime.Get(narrative, "PendingHintId") == "L01_T02",
                "The initially selected step 1 must show the contextual hero-selection hint.");

            VisualElement dock = hud.rootVisualElement.Q("demoDialogueDock");
            Assert.That(dock, Is.Not.Null);
            Assert.That(dock.Q("demoHint"), Is.SameAs(Runtime.Field(narrative, "hintPanel")), "Hint copy must share the HUD action surface.");
            Assert.That(dock.Q<Label>("hintSpeaker").text, Is.EqualTo("Jo's advice"));
            Assert.That(dock.Q<Label>("hintText").text, Is.EqualTo("Now tap Tish on the outlined tile. He'll take the step you chose."));
            AssertNoCyrillic(dock.Q<Label>("hintSpeaker").text + dock.Q<Label>("hintText").text);
            Image profile = root.Q<Image>("hintPortrait");
            yield return Until(() => profile.sprite != null && profile.resolvedStyle.height > 150 &&
                dock.Q<Button>("hintSkip").worldBound.height > 0, "A large speaking profile did not lay out.");
            Assert.That(Runtime.Get(narrative, "IsModal"), Is.False, "Contextual hints must leave gameplay available.");
            Assert.That(Runtime.Get(battle, "PresentationCanInteract"), Is.True);
            AssertInteractable(dock.Q<Button>("hintSkip"));
            AssertInteractable(root.Q<Button>("demoHelpButton"));
            AssertInteractable(hud.rootVisualElement.Q<Button>("hero_tish"));
            AssertInteractable(hud.rootVisualElement.Q("moves").Q<Button>());
            AssertInteractable(hud.rootVisualElement.Q<Button>("pause"));
            AssertInteractable(hud.rootVisualElement.Q<Button>("settings"));

            PlayerPrefs.SetString("ui.language", "ru");
            yield return Until(() => dock.Q<Label>("hintSpeaker").text == "Совет Лумы", "Russian hint name did not refresh.");
            Assert.That(dock.Q<Label>("hintText").text, Is.EqualTo("Теперь нажми Тиша на подсвеченной плите. Он сам сделает выбранный шаг."));
            Assert.That(Runtime.Get(narrative, "PendingHintId"), Is.EqualTo("L01_T02"));
            PlayerPrefs.SetString("ui.language", "en");
            yield return Until(() => dock.Q<Label>("hintSpeaker").text == "Jo's advice", "English hint name did not refresh.");
            Assert.That(Runtime.Get(narrative, "PendingHintId"), Is.EqualTo("L01_T02"));
            yield return CapturePresentations(narrative, "L1-hint-Jo", dock.Q<Label>("hintSpeaker"), "Jo's advice", "Совет Лумы");

            // Complete the short trail so the real victory sequence exercises the same renderer.
            for (int move = 0; move < 8; move++)
            {
                Runtime.Call(battle, "SelectPresentationDie", 0);
                Runtime.Call(battle, "MovePresentationHero", "tish");
                yield return Until(() => !(bool)Runtime.Get(battle, "PresentationIsAnimating"), "Tish's movement did not finish.");
                yield return null;
            }
            yield return Until(() => (bool)Runtime.Get(narrative, "IsStoryVisible") && Runtime.Get(narrative, "StoryId") == null,
                "The victory dialogue did not appear.");
            AssertStory(narrative, root, 0, 0, "Tish", "First trail crossed. Scarf still here. A good start.");
            yield return ChangeStoryLanguage(narrative, root, "ru", 0, 0, "Тиш", "Первая тропинка пройдена. Шарф тоже на месте. Хорошее начало.");
            yield return ChangeStoryLanguage(narrative, root, "en", 0, 0, "Tish", "First trail crossed. Scarf still here. A good start.");
            Runtime.Call(narrative, "AdvanceStory");
            AssertStory(narrative, root, 0, 1, "Jo", "The trail leads to the stream. Look at Fika's drawing.");
        }

        private static IEnumerator ChangeStoryLanguage(object narrative, VisualElement root, string language,
            int frame, int line, string speaker, string text)
        {
            PlayerPrefs.SetString("ui.language", language);
            yield return Until(() => root.Q<Label>("storyHeading").text == speaker && root.Q<Label>("storyText").text == text,
                "The visible story line did not refresh after changing language.");
            AssertStory(narrative, root, frame, line, speaker, text);
        }

        private IEnumerator CapturePresentations(object narrative, string name, Label visibleSpeaker, string englishSpeaker, string russianSpeaker)
        {
            int frame = (int)Runtime.Get(narrative, "StoryFrame"), line = (int)Runtime.Field(narrative, "line");
            string hint = (string)Runtime.Get(narrative, "PendingHintId");
            string storyId = (string)Runtime.Get(narrative, "StoryId");
            string directory = Path.GetFullPath("docs/Validation/DemoRCStage6");
            Directory.CreateDirectory(directory);
            foreach (Vector2Int resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280) })
            {
                int index = FindOrAddResolution(resolution.x, resolution.y);
                Runtime.Call(gameView, "SizeSelectionCallback", index, null);
                gameView.Focus(); gameView.Repaint();
                yield return Until(() => Screen.width == resolution.x && Screen.height == resolution.y,
                    "The Game View did not switch to the actual requested render resolution.");
                foreach (string language in new[] { "en", "ru" })
                {
                    PlayerPrefs.SetString("ui.language", language);
                    yield return Until(() => visibleSpeaker.text == (language == "ru" ? russianSpeaker : englishSpeaker),
                        "The captured presentation did not refresh its language.");
                    for (int settle = 0; settle < 4; settle++) yield return null;
                    var hud = (UIDocument)Runtime.Get(Find("View.DioramaHud"), "Document");
                    AssertHudLanguage(hud.rootVisualElement, language == "ru");
                    if (visibleSpeaker.name == "hintSpeaker")
                    {
                        AssertHintStyles(hud.rootVisualElement.Q("demoDialogueDock"));
                        AssertTrailFitsVisibleViewport(hud.rootVisualElement);
                    }
                    Assert.That(Runtime.Get(narrative, "StoryFrame"), Is.EqualTo(frame), "Changing orientation must retain the frame.");
                    Assert.That(Runtime.Field(narrative, "line"), Is.EqualTo(line), "Changing orientation must retain the line.");
                    Assert.That(Runtime.Get(narrative, "StoryId"), Is.EqualTo(storyId));
                    Assert.That(Runtime.Get(narrative, "PendingHintId"), Is.EqualTo(hint), "Capturing must not change the pending hint.");
                    string orientation = resolution.x > resolution.y ? "landscape" : "portrait";
                    string path = Path.Combine(directory, "presentation-test-" + name + "-" + language + "-" + orientation + ".png");
                    if (File.Exists(path)) File.Delete(path);
                    ScreenCapture.CaptureScreenshot(path);
                    yield return Until(() => File.Exists(path) && new FileInfo(path).Length > 24, "The screenshot was not written.");
                    byte[] bytes = File.ReadAllBytes(path);
                    Assert.That(PngDimension(bytes, 16), Is.EqualTo(resolution.x), "Screenshot width must prove a real Game View render.");
                    Assert.That(PngDimension(bytes, 20), Is.EqualTo(resolution.y), "Screenshot height must prove a real Game View render.");
                }
            }
            PlayerPrefs.SetString("ui.language", "en");
            yield return Until(() => visibleSpeaker.text == englishSpeaker, "English was not restored after capturing.");
        }

        private static void AssertHintStyles(VisualElement dock)
        {
            var header = dock.Q(className: "hint-header");
            var body = dock.Q<Label>("hintText");
            var close = dock.Q<Button>("hintSkip");
            Assert.That(header, Is.Not.Null);
            Assert.That(header.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row),
                "Hint header must keep the speaker and dismiss button in one row; the shared USS may be missing.");
            Assert.That(body.resolvedStyle.whiteSpace, Is.EqualTo(WhiteSpace.Normal),
                "Hint text must wrap; an unstyled UXML subasset cannot replace the shared USS.");
            Assert.That(close.resolvedStyle.width, Is.InRange(1f, 50f),
                "Dismiss must remain a compact button instead of stretching across the dialogue.");
            Assert.That(body.resolvedStyle.fontSize, Is.GreaterThanOrEqualTo(18f),
                "Hint copy must retain its readable dialogue font size in both orientations.");
        }

        private static void AssertHudLanguage(VisualElement hud, bool russian)
        {
            Assert.That(hud.Q<Label>("demoTitle").text, Is.EqualTo(russian ? "Калитка у дома" : "Gate by the house"),
                "Changing dialogue language must also refresh the HUD title.");
            Assert.That(hud.Q<Label>("turn").text, Is.EqualTo(russian ? "ВАШ ХОД" : "YOUR TURN"));
            Assert.That(hud.Q<Button>("hero_tish").text, Is.EqualTo(russian ? "Тиш" : "Tish"));
            Assert.That(hud.Q<Label>("stepRule").text, Is.EqualTo(russian ? "Один шаг на ход" : "One step per turn"),
                "The HUD step rule must use the same language as the visible dialogue.");
        }

        private static void AssertTrailFitsVisibleViewport(VisualElement hud)
        {
            const float margin = 4f;
            Camera camera = Camera.main;
            Component board = Find("View.DioramaBoard");
            VisualElement header = hud.Q(className: "hud-header");
            VisualElement actions = hud.Q("demoActionSurface");
            Assert.That(camera, Is.Not.Null, "The diorama camera is missing.");
            Assert.That(board, Is.Not.Null, "The logical trail presentation is missing.");
            Assert.That(header, Is.Not.Null);
            Assert.That(actions, Is.Not.Null);
            float scaleX = hud.resolvedStyle.width / Screen.width;
            float scaleY = hud.resolvedStyle.height / Screen.height;
            Assert.That(scaleX, Is.GreaterThan(0f));
            Assert.That(scaleY, Is.GreaterThan(0f));
            for (int cell = 0; cell < 8; cell++)
            {
                Vector3 world = (Vector3)Runtime.Call(board, "CellPosition", cell);
                Vector3 screen = camera.WorldToScreenPoint(world);
                Assert.That(screen.z, Is.GreaterThan(0f), "Tile " + cell + " is behind the camera.");
                float x = hud.worldBound.xMin + screen.x * scaleX;
                float topDownY = hud.worldBound.yMin + (Screen.height - screen.y) * scaleY;
                Assert.That(x, Is.GreaterThan(hud.worldBound.xMin + margin), "Tile " + cell + " is outside the left edge.");
                Assert.That(x, Is.LessThan(hud.worldBound.xMax - margin), "Tile " + cell + " is outside the right edge.");
                Assert.That(topDownY, Is.GreaterThan(header.worldBound.yMax + margin),
                    "Tile " + cell + " is covered by the top HUD.");
                Assert.That(topDownY, Is.LessThan(actions.worldBound.yMin - margin),
                    "Tile " + cell + " is covered by dialogue or game actions.");
            }
        }

        private int FindOrAddResolution(int width, int height)
        {
            int count = (int)Runtime.Call(gameViewSizeGroup, "GetTotalCount");
            for (int index = 0; index < count; index++)
            {
                object size = Runtime.Call(gameViewSizeGroup, "GetGameViewSize", index);
                if ((int)Runtime.Get(size, "width") == width && (int)Runtime.Get(size, "height") == height &&
                    Runtime.Get(size, "sizeType").ToString() == "FixedResolution") return index;
            }
            object fixedResolution = Enum.Parse(EditorType("UnityEditor.GameViewSizeType"), "FixedResolution");
            object added = Activator.CreateInstance(EditorType("UnityEditor.GameViewSize"), Runtime.Members, null,
                new object[] { fixedResolution, width, height, "Demo RC validation " + width + "x" + height }, null);
            Runtime.Call(gameViewSizeGroup, "AddCustomSize", added);
            addedResolutionIndices.Add(count);
            return count;
        }

        private static Type EditorType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).First(type => type != null);

        private static int PngDimension(byte[] bytes, int offset) =>
            bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];

        private static void AssertStory(object narrative, VisualElement root, int frame, int line, string speaker, string text)
        {
            Assert.That(Runtime.Get(narrative, "StoryFrame"), Is.EqualTo(frame));
            Assert.That(Runtime.Field(narrative, "line"), Is.EqualTo(line), "Language changes must retain the current line.");
            Assert.That(root.Q<Label>("storyHeading").text, Is.EqualTo(speaker));
            Assert.That(root.Q<Label>("storyText").text, Is.EqualTo(text), "Only the current speaker's line must be visible.");
        }

        private static void AssertNoCyrillic(string text) => Assert.That(Regex.IsMatch(text, "[А-Яа-яЁё]"), Is.False,
            "English presentation must not fall back to Russian text.");

        private static void AssertInteractable(Button button)
        {
            Assert.That(button, Is.Not.Null, "A required action is missing.");
            Assert.That(button.enabledInHierarchy, Is.True, button.name + " must remain enabled.");
            Assert.That(button.resolvedStyle.display, Is.Not.EqualTo(DisplayStyle.None));
            Assert.That(button.worldBound.width, Is.GreaterThan(0));
            Assert.That(button.worldBound.height, Is.GreaterThan(0));
            VisualElement picked = button.panel.Pick(button.worldBound.center);
            Assert.That(picked == button || picked?.GetFirstAncestorOfType<Button>() == button, Is.True,
                button.name + " is covered by another visual element.");
        }

        private static Component Find(string type) => (Component)UnityEngine.Object.FindAnyObjectByType(
            type == "MapController" ? Type.GetType("MapController, Assembly-CSharp", true) : Runtime.Type(type));

        private static IEnumerator Until(Func<bool> ready, string message)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, message);
        }
    }

    public sealed class DemoNarrativeCatalogTests
    {
        [Test]
        public void EveryAuthoredLineAndTitleHasEnglishAndSpeakerArtIsPreserved()
        {
            object catalog = Resources.Load("DemoRC/Narrative", Runtime.Type("Dialogue.DemoNarrativeCatalog"));
            Assert.That(catalog, Is.Not.Null, "Import the bilingual demo narrative first.");
            object[] events = ((IEnumerable)Runtime.Field(catalog, "events")).Cast<object>().ToArray();
            object[] scenes = ((IEnumerable)Runtime.Field(catalog, "scenes")).Cast<object>().ToArray();
            object[] speakers = ((IEnumerable)Runtime.Field(catalog, "speakers")).Cast<object>().ToArray();
            Assert.That(events.Length, Is.EqualTo(138));
            Assert.That(events.Select(e => Runtime.Field(e, "id")).Distinct().Count(), Is.EqualTo(138));
            foreach (object entry in events) AssertBilingualText(entry, "ru", "en", "Text");
            Assert.That(scenes.Length, Is.EqualTo(7));
            int frameCount = 0, lineCount = 0;
            foreach (object scene in scenes)
            {
                AssertBilingualText(scene, "title", "titleEn", "Title");
                object[] frames = ((IEnumerable)Runtime.Field(scene, "frames")).Cast<object>().ToArray();
                Assert.That(frames.Length, Is.EqualTo(3));
                foreach (object frame in frames)
                {
                    frameCount++;
                    AssertBilingualText(frame, "title", "titleEn", "Title");
                    object[] lines = ((IEnumerable)Runtime.Field(frame, "lines")).Cast<object>().ToArray();
                    Assert.That(lines.Length, Is.GreaterThan(0));
                    foreach (object line in lines) { lineCount++; AssertBilingualText(line, "ru", "en", "Text"); }
                }
            }
            Assert.That(frameCount, Is.EqualTo(21));
            Assert.That(lineCount, Is.EqualTo(35));
            var englishNames = new Dictionary<string, string>
            {
                ["tish"] = "Tish", ["luma"] = "Jo", ["bum"] = "Boom", ["koren"] = "Grandpa Koren",
                ["pip"] = "Pip", ["fika"] = "Fika", ["mira"] = "Mira", ["una"] = "Una",
                ["shal"] = "Shal", ["kloch"] = "Kloch", ["ryzh"] = "Rusty", ["bark"] = "Bark"
            };
            Assert.That(speakers.Length, Is.EqualTo(12));
            Assert.That(speakers.Count(s => Runtime.Field(s, "neutralPortrait") is Sprite sprite && sprite != null), Is.EqualTo(12),
                "Reimporting localization must retain every neutral portrait.");
            Assert.That(speakers.Count(s => Runtime.Field(s, "dialogueProfile") is Sprite sprite && sprite != null), Is.EqualTo(6),
                "The six speaking character profiles must survive narrative import.");
            foreach (object speaker in speakers)
            {
                string id = (string)Runtime.Field(speaker, "id");
                Assert.That(Runtime.Call(speaker, "Name", false), Is.EqualTo(englishNames[id]), id);
                Assert.That(Runtime.Call(speaker, "Name", true), Is.EqualTo(Runtime.Field(speaker, "ru")), id);
            }
            foreach (object entry in events.Where(e => !string.IsNullOrWhiteSpace((string)Runtime.Field(e, "speakerLabelRu"))))
                Assert.That(Runtime.Field(entry, "speakerLabelEn"), Is.EqualTo("Jo's advice"), (string)Runtime.Field(entry, "id"));
        }

        private static void AssertBilingualText(object entry, string russianField, string englishField, string method)
        {
            string id = (string)Runtime.Field(entry, "id");
            string ru = (string)Runtime.Field(entry, russianField), en = (string)Runtime.Field(entry, englishField);
            Assert.That(string.IsNullOrWhiteSpace(ru), Is.False, id + " is missing Russian.");
            Assert.That(string.IsNullOrWhiteSpace(en), Is.False, id + " is missing English.");
            Assert.That(Regex.IsMatch(en, "[А-Яа-яЁё]"), Is.False, id + " has Russian in its English translation.");
            Assert.That(Runtime.Call(entry, method, true), Is.EqualTo(ru), id);
            Assert.That(Runtime.Call(entry, method, false), Is.EqualTo(en), id);
        }
    }
}
