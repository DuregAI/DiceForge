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

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            yield return null;
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
            foreach (var pair in savedProfileFields) pair.Key.SetValue(null, pair.Value);
            savedProfileFields.Clear();
            Runtime.Static("Progression.ProfileService", "RebuildCache");
            Runtime.Static("Map.MapFlowRuntime", "ClearRunContext");
            if (hadLanguage) PlayerPrefs.SetString("ui.language", originalLanguage);
            else PlayerPrefs.DeleteKey("ui.language");
            UnityEngine.Random.state = randomState;
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
            Runtime.Call(battle, "MovePresentationHero", hero);
            Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.EqualTo(true));
            float deadline = Time.realtimeSinceStartup + 15;
            while ((bool)Runtime.Get(battle, "PresentationIsAnimating") && Time.realtimeSinceStartup < deadline)
            {
                if (jumpOverBark) Assert.That(Runtime.Get(mover, "CurrentCellId"), Is.Not.EqualTo(4), "A jump must not land on the occupied intermediate cell.");
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
                            Runtime.Call(board, "PreviewBlocked", 2, 2, Runtime.Get(battle, "PresentationState"));
                            yield return Capture("level-04-blocked-landing");
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
        }

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
                        Assert.That(Runtime.Get(narrative, "StoryFrame"), Is.EqualTo(1));
                        Runtime.Call(narrative, "AdvanceStory"); yield return CaptureNarrative("S00-frame-3");
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
                var doc = (UIDocument)Runtime.Field(narrative, "document");
                doc.rootVisualElement.Q("demoHelp").style.display = DisplayStyle.None;
            }
        }

        private IEnumerator FinishStoryAndResult(Component battle, Func<bool> ready, int level)
        {
            var narrative = Runtime.Get(battle, "DemoNarrative"); float deadline = Time.realtimeSinceStartup + 25;
            while (!ready() && Time.realtimeSinceStartup < deadline)
            {
                if ((bool)Runtime.Get(narrative, "IsStoryVisible"))
                {
                    Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.False, "Story must wait for the last movement.");
                    string scene = (string)Runtime.Get(narrative, "StoryId");
                    if (scene != null)
                    {
                        storyOrder.Add(scene);
                        Assert.That(Runtime.Get(battle, "LocalPlayerBorneOffCount"), Is.EqualTo(level >= 4 ? 3 : level == 3 ? 2 : 1));
                        yield return CaptureNarrative(scene + "-frame-1");
                        if (level == 6)
                        {
                            Runtime.Call(narrative, "AdvanceStory"); yield return CaptureNarrative("S06-wedding");
                            Runtime.Call(narrative, "AdvanceStory"); yield return CaptureNarrative("S06-last-frame");
                        }
                    }
                    Runtime.Call(narrative, "SkipStory");
                }
                yield return null;
            }
            Assert.That(ready(), Is.True, "Result must finalize exactly once after closing the story.");
            Assert.That((int)Runtime.Get(narrative, "EvidenceCount"), Is.GreaterThan(0));
        }

        private static IEnumerator CaptureNarrative(string name)
        {
            string directory = Path.GetFullPath("docs/Validation/DemoRCStage3");
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
            string directory = Path.GetFullPath("docs/Validation/DemoRCStage3");
            Directory.CreateDirectory(directory);
            yield return null;
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
            yield return null;
            yield return null;
        }
    }
}
