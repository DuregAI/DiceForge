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

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DemoCheckpointFlowTests
    {
        private readonly Dictionary<FieldInfo, object> savedProfileFields = new();
        private string testDirectory, originalLanguage;
        private bool hadLanguage;
        private object testStore;
        private UnityEngine.Random.State randomState;

        private static object Profile => Runtime.Type("Progression.ProfileService").GetProperty("Current").GetValue(null);
        private static object Saved => Runtime.Field(Profile, "demoCheckpoint");
        private static object Board(Component battle) => Runtime.Get(battle, "PresentationState");
        private static object Checkpoint(Component battle) => Runtime.Get(battle, "DemoCheckpoint");
        private static object Narrative(Component battle) => Runtime.Get(battle, "DemoNarrative");
        private static int Receipts => ((IList)Runtime.Field(Profile, "progressionReceipts")).Count;
        private static string Operation(Component battle) => (string)Runtime.Get(Runtime.Get(battle, "RewardSession"), "OperationId");
        private static Type Transition => Type.GetType("Diceforge.Transitions.ScreenTransition, Diceforge.Transitions", true);
        private static Component Find(string name) => (Component)UnityEngine.Object.FindFirstObjectByType(
            name == "MapController" ? Type.GetType("MapController, Assembly-CSharp", true) : Runtime.Type(name));

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            yield return null;
            // Remote admin epochs must not mutate the deliberately isolated local campaign fixture.
            foreach (UnityEngine.Object client in UnityEngine.Object.FindObjectsByType(
                Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbLocalDevRuntime"), FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(((Component)client).gameObject);
            randomState = UnityEngine.Random.state;
            hadLanguage = PlayerPrefs.HasKey("ui.language");
            originalLanguage = PlayerPrefs.GetString("ui.language", "en");
            PlayerPrefs.SetString("ui.language", "ru");
            Type service = Runtime.Type("Progression.ProfileService");
            object original = Profile;
            foreach (string name in new[] { "_profile", "_store", "_lastSavedJson", "<LoadError>k__BackingField" })
            {
                FieldInfo field = service.GetField(name, Runtime.Members);
                savedProfileFields.Add(field, field.GetValue(null));
            }
            testDirectory = Path.Combine(Path.GetTempPath(), "DiceforgeDemoCheckpointTests", Guid.NewGuid().ToString("N"));
            testStore = Activator.CreateInstance(Runtime.Type("Progression.AtomicProfileStore"), Runtime.Members, null,
                new object[] { Path.Combine(testDirectory, "player_profile.json") }, null);
            object profile = Runtime.New("Progression.PlayerProfile");
            // Retain the signed-in identity so the SDK cannot mistake this fixture for a different player.
            Runtime.Set(profile, "playerGuid", Runtime.Field(original, "playerGuid"));
            Runtime.Set(profile, "selectedAvatarId", Runtime.Static("Progression.AvatarService", "GetDefaultAvatarId"));
            object chapter = Runtime.New("Progression.ChapterProgress");
            Runtime.Set(chapter, "chapterId", "Chapter1");
            Runtime.Set(chapter, "runId", Guid.NewGuid().ToString("N"));
            ((IList)Runtime.Field(profile, "chapters")).Add(chapter);
            service.GetField("_profile", Runtime.Members).SetValue(null, profile);
            service.GetField("_store", Runtime.Members).SetValue(null, testStore);
            service.GetField("_lastSavedJson", Runtime.Members).SetValue(null, null);
            service.GetField("<LoadError>k__BackingField", Runtime.Members).SetValue(null, null);
            Runtime.Static("Progression.ProfileService", "RebuildCache");
            Runtime.Static("Progression.ProfileService", "Save");
            Runtime.Static("Map.MapFlowRuntime", "ClearRunContext");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (testStore != null) Runtime.Set(testStore, "Checkpoint", null);
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
            if (!string.IsNullOrEmpty(testDirectory) && Directory.Exists(testDirectory))
            {
                string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DiceforgeDemoCheckpointTests")) + Path.DirectorySeparatorChar;
                Assert.That(Path.GetFullPath(testDirectory).StartsWith(root, StringComparison.OrdinalIgnoreCase), Is.True);
                Directory.Delete(testDirectory, true);
            }
            testStore = null;
        }

        private static IEnumerator Until(Func<bool> ready, string message)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, message);
        }

        private static void SeedLevel(int level)
        {
            object chapter = ((IList)Runtime.Field(Profile, "chapters"))[0];
            object progress = Runtime.Field(chapter, "state");
            Runtime.Set(progress, "currentNodeId", "C1_0" + level);
            for (int i = 1; i <= level; i++)
            {
                Runtime.Call(progress, "Unlock", "C1_0" + i);
                if (i < level) Runtime.Call(progress, "MarkCompleted", "C1_0" + i);
            }
            Runtime.Static("Progression.ProfileService", "Save");
        }

        private static IEnumerator Menu()
        {
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                Runtime.Static("Map.MapFlowRuntime", "RequestReturnToMap");
                Transition.GetMethod("LoadScene").Invoke(null, new object[] { "MainMenu", null, null });
            }
            yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && Find("Map.MapFlowOrchestrator") != null,
                "Main menu did not load.");
            yield return Until(() => !(bool)Transition.GetProperty("IsBusy").GetValue(null), "Menu transition did not finish.");
            Runtime.Static("Map.MapFlowRuntime", "ClearRunContext");
            Runtime.Static("Progression.ProfileService", "Load");
        }

        private static IEnumerator Launch(int level)
        {
            yield return Menu();
            Component map = Find("Map.MapFlowOrchestrator");
            Runtime.Call(map, "StartChapter", "Chapter1");
            yield return Until(() => !(bool)Runtime.Get(Find("MapController"), "IsHeroTravelling"), "Map travel did not finish.");
            Runtime.Call(map, "OnNodeSelected", "C1_0" + level);
            yield return Until(() => SceneManager.GetActiveScene().name == "Battle" && Find("View.DioramaHud") != null &&
                Find("View.BattleDebugController") != null && Checkpoint(Find("View.BattleDebugController")) != null,
                "Campaign checkpoint battle did not launch.");
            yield return Until(() => !(bool)Transition.GetProperty("IsBusy").GetValue(null), "Battle transition did not finish.");
        }

        private static IEnumerator Opening(Component battle)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            object narrative = Narrative(battle);
            while (!(bool)Runtime.Get(narrative, "Ready") && Time.realtimeSinceStartup < deadline)
            {
                if ((bool)Runtime.Get(narrative, "IsStoryVisible")) Runtime.Call(narrative, "SkipStory");
                yield return null;
            }
            Assert.That(Runtime.Get(narrative, "Ready"), Is.EqualTo(true), "Narrative did not become ready.");
            Assert.That(Runtime.Get(battle, "PresentationCanInteract"), Is.EqualTo(true));
            yield return null;
            yield return Until(() => Saved != null, "Initial checkpoint was not saved.");
        }

        private static void SelectMove(Component battle, string hero, int step)
        {
            int[] dice = ((IEnumerable)Runtime.Get(battle, "PresentationDice")).Cast<int>().ToArray();
            Assert.That(Array.IndexOf(dice, step), Is.GreaterThanOrEqualTo(0));
            Runtime.Call(battle, "SelectPresentationDie", Array.IndexOf(dice, step));
            Runtime.Call(battle, "MovePresentationHero", hero);
            Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.EqualTo(true));
        }

        private static IEnumerator Move(Component battle, string hero, int step)
        {
            SelectMove(battle, hero, step);
            yield return Until(() => !(bool)Runtime.Get(battle, "PresentationIsAnimating"), "Move presentation did not settle.");
            yield return null;
            yield return null;
        }

        private static void Hero(Component battle, string id, int cell, bool exited = false)
        {
            object[] args = { id, -1, null, false };
            Assert.That(Runtime.Call(battle, "TryGetHero", args), Is.EqualTo(true));
            Assert.That(args[1], Is.EqualTo(cell), id + " changed cell on resume.");
            Assert.That(args[3], Is.EqualTo(exited), id + " changed exit state on resume.");
        }

        [UnityTest]
        public IEnumerator MidTurnL5ResumeKeepsNamesAndOneHazardAdvanceThenRestartAndRejectsChangedScenario()
        {
            SeedLevel(5);
            yield return Launch(5);
            Component battle = Find("View.BattleDebugController");
            yield return Opening(battle);
            yield return Move(battle, "luma", 2);
            Hero(battle, "luma", 2); Hero(battle, "tish", 0); Hero(battle, "bum", 0);
            string operation = Operation(battle);
            string attempt = (string)Runtime.Field(Runtime.Call(Narrative(battle), "ExportLearning"), "attemptId");
            CollectionAssert.AreEqual(new[] { 1 }, (IEnumerable)Runtime.Get(battle, "PresentationDice"));
            Assert.That(Runtime.Get(Board(battle), "TrailHazardCell"), Is.EqualTo(3));
            yield return Launch(5);
            battle = Find("View.BattleDebugController");
            yield return Opening(battle);
            Assert.That(Runtime.Get(Checkpoint(battle), "Resumed"), Is.EqualTo(true));
            Assert.That(Operation(battle), Is.EqualTo(operation));
            Assert.That(Runtime.Field(Runtime.Call(Narrative(battle), "ExportLearning"), "attemptId"), Is.EqualTo(attempt));
            Hero(battle, "luma", 2); Hero(battle, "tish", 0); Hero(battle, "bum", 0);
            CollectionAssert.AreEqual(new[] { 1 }, (IEnumerable)Runtime.Get(battle, "PresentationDice"));
            Assert.That(Runtime.Get(Board(battle), "TrailHazardCell"), Is.EqualTo(3));
            int hazards = 0;
            Runtime.Observe(Runtime.Field(battle, "_runner"), "OnTrailHazardMoved", _ => hazards++);
            yield return Move(battle, "tish", 1);
            Assert.That(hazards, Is.EqualTo(1));
            Assert.That(Runtime.Get(Board(battle), "TrailHazardCell"), Is.EqualTo(4));
            Assert.That(Runtime.Get(Board(battle), "TurnIndex"), Is.EqualTo(1));
            Runtime.Call(battle, "RestartMatch");
            yield return null; yield return null;
            Hero(battle, "luma", 0); Hero(battle, "tish", 0); Hero(battle, "bum", 0);
            Assert.That(Runtime.Get(Board(battle), "TrailHazardCell"), Is.EqualTo(3));
            Assert.That(Runtime.Get(Board(battle), "TurnIndex"), Is.EqualTo(0));
            Assert.That(Operation(battle), Is.Not.EqualTo(operation));
            Assert.That(Runtime.Field(Runtime.Field(Saved, "battle"), "turnIndex"), Is.EqualTo(0));
            Assert.That(Runtime.Get(Narrative(battle), "PendingHintId"), Is.EqualTo("L05_R02"));

            string progress = JsonUtility.ToJson(((IList)Runtime.Field(Profile, "chapters"))[0]);
            string storySeen = JsonUtility.ToJson(Profile);
            object candidate = Runtime.Static("Progression.ProfileService", "Snapshot");
            Runtime.Set(Runtime.Field(candidate, "demoCheckpoint"), "scenarioSignature", "changed-scenario");
            object[] commit = { candidate, null, false };
            Assert.That(Runtime.Static("Progression.ProfileService", "TryCommit", commit), Is.EqualTo(true), commit[1] as string);
            yield return Launch(5);
            battle = Find("View.BattleDebugController");
            yield return Opening(battle);
            Assert.That(Runtime.Get(Checkpoint(battle), "ResumeFailed"), Is.EqualTo(true));
            Assert.That(Runtime.Get(Checkpoint(battle), "Resumed"), Is.EqualTo(false));
            Hero(battle, "luma", 0); Hero(battle, "tish", 0); Hero(battle, "bum", 0);
            Assert.That(Runtime.Get(Board(battle), "TurnIndex"), Is.EqualTo(0));
            Assert.That(JsonUtility.ToJson(((IList)Runtime.Field(Profile, "chapters"))[0]), Is.EqualTo(progress));
            object before = JsonUtility.FromJson(storySeen, Runtime.Type("Progression.PlayerProfile"));
            CollectionAssert.AreEqual((IEnumerable)Runtime.Field(before, "demoStorySeen"), (IEnumerable)Runtime.Field(Profile, "demoStorySeen"));
            Assert.That(Receipts, Is.Zero);
        }

        [UnityTest]
        public IEnumerator IndependentExitAndInterruptedFinalJumpResumeThroughStoryWithOneStableReward()
        {
            SeedLevel(3);
            yield return Launch(3);
            Component battle = Find("View.BattleDebugController");
            yield return Opening(battle);
            string operation = Operation(battle);
            for (int i = 0; i < 4; i++) yield return Move(battle, "luma", 2);
            Hero(battle, "luma", -1, true); Hero(battle, "tish", 0);
            Assert.That(Runtime.Get(Board(battle), "IsFinished"), Is.EqualTo(false));
            yield return Launch(3);
            battle = Find("View.BattleDebugController");
            yield return Opening(battle);
            Hero(battle, "luma", -1, true); Hero(battle, "tish", 0);
            Assert.That(Operation(battle), Is.EqualTo(operation));
            for (int i = 0; i < 3; i++) yield return Move(battle, "tish", 2);
            SelectMove(battle, "tish", 2);
            yield return Until(() => (bool)Runtime.Get(Board(battle), "IsFinished"), "Last exit was not applied.");
            Assert.That(Runtime.Get(battle, "PresentationIsAnimating"), Is.EqualTo(true));
            Assert.That(Runtime.Field(Runtime.Field(Saved, "battle"), "finished"), Is.EqualTo(false),
                "An unfinished presentation must keep the preceding checkpoint.");
            // Immediate unload models interruption before the animation can settle or save.
            SceneManager.LoadScene("MainMenu");
            yield return Launch(3);
            battle = Find("View.BattleDebugController");
            yield return Opening(battle);
            Hero(battle, "luma", -1, true); Hero(battle, "tish", 6);
            Assert.That(Operation(battle), Is.EqualTo(operation));
            Assert.That(Receipts, Is.Zero);
            yield return Move(battle, "tish", 2);
            yield return Until(() => (bool)Runtime.Field(Runtime.Field(Saved, "battle"), "finished") &&
                (bool)Runtime.Get(Narrative(battle), "IsStoryVisible"), "Finished checkpoint or success dialogue did not appear.");
            Assert.That(Receipts, Is.Zero);
            Assert.That(Runtime.Get(Narrative(battle), "StoryId"), Is.Null, "Success lines must precede the comic.");
            Runtime.Call(Narrative(battle), "SkipStory");
            yield return Until(() => (string)Runtime.Get(Narrative(battle), "StoryId") == "S03", "Completion comic did not appear.");
            Assert.That(Runtime.Field(Saved, "completionLinesSeen"), Is.EqualTo(true));
            yield return Launch(3);
            battle = Find("View.BattleDebugController");
            yield return Until(() => (string)Runtime.Get(Narrative(battle), "StoryId") == "S03", "Finished resume did not return to its comic.");
            Assert.That(Runtime.Get(Checkpoint(battle), "Resumed"), Is.EqualTo(true));
            Assert.That(Runtime.Get(Board(battle), "IsFinished"), Is.EqualTo(true));
            Hero(battle, "luma", -1, true); Hero(battle, "tish", -1, true);
            Assert.That(Operation(battle), Is.EqualTo(operation));
            Assert.That(Receipts, Is.Zero);
            Runtime.Call(Narrative(battle), "SkipStory");
            yield return Until(() => Receipts == 1, "Result did not commit after the resumed comic.");
            Assert.That(Saved, Is.Null);
            Assert.That(Runtime.Field(((IList)Runtime.Field(Profile, "progressionReceipts"))[0], "operationId"), Is.EqualTo(operation));
            int xp = (int)Runtime.Field(Runtime.Field(Profile, "hero"), "xp");
            string currencies = JsonUtility.ToJson(Profile);
            Runtime.Call(Runtime.Get(battle, "RewardSession"), "Retry");
            Runtime.Call(Checkpoint(battle), "RetrySave");
            Assert.That(Saved, Is.Null, "A checkpoint retry after committed completion must not recreate it.");
            yield return Menu();
            Assert.That(Receipts, Is.EqualTo(1));
            Assert.That(Saved, Is.Null);
            Assert.That(Runtime.Field(Runtime.Field(Profile, "hero"), "xp"), Is.EqualTo(xp));
            object before = JsonUtility.FromJson(currencies, Runtime.Type("Progression.PlayerProfile"));
            Assert.That(JsonUtility.ToJson(Profile), Is.EqualTo(JsonUtility.ToJson(before)), "Reload/retry must not change rewards or progress.");
        }

        [UnityTest]
        public IEnumerator FailedCheckpointSaveLeavesPreviousDiskStateAndExplicitRetryStoresSettledTurn()
        {
            SeedLevel(5);
            yield return Launch(5);
            Component battle = Find("View.BattleDebugController");
            yield return Opening(battle);
            yield return Move(battle, "tish", 2);
            string previous = JsonUtility.ToJson(Saved);
            Runtime.Set(testStore, "Checkpoint", new Action<string>(stage =>
            {
                if (stage == "BeforeReplace") throw new IOException("Injected checkpoint failure.");
            }));
            yield return Move(battle, "luma", 1);
            Assert.That(Runtime.Get(Checkpoint(battle), "SavePending"), Is.EqualTo(true));
            Assert.That(JsonUtility.ToJson(Saved), Is.EqualTo(previous));
            object disk = Runtime.Call(testStore, "Load");
            Assert.That(JsonUtility.ToJson(Runtime.Field(disk, "demoCheckpoint")), Is.EqualTo(previous));
            Assert.That(Runtime.Get(Board(battle), "TrailHazardCell"), Is.EqualTo(4));
            Assert.That(Receipts, Is.Zero);
            Runtime.Set(testStore, "Checkpoint", null);
            Runtime.Call(Checkpoint(battle), "RetrySave");
            Assert.That(Runtime.Get(Checkpoint(battle), "SavePending"), Is.EqualTo(false));
            Assert.That(Runtime.Field(Runtime.Field(Saved, "battle"), "turnIndex"), Is.EqualTo(1));
            Assert.That(Runtime.Field(Runtime.Field(Saved, "battle"), "trailHazardCell"), Is.EqualTo(4));
            yield return Launch(5);
            battle = Find("View.BattleDebugController");
            yield return Opening(battle);
            Assert.That(Runtime.Get(Checkpoint(battle), "Resumed"), Is.EqualTo(true));
            Hero(battle, "tish", 2); Hero(battle, "luma", 1); Hero(battle, "bum", 0);
            Assert.That(Runtime.Get(Board(battle), "TurnIndex"), Is.EqualTo(1));
            Assert.That(Runtime.Get(Board(battle), "TrailHazardCell"), Is.EqualTo(4));
            CollectionAssert.AreEqual(new[] { 1, 2 }, (IEnumerable)Runtime.Get(battle, "PresentationDice"));
            Assert.That(Receipts, Is.Zero);
            object[] reset = { 1L, null };
            Assert.That(Runtime.Static("Progression.ProfileService", "TryApplyAdminMapReset", reset), Is.EqualTo(true));
            Runtime.Call(Checkpoint(battle), "RetrySave");
            Assert.That(Saved, Is.Null, "An active battle from an older campaign run must not recreate its checkpoint.");
            Assert.That(Runtime.Get(Checkpoint(battle), "RunChanged"), Is.EqualTo(true));
        }
    }
}
