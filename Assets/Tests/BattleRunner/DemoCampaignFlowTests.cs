using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DemoCampaignFlowTests
    {
        [Serializable] private sealed class Routes { public Route[] levels; }
        [Serializable] private sealed class Route { public string id; public Replay balance; }
        [Serializable] private sealed class Replay { public ActionStep[] manualReplay; }
        [Serializable] private sealed class ActionStep { public string hero; public int step; public int from; }
        private readonly Dictionary<FieldInfo, object> savedProfileFields = new();
        private string testDirectory;
        private string originalLanguage;
        private bool hadLanguage;
        private UnityEngine.Random.State randomState;
        private readonly List<string> storyOrder = new();
        private int savedCaptureRate;
        private UnityEditor.EditorWindow gameView;
        private object gameViewSizeGroup;
        private int originalSizeIndex;
        private readonly List<int> addedResolutionIndices = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            yield return null;
            savedCaptureRate = Time.captureFramerate;
            if (UnityEditor.SessionState.GetBool("DemoHopCaptureFrames", false)) Time.captureFramerate = 30;
            foreach (UnityEngine.Object client in UnityEngine.Object.FindObjectsByType(
                Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbLocalDevRuntime"), FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(((Component)client).gameObject);
            randomState = UnityEngine.Random.state;
            originalLanguage = PlayerPrefs.GetString("ui.language", "en");
            hadLanguage = PlayerPrefs.HasKey("ui.language");
            PlayerPrefs.SetString("ui.language", "ru");
            Type service = Runtime.Type("Progression.ProfileService");
            foreach (string name in new[] { "_profile", "_store", "_lastSavedJson", "<LoadError>k__BackingField" })
            {
                FieldInfo field = service.GetField(name, Runtime.Members);
                savedProfileFields.Add(field, field.GetValue(null));
            }
            testDirectory = Path.Combine(Path.GetTempPath(), "DiceforgeDemoTests", Guid.NewGuid().ToString("N"));
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
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (gameView != null)
            {
                Runtime.Call(gameView, "SizeSelectionCallback", originalSizeIndex, null);
                foreach (int index in addedResolutionIndices.OrderByDescending(i => i))
                    Runtime.Call(gameViewSizeGroup, "RemoveCustomSize", index);
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
            Time.captureFramerate = savedCaptureRate;
            foreach (UnityEngine.Object client in UnityEngine.Object.FindObjectsByType(
                Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbLocalDevRuntime"), FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(((Component)client).gameObject);
            yield return new ExitPlayMode();
            if (!string.IsNullOrEmpty(testDirectory) && Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }

        private static Component Find(string type) => (Component)UnityEngine.Object.FindFirstObjectByType(
            type == "MapController" ? Type.GetType("MapController, Assembly-CSharp", true) : Runtime.Type(type));
        private static object Profile => Runtime.Type("Progression.ProfileService").GetProperty("Current").GetValue(null);
        private static Type Transition => Type.GetType("Diceforge.Transitions.ScreenTransition, Diceforge.Transitions", true);
        private static IEnumerator Until(Func<bool> ready, string message)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, message);
        }

        private static IEnumerator Move(Component battle, string hero, int step)
        {
            object[] origin = { hero, -1, null, false };
            Runtime.Call(battle, "TryGetHero", origin);
            Component mover = GameObject.Find((string)origin[2]).GetComponent(Runtime.Type("View.BoardLayoutTokenMover"));
            bool jumpOverBark = (int)origin[1] == 3 && step == 2 &&
                (int)Runtime.Call(Runtime.Get(battle, "PresentationState"), "GetStonesAt", Runtime.Player(1), 4) > 0;
            var dice = ((IEnumerable)Runtime.Get(battle, "PresentationDice")).Cast<int>().ToArray();
            Runtime.Call(battle, "SelectPresentationDie", Array.IndexOf(dice, step));
            string frames = Path.Combine(UnityEditor.SessionState.GetString("DemoCampaignCaptureDirectory", "docs/Validation/DemoRCStage7/Campaign"), "HopFrames");
            bool captureHop = UnityEditor.SessionState.GetBool("DemoHopCaptureFrames", false) && !Directory.Exists(frames);
            int frame = 0;
            if (captureHop)
            {
                Directory.CreateDirectory(frames);
                ScreenCapture.CaptureScreenshot(Path.Combine(frames, "frame-" + frame++.ToString("D3") + ".png"));
                yield return null;
            }
            Runtime.Call(battle, "MovePresentationHero", hero);
            Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.EqualTo(true));
            float deadline = Time.realtimeSinceStartup + 15;
            while ((bool)Runtime.Get(battle, "PresentationIsAnimating") && Time.realtimeSinceStartup < deadline)
            {
                if (jumpOverBark) Assert.That(Runtime.Get(mover, "CurrentCellId"), Is.Not.EqualTo(4), "A jump must not land on the occupied intermediate cell.");
                if (captureHop) ScreenCapture.CaptureScreenshot(Path.Combine(frames, "frame-" + frame++.ToString("D3") + ".png"));
                yield return null;
            }
            Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.EqualTo(false));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MapToAllSixLevelsSavesExactlyOnceAndKeepsNamedHeroes()
        {
            var routes = JsonUtility.FromJson<Routes>(File.ReadAllText("docs/DemoRC/v0.1/data/levels.json"));
            for (int level = 1; level <= 6; level++)
            {
                if (SceneManager.GetActiveScene().name != "MainMenu")
                {
                    Runtime.Static("Map.MapFlowRuntime", "RequestReturnToMap");
                    Transition.GetMethod("LoadScene").Invoke(null, new object[] { "MainMenu", null, null });
                }
                yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && Find("Map.MapFlowOrchestrator") != null,
                    "The main menu did not load.");
                yield return Until(() => !(bool)Transition.GetProperty("IsBusy").GetValue(null),
                    "The menu transition did not finish.");
                Component map = Find("Map.MapFlowOrchestrator");
                Runtime.Call(map, "StartChapter", "Chapter1");
                yield return Until(() => !(bool)Runtime.Get(Find("MapController"), "IsHeroTravelling"), "Map travel did not finish.");
                Runtime.Call(map, "OnNodeSelected", "C1_0" + level);
                yield return Until(() => SceneManager.GetActiveScene().name == "Battle" && Find("View.DioramaHud") != null,
                    "Campaign battle did not launch.");
                yield return Until(() => !(bool)Transition.GetProperty("IsBusy").GetValue(null),
                    "The battle transition did not finish.");
                Component battle = Find("View.BattleDebugController");
                yield return DismissOpening(battle, level);
                var hud = (UIDocument)Runtime.Get(Find("View.DioramaHud"), "Document");
                Assert.That(hud.rootVisualElement.Q("scorePanelB").ClassListContains("hidden"), Is.True);
                Assert.That(hud.rootVisualElement.Q<Label>("stepRule").text, Is.EqualTo(level >= 5 ? "Два шага на ход" : "Один шаг на ход"));
                Assert.That(Runtime.Get(battle, "TotalStonesPerPlayer"), Is.EqualTo(level >= 4 ? 3 : level == 3 ? 2 : 1));
                int results = 0;
                Runtime.Observe(battle, "OnMatchEnded", _ => results++);

                if (UnityEditor.SessionState.GetBool("DemoInterfaceCaptureLayouts", false))
                {
                    foreach (string locale in new[] { "en", "ru" })
                    {
                        PlayerPrefs.SetString("ui.language", locale);
                        int settledFrame = Time.frameCount + 4;
                        yield return Until(() => Time.frameCount >= settledFrame, "HUD locale and geometry did not settle.");
                        Assert.That(hud.rootVisualElement.Q<Label>("friendCaption").text, Is.EqualTo(locale == "ru" ? "2 · Друг" : "2 · Friend"));
                        var surface = hud.rootVisualElement.Q("demoActionSurface");
                        foreach (var button in hud.rootVisualElement.Q("demoHeroes").Query<Button>().ToList()
                            .Concat(hud.rootVisualElement.Q("moves").Query<Button>().ToList()))
                        {
                            Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(44f));
                            Assert.That(button.worldBound.xMin, Is.GreaterThanOrEqualTo(surface.worldBound.xMin));
                            Assert.That(button.worldBound.xMax, Is.LessThanOrEqualTo(surface.worldBound.xMax));
                            Assert.That(button.worldBound.yMax, Is.LessThanOrEqualTo(surface.worldBound.yMax));
                        }
                        yield return Capture("level-0" + level + "-" + locale + "-hud");
                    }
                }
                if (level >= 4)
                {
                    yield return Capture("level-0" + level + "-start");
                    Component hazard = Find("View.DemoTrailHazardView");
                    if (level == 5)
                    {
                        yield return Move(battle, "tish", 2);
                        Assert.That(Runtime.Get(hazard, "PresentedCell"), Is.EqualTo(3));
                        Assert.That(hud.rootVisualElement.Q<Label>("actionsRemaining").text, Is.EqualTo("Осталось действий: 1"));
                        yield return Capture("level-05-one-action-left");
                        Runtime.Call(battle, "SelectPresentationDie", 0);
                        Runtime.Call(battle, "MovePresentationHero", "luma");
                        yield return Until(() => (int)Runtime.Get(Runtime.Get(battle, "PresentationState"), "TrailHazardCell") == 4,
                            "Ryzh did not advance after the second action.");
                        Assert.That(Runtime.Get(hazard, "PresentedCell"), Is.EqualTo(3), "Ryzh's visual must wait for the friend.");
                        Runtime.Call(battle, "RestartMatch");
                        yield return new WaitForSeconds(.5f);
                        Assert.That(Runtime.Get(hazard, "PresentedCell"), Is.EqualTo(3));
                        Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.EqualTo(false));
                    }
                    foreach (ActionStep action in routes.levels.Single(x => x.id == "L" + level).balance.manualReplay)
                    {
                        if (level == 4 && action.hero == "tish" && action.from == 2)
                        {
                            Runtime.Call(battle, "SelectPresentationDie", 1);
                            Runtime.Call(battle, "MovePresentationHero", "tish");
                            Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.EqualTo(false));
                            CollectionAssert.AreEqual(new[] { 1, 2 }, (IEnumerable)Runtime.Get(battle, "PresentationDice"));
                            object board = Find("View.DioramaBoard");
                            Runtime.Set(Find("View.BoardDebugView"), "_cellSelectionEnabled", false);
                            int blockedPreviews = 0;
                            Runtime.Observe(battle, "OnDemoBlockedPreview", _ => blockedPreviews++);
                            Runtime.Call(battle, "PreviewPresentationCell", board, 1, null);
                            for (int preview = 0; preview < 5; preview++)
                                Runtime.Call(battle, "PreviewPresentationCell", board, 2, "tish");
                            Assert.That(blockedPreviews, Is.EqualTo(1), "A persistent blocked preview must notify learning once per selection.");
                            yield return Capture("level-04-blocked-landing");
                            Component previewView = Find("View.DioramaMovePreview");
                            Assert.That(Runtime.Get(previewView, "IsVisible"), Is.True);
                            Assert.That(Runtime.Get(previewView, "IsBlocked"), Is.True);
                            Runtime.Call(board, "Preview", null, null, -1);
                            Runtime.Call(battle, "RefreshPresentation");
                        }
                        yield return Move(battle, action.hero, action.step);
                    }
                }
                else if (level == 3)
                {
                    Runtime.Call(battle, "SelectPresentationDie", 1);
                    Runtime.Call(battle, "MovePresentationHero", "luma");
                    Runtime.Call(battle, "RestartMatch");
                    yield return new WaitForSeconds(.4f);
                    Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.EqualTo(false));
                    object[] reset = { "luma", -1, null, false };
                    Runtime.Call(battle, "TryGetHero", reset);
                    Assert.That(reset[1], Is.EqualTo(0), "Restart must cancel the queued selection.");
                    yield return Move(battle, "luma", 2);
                    object[] tish = { "tish", -1, null, false };
                    Runtime.Call(battle, "TryGetHero", tish);
                    Assert.That(tish[1], Is.EqualTo(0), "Choosing Luma must leave Tish at the shared start.");
                    for (int action = 0; action < 3; action++) yield return Move(battle, "luma", 2);
                    Assert.That(Runtime.Get(battle, "IsMatchEnded"), Is.EqualTo(false));
                    Assert.That(Runtime.Get(battle, "LocalPlayerBorneOffCount"), Is.EqualTo(1));
                    for (int action = 0; action < 4; action++) yield return Move(battle, "tish", 2);
                }
                else
                    for (int action = 0; action < (level == 1 ? 8 : 4); action++)
                        yield return Move(battle, "tish", level == 1 ? 1 : 2);

                yield return FinishStoryAndResult(battle, () => results == 1, level);
                Assert.That(Runtime.Get(battle, "HasPendingResultSave"), Is.EqualTo(false));
                object session = Runtime.Get(battle, "RewardSession");
                Assert.That(Runtime.Get(session, "Outcome"), Is.Not.Null);
                int receipts = ((IList)Runtime.Field(Profile, "progressionReceipts")).Count;
                Assert.That(receipts, Is.EqualTo(level));
                Runtime.Call(session, "Retry");
                Assert.That(((IList)Runtime.Field(Profile, "progressionReceipts")).Count, Is.EqualTo(receipts));
                Runtime.Static("Progression.ProfileService", "Load");
                object chapter = ((IList)Runtime.Field(Profile, "chapters"))[0];
                object progress = Runtime.Field(chapter, "state");
                Assert.That(Runtime.Call(progress, "IsCompleted", "C1_0" + level), Is.EqualTo(true));
                if (level < 6) Assert.That(Runtime.Call(progress, "IsUnlocked", "C1_0" + (level + 1)), Is.EqualTo(true));
            }
            CollectionAssert.AreEqual(new[] { "S00", "S01", "S02", "S03", "S04", "S05", "S06" }, storyOrder);
            Assert.That(((IList)Runtime.Field(Profile, "demoLearning")).Count, Is.EqualTo(6));
            Assert.That(((IList)Runtime.Field(Profile, "demoStorySeen")).Count, Is.EqualTo(7));
            yield return VerifyWorldSelectionAndReplay();
        }

        private IEnumerator VerifyWorldSelectionAndReplay()
        {
            Component result = Find("View.ResultOverlayView");
            var document = (UIDocument)Runtime.Field(result, "document");
            var worlds = document.rootVisualElement.Q("worldSelectionRoot");
            Assert.That(worlds, Is.Not.Null, "L6 victory must reveal the world selector.");
            Assert.That(worlds.ClassListContains("world-hidden"), Is.False);
            Assert.That(worlds.Q<Button>("worldIslandAction1"), Is.Not.Null);
            Assert.That(worlds.Q<Button>("worldIslandAction2"), Is.Not.Null);
            gameView = UnityEditor.EditorWindow.GetWindow(EditorType("UnityEditor.GameView"));
            originalSizeIndex = (int)Runtime.Get(gameView, "selectedSizeIndex");
            Type sizesType = EditorType("UnityEditor.GameViewSizes");
            object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            gameViewSizeGroup = Runtime.Get(sizes, "currentGroup");
            string directory = Path.GetFullPath(UnityEditor.SessionState.GetString("DemoWorldCaptureDirectory", "docs/Validation/DemoRCStage10Worlds"));
            Directory.CreateDirectory(directory);
            foreach (Vector2Int resolution in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280) })
            {
                Runtime.Call(gameView, "SizeSelectionCallback", FindOrAddResolution(resolution.x, resolution.y), null);
                gameView.Focus(); gameView.Repaint();
                yield return Until(() => Screen.width == resolution.x && Screen.height == resolution.y, "World selector did not resize.");
                foreach (string locale in new[] { "en", "ru" })
                {
                    PlayerPrefs.SetString("ui.language", locale);
                    yield return Until(() => worlds.Q<Label>("mushroomSoon").text == (locale == "ru" ? "Скоро" : "Coming soon"), "World captions did not localize.");
                    int targetFrame = Time.frameCount + 5;
                    yield return Until(() => Time.frameCount >= targetFrame, "World layout did not settle.");
                    Assert.That(worlds.Q<Label>("frostySoon").text, Is.EqualTo(locale == "ru" ? "Скоро" : "Coming soon"));
                    foreach (string name in new[] { "worldHome", "worldReplay", "worldWebsite", "worldReview", "worldIslandAction0", "worldIslandAction1", "worldIslandAction2" })
                    {
                        var button = worlds.Q<Button>(name);
                        Assert.That(button.worldBound.height, Is.GreaterThanOrEqualTo(44));
                        Assert.That(button.worldBound.xMin, Is.GreaterThanOrEqualTo(worlds.worldBound.xMin));
                        Assert.That(button.worldBound.xMax, Is.LessThanOrEqualTo(worlds.worldBound.xMax));
                        var picked = button.panel.Pick(button.worldBound.center);
                        Assert.That(picked == button || button.Contains(picked), Is.True, name + " must be reachable without an overlapping panel.");
                    }
                    string path = Path.Combine(directory, "worlds-" + locale + "-" + resolution.x + ".png");
                    ScreenCapture.CaptureScreenshot(path);
                    int capturedFrame = Time.frameCount + 2;
                    yield return Until(() => Time.frameCount >= capturedFrame && File.Exists(path), "World screenshot was not written.");
                    byte[] image = File.ReadAllBytes(path);
                    Assert.That(PngDimension(image, 16), Is.EqualTo(resolution.x));
                    Assert.That(PngDimension(image, 20), Is.EqualTo(resolution.y));
                    if (locale == "ru" && resolution.x == 1280)
                    {
                        var view = Runtime.Field(result, "worlds");
                        var atmosphere = Runtime.Field(view, "atmosphere");
                        int framesBefore = (int)Runtime.Get(atmosphere, "AnimationFrames");
                        var positionBefore = worlds.Q<Image>("worldIsland0").style.translate.value;
                        yield return new WaitForSecondsRealtime(.4f);
                        Assert.That((int)Runtime.Get(atmosphere, "AnimationFrames"), Is.GreaterThan(framesBefore));
                        Assert.That(worlds.Q<Image>("worldIsland0").style.translate.value, Is.Not.EqualTo(positionBefore), "Visible islands must move.");
                        for (int world = 0; world < 3; world++) Assert.That(worlds.Q("worldWeather" + world), Is.Not.Null);
                        if (UnityEditor.SessionState.GetBool("DemoWorldMotionCapture", false))
                        {
                            var motionDirectory = Path.Combine(directory, "Motion");
                            Directory.CreateDirectory(motionDirectory);
                            for (int frame = 0; frame < 24; frame++)
                            {
                                ScreenCapture.CaptureScreenshot(Path.Combine(motionDirectory, "frame-" + frame.ToString("000") + ".png"));
                                yield return new WaitForSecondsRealtime(.1f);
                            }
                        }
                    }
                    if (resolution.y > resolution.x)
                    {
                        var scroll = worlds.Q<ScrollView>("worldScroll");
                        scroll.ScrollTo(worlds.Q("frostyName"));
                        int scrolledFrame = Time.frameCount + 3;
                        yield return Until(() => Time.frameCount >= scrolledFrame, "World scroll did not settle.");
                        Assert.That(worlds.Q("frostyName").worldBound.yMax, Is.LessThanOrEqualTo(scroll.worldBound.yMax + 2), "The last island must be reachable in portrait.");
                        ScreenCapture.CaptureScreenshot(Path.Combine(directory, "worlds-" + locale + "-portrait-scrolled.png"));
                        int captureScrollFrame = Time.frameCount + 2;
                        yield return Until(() => Time.frameCount >= captureScrollFrame, "Scrolled screenshot did not finish.");
                        scroll.scrollOffset = Vector2.zero;
                    }
                }
            }
            object worldView = Runtime.Field(result, "worlds");
            string openedUrl = null;
            Runtime.Set(worldView, "OpenExternalUrl", new Action<string>(url => openedUrl = url));
            Click(worlds.Q<Button>("worldWebsite"));
            Assert.That(openedUrl, Is.EqualTo("https://glimblehop.com/"));
            string beforeReview = JsonUtility.ToJson(Profile);
            Click(worlds.Q<Button>("worldReview"));
            yield return null;
            Assert.That(worlds.Q("FeedbackModal").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(worlds.Q<Button>("rating5"), Is.Not.Null);
            int reviewFrame = Time.frameCount + 3;
            yield return Until(() => Time.frameCount >= reviewFrame, "Review layout did not settle.");
            var reviewModal = worlds.Q("FeedbackModal");
            var reviewViewport = reviewModal.Q<ScrollView>().contentViewport.worldBound;
            foreach (var star in reviewModal.Query<Button>(className: "pf-star-button").ToList())
            {
                Assert.That(star.worldBound.xMin, Is.GreaterThanOrEqualTo(reviewViewport.xMin));
                Assert.That(star.worldBound.xMax, Is.LessThanOrEqualTo(reviewViewport.xMax));
            }
            foreach (string name in new[] { "btnFeedbackSubmit", "btnFeedbackCancel" })
            {
                var action = reviewModal.Q<Button>(name);
                var picked = action.panel.Pick(action.worldBound.center);
                Assert.That(picked == action || action.Contains(picked), Is.True, name + " must stay visible without scrolling.");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "review-ru-portrait.png"));
            reviewFrame = Time.frameCount + 2;
            yield return Until(() => Time.frameCount >= reviewFrame, "Review screenshot did not finish.");
            Click(worlds.Q<Button>("btnFeedbackCancel"));
            Assert.That(JsonUtility.ToJson(Profile), Is.EqualTo(beforeReview));
            foreach (string island in new[] { "worldIslandAction1", "worldIslandAction2" })
            {
                Click(worlds.Q<Button>(island));
                yield return null;
                Assert.That(worlds.Q("FeedbackModal").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Click(worlds.Q<Button>("btnFeedbackCancel"));
                Assert.That(JsonUtility.ToJson(Profile), Is.EqualTo(beforeReview), "Future islands must not reset progress.");
            }
            var worldAtmosphere = Runtime.Field(worldView, "atmosphere");
            Runtime.Call(worldView, "Hide");
            int hiddenFrames = (int)Runtime.Get(worldAtmosphere, "AnimationFrames");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That((int)Runtime.Get(worldAtmosphere, "AnimationFrames"), Is.EqualTo(hiddenFrames), "Hidden world screens must stop animating.");
            Runtime.Call(worldView, "Show");
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That((int)Runtime.Get(worldAtmosphere, "AnimationFrames"), Is.GreaterThan(hiddenFrames));
            Click(worlds.Q<Button>("worldHome"));
            yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu", "Home did not leave the world selector.");
            yield return Until(() => !(bool)Transition.GetProperty("IsBusy").GetValue(null), "Home transition did not finish.");
            Runtime.Static("Progression.ProfileService", "Load");
            var menu = (Component)UnityEngine.Object.FindAnyObjectByType(Type.GetType("MainMenuController, Assembly-CSharp", true));
            Runtime.Call(menu, "OpenMapChapterImmediately");
            worlds = ((VisualElement)Runtime.Field(menu, "root")).Q("worldSelectionRoot");
            Assert.That(worlds.ClassListContains("world-hidden"), Is.False, "Saved completion must reopen world selection.");
            int receipts = ((IList)Runtime.Field(Profile, "progressionReceipts")).Count;
            string profileBeforeFailure = JsonUtility.ToJson(Profile);
            string currencies = Amounts("currencies");
            string inventory = Amounts("inventory");
            string hero = JsonUtility.ToJson(Runtime.Field(Profile, "hero"));
            object store = Runtime.Type("Progression.ProfileService").GetField("_store", Runtime.Members).GetValue(null);
            Runtime.Set(store, "Checkpoint", new Action<string>(stage => { if (stage == "BeforeReplace") throw new IOException("Injected replay save failure."); }));
            Click(worlds.Q<Button>("worldReplay"));
            Assert.That(worlds.Q("worldError").ClassListContains("world-hidden"), Is.False);
            Assert.That(JsonUtility.ToJson(Profile), Is.EqualTo(profileBeforeFailure), "Failed replay must preserve the completed profile.");
            Runtime.Set(store, "Checkpoint", null);
            Click(worlds.Q<Button>("worldIslandAction0"));
            Assert.That(worlds.ClassListContains("world-hidden"), Is.True);
            Runtime.Static("Progression.ProfileService", "Load");
            object state = Runtime.Field(((IList)Runtime.Field(Profile, "chapters"))[0], "state");
            Assert.That(Runtime.Field(state, "currentNodeId"), Is.EqualTo("C1_01"));
            Assert.That(Runtime.Call(state, "IsCompleted", "C1_06"), Is.EqualTo(false));
            Assert.That(((IList)Runtime.Field(Profile, "progressionReceipts")).Count, Is.EqualTo(receipts));
            Assert.That(((IList)Runtime.Field(Profile, "demoStorySeen")).Count, Is.Zero);
            Assert.That(((IList)Runtime.Field(Profile, "demoLearning")).Count, Is.Zero);
            Assert.That(Amounts("currencies"), Is.EqualTo(currencies));
            Assert.That(Amounts("inventory"), Is.EqualTo(inventory));
            Assert.That(JsonUtility.ToJson(Runtime.Field(Profile, "hero")), Is.EqualTo(hero));
            Component map = Find("Map.MapFlowOrchestrator");
            Runtime.Call(map, "OnNodeSelected", "C1_01");
            yield return Until(() => SceneManager.GetActiveScene().name == "Battle" && Find("View.DioramaHud") != null, "Replay did not launch the first level.");
            yield return Until(() => !(bool)Transition.GetProperty("IsBusy").GetValue(null), "Replay transition did not finish.");
            yield return Until(() => (bool)Runtime.Get(Runtime.Get(Find("View.BattleDebugController"), "DemoNarrative"), "IsStoryVisible"), "Replay did not restore the opening story.");
        }

        private static string Amounts(string field) => string.Join("|", ((IEnumerable)Runtime.Field(Profile, field)).Cast<object>().Select(item => JsonUtility.ToJson(item)));
        private static int PngDimension(byte[] bytes, int offset) =>
            bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];

        private static void Click(Button button)
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            evt.target = button;
            button.SendEvent(evt);
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

        private IEnumerator DismissOpening(Component battle, int level)
        {
            object narrative = Runtime.Get(battle, "DemoNarrative");
            yield return Until(() => (bool)Runtime.Get(narrative, "IsStoryVisible"), "Opening dialogue never appeared.");
            Assert.That(Runtime.Get(battle, "PresentationCanInteract"), Is.False);
            Runtime.Call(battle, "MovePresentationHero", "tish"); Runtime.Call(battle, "SelectPresentationDie", 0);
            Assert.That(Runtime.Get(battle, "LocalPlayerBorneOffCount"), Is.EqualTo(0));
            Assert.That(Runtime.Get(narrative, "EvidenceCount"), Is.EqualTo(0));
            float deadline = Time.realtimeSinceStartup + 20;
            while ((bool)Runtime.Get(narrative, "IsModal") && Time.realtimeSinceStartup < deadline)
            {
                string scene = (string)Runtime.Get(narrative, "StoryId");
                if (scene != null)
                {
                    storyOrder.Add(scene);
                    if (level == 1)
                    {
                        yield return CaptureNarrative("S00-frame-1"); Runtime.Call(narrative, "AdvanceStory");
                        Assert.That(Runtime.Get(narrative, "StoryFrame"), Is.EqualTo(0));
                        Assert.That(Runtime.Field(narrative, "line"), Is.EqualTo(1), "The second speaker stays in the same frame.");
                        while ((int)Runtime.Get(narrative, "StoryFrame") < 2) Runtime.Call(narrative, "AdvanceStory");
                        yield return CaptureNarrative("S00-frame-3");
                    }
                }
                Runtime.Call(narrative, "SkipStory"); yield return null; yield return null;
            }
            Assert.That(Runtime.Get(narrative, "IsModal"), Is.False);
            Assert.That(Runtime.Get(narrative, "EvidenceCount"), Is.EqualTo(0), "Skipping must not count as learning.");
            if (level == 6) Assert.That(Runtime.Get(narrative, "PendingHintId"), Is.EqualTo("L06_T01"), "Hiding guidance must keep the level objective.");
            if (level == 1)
            {
                yield return CaptureNarrative("L1-luma-note"); Runtime.Call(narrative, "SkipHint", true);
                Assert.That(Runtime.Get(narrative, "EvidenceCount"), Is.EqualTo(0));
                Assert.That(((IList)Runtime.Field(Profile, "progressionReceipts")).Count, Is.EqualTo(0));
                Assert.That(Runtime.Field(Profile, "demoGuidanceHidden"), Is.EqualTo(true));
                Runtime.Call(narrative, "OpenHelp"); yield return CaptureNarrative("L1-help");
                Runtime.Call(narrative, "CloseHelp");
            }
        }

        private IEnumerator FinishStoryAndResult(Component battle, Func<bool> ready, int level)
        {
            var narrative = Runtime.Get(battle, "DemoNarrative"); float deadline = Time.realtimeSinceStartup + 45;
            bool teaserSeen = false;
            while (!ready() && Time.realtimeSinceStartup < deadline)
            {
                if ((bool)Runtime.Get(narrative, "IsStoryVisible"))
                {
                    Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.False, "Story must wait for the last movement.");
                    if ((bool)Runtime.Get(narrative, "IsWorldTeaserVisible"))
                    {
                        teaserSeen = true;
                        Assert.That(ready(), Is.False, "World selection must wait for the cliffhanger.");
                        Assert.That(level, Is.EqualTo(6));
                        var next = (Button)Runtime.Field(narrative, "storyNext");
                        PlayerPrefs.SetString("ui.language", "en");
                        yield return new WaitForSecondsRealtime(.25f);
                        yield return CaptureNarrative("cliffhanger-en");
                        Click(next);
                        Assert.That(Runtime.Field(narrative, "line"), Is.EqualTo(1));
                        PlayerPrefs.SetString("ui.language", "ru");
                        yield return new WaitForSecondsRealtime(.25f);
                        Assert.That(Runtime.Field(narrative, "line"), Is.EqualTo(1));
                        Click(next);
                        Assert.That(next.text, Is.EqualTo("К новым мирам"));
                        yield return CaptureNarrative("cliffhanger-ru");
                        Assert.That(ready(), Is.False);
                        Click(next);
                        yield return null;
                        continue;
                    }
                    string scene = (string)Runtime.Get(narrative, "StoryId");
                    if (scene != null)
                    {
                        storyOrder.Add(scene);
                        Assert.That(Runtime.Get(battle, "LocalPlayerBorneOffCount"), Is.EqualTo(level >= 4 ? 3 : level == 3 ? 2 : 1));
                        yield return CaptureNarrative(scene + "-frame-1");
                        if (level == 6)
                        {
                            while ((int)Runtime.Get(narrative, "StoryFrame") < 1) Runtime.Call(narrative, "AdvanceStory"); yield return CaptureNarrative("S06-wedding");
                            while ((int)Runtime.Get(narrative, "StoryFrame") < 2) Runtime.Call(narrative, "AdvanceStory"); yield return CaptureNarrative("S06-last-frame");
                        }
                    }
                    Runtime.Call(narrative, "SkipStory");
                }
                yield return null;
            }
            if (level == 6) Assert.That(teaserSeen, Is.True, "The final level must show the cliffhanger.");
            Assert.That(ready(), Is.True, "Result must finalize exactly once after closing the story.");
            Assert.That((int)Runtime.Get(narrative, "EvidenceCount"), Is.GreaterThan(0));
        }

        private static IEnumerator CaptureNarrative(string name)
        {
            string directory = Path.GetFullPath(UnityEditor.SessionState.GetString("DemoCampaignCaptureDirectory", "docs/Validation/DemoRCStage7/Campaign"));
            Directory.CreateDirectory(directory); yield return null; yield return null;
            Component narrative = Find("UI.Dialogue.DemoNarrativeController");
            if ((bool)Runtime.Get(narrative, "IsStoryVisible"))
            {
                var doc = (UIDocument)Runtime.Field(narrative, "document");
                var bounds = doc.rootVisualElement.Q("demoStory").worldBound;
                Assert.That(bounds.height, Is.GreaterThan(300));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(doc.rootVisualElement.panel.visualTree.worldBound.yMax + 1), "Story must fit inside the visible panel.");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png")); yield return null; yield return null;
        }

        private static IEnumerator Capture(string name)
        {
            string directory = Path.GetFullPath(UnityEditor.SessionState.GetString("DemoCampaignCaptureDirectory", "docs/Validation/DemoRCStage7/Campaign"));
            Directory.CreateDirectory(directory);
            yield return null;
            yield return null;
            var document = (UIDocument)Runtime.Get(Find("View.DioramaHud"), "Document");
            Rect surface = document.rootVisualElement.Q("demoActionSurface").worldBound;
            Rect meta = document.rootVisualElement.Q(className: "demo-meta").worldBound;
            foreach (Button button in document.rootVisualElement.Query<Button>(className: "demo-hero").ToList())
            {
                Rect bounds = button.worldBound;
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(meta.yMax - .5f), "Hero controls overlap the action metadata.");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(surface.yMax + .5f), "Hero controls must stay inside their action surface.");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}
