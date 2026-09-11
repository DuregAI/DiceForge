using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Tests.Progression
{
    // Legacy gameplay lives in Assembly-CSharp; keep the new tests in an isolated assembly.
    internal static class R
    {
        internal const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static Type Type(string name) => System.Type.GetType("Diceforge." + name + ", Assembly-CSharp", true);
        internal static object New(string name, params object[] args) => Activator.CreateInstance(Type(name), Flags, null, args, null);
        internal static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
        internal static object Static(string type, string name, params object[] args) => Type(type).GetMethod(name, Flags).Invoke(null, args);
        internal static object Get(object target, string name) => target.GetType().GetProperty(name, Flags).GetValue(target);
        internal static object Field(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
        internal static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
        internal static object Profile => Type("Progression.ProfileService").GetProperty("Current").GetValue(null);
        internal static IList List(object target, string field) => (IList)Field(target, field);
    }

    [NonParallelizable]
    public class ProgressionTransactionTests
    {
        private readonly Dictionary<FieldInfo, object> _saved = new();
        private string _directory;
        private string _path;
        private object _store;
        private UnityEngine.Object _map;
        private int _notifications;
        private string _legacyKey;
        private bool _hadLegacy;
        private string _legacyValue;
        private UnityEngine.Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            _randomState = UnityEngine.Random.state;
            _directory = Path.Combine(Path.GetTempPath(), "DiceforgeProgressionTests", Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "player_profile.json");
            Type service = R.Type("Progression.ProfileService");
            foreach (string name in new[] { "_profile", "_store", "_lastSavedJson", "<LoadError>k__BackingField", "ProfileChanged", "OnPlayerNameChanged" })
            {
                FieldInfo field = service.GetField(name, R.Flags);
                _saved[field] = field.GetValue(null);
                field.SetValue(null, null);
            }
            _store = R.New("Progression.AtomicProfileStore", _path);
            service.GetField("_store", R.Flags).SetValue(null, _store);
            object profile = R.New("Progression.PlayerProfile");
            R.Set(profile, "playerGuid", Guid.NewGuid().ToString());
            R.Set(profile, "selectedAvatarId", R.Static("Progression.AvatarService", "GetDefaultAvatarId"));
            service.GetField("_profile", R.Flags).SetValue(null, profile);
            R.Static("Progression.ProfileService", "RebuildCache");
            R.Static("Progression.ProfileService", "Save");
            _notifications = 0;
            service.GetField("ProfileChanged", R.Flags).SetValue(null, (Action)(() => _notifications++));
            _map = (UnityEngine.Object)R.Static("Map.MapDefinitionSO", "LoadChapter", "Chapter1");
            _legacyKey = "map_state_Chapter1";
            _hadLegacy = PlayerPrefs.HasKey(_legacyKey);
            _legacyValue = PlayerPrefs.GetString(_legacyKey, "");
            PlayerPrefs.DeleteKey(_legacyKey);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pair in _saved) pair.Key.SetValue(null, pair.Value);
            _saved.Clear();
            R.Static("Progression.ProfileService", "RebuildCache");
            if (_hadLegacy) PlayerPrefs.SetString(_legacyKey, _legacyValue);
            else PlayerPrefs.DeleteKey(_legacyKey);
            UnityEngine.Random.state = _randomState;
            R.Static("Map.MapFlowRuntime", "ClearRunContext");
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private object Reward()
        {
            object reward = R.New("Progression.RewardBundle");
            R.Set(reward, "xp", 120);
            R.List(reward, "currencies").Add(R.New("Progression.ProfileAmount", "test_gold", 30));
            R.List(reward, "items").Add(R.New("Progression.ProfileAmount", "test_item", 2));
            R.List(reward, "chests").Add(R.New("Progression.ChestInstance", "fixed-chest-id", "CHEST_BASIC"));
            return reward;
        }

        private object State => R.Static("Map.MapProgressService", "Load", _map);
        private string RunId => (string)R.Static("Map.MapProgressService", "GetRunId", "Chapter1");
        private int Gold => (int)R.Static("Progression.ProfileService", "GetCurrency", "test_gold");
        private int Xp => (int)R.Field(R.Field(R.Profile, "hero"), "xp");
        private object Operation(string id = "battle-1", bool won = true, bool campaign = true, object reward = null) =>
            R.Static("Progression.ProgressionTransactionService", "Prepare", id, campaign ? "Chapter1" : null,
                campaign ? RunId : null, campaign ? "C1_01" : null, new[] { "C1_02" }, won,
                reward ?? Reward(), "Battle");
        private object Commit(object op) => R.Static("Progression.ProgressionTransactionService", "Commit", op);
        private string Status(object result) => R.Get(result, "Status").ToString();
        private void Fault(string checkpoint) => R.Set(_store, "Checkpoint", (Action<string>)(stage =>
        { if (stage == checkpoint) throw new IOException("Injected " + stage); }));
        private void Reload() => R.Static("Progression.ProfileService", "Load");

        [Test]
        public void VictoryCommitsAllRewardsAndMapBeforeReturn()
        {
            var op = Operation();
            _notifications = 0;
            Assert.That(Status(Commit(op)), Is.EqualTo("Applied"));
            Assert.That(_notifications, Is.EqualTo(1));
            Assert.That(Gold, Is.EqualTo(30));
            Assert.That(Xp, Is.EqualTo(120));
            Assert.That(R.List(R.Profile, "chestQueue").Count, Is.EqualTo(1));
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.True);
            Assert.That(R.Call(State, "IsUnlocked", "C1_02"), Is.True);
            Reload();
            Assert.That(Gold, Is.EqualTo(30));
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.True);
        }

        [Test]
        public void DuplicateResultAndRestartedProcessNeverGrantTwice()
        {
            var op = Operation();
            Commit(op);
            int notifications = _notifications;
            Assert.That(Status(Commit(op)), Is.EqualTo("AlreadyApplied"));
            Assert.That(_notifications, Is.EqualTo(notifications));
            Reload();
            var result = Commit(op);
            Assert.That(Status(result), Is.EqualTo("AlreadyApplied"));
            Assert.That(R.Get(R.Get(result, "Application"), "LevelUpData"), Is.Null);
            Assert.That(Gold, Is.EqualTo(30));
            Assert.That(R.List(R.Profile, "chestQueue").Count, Is.EqualTo(1));
        }

        [Test]
        public void NewVictoryOnCompletedNodeHasNoReward()
        {
            Commit(Operation());
            var result = Commit(Operation("battle-2"));
            Assert.That(Status(result), Is.EqualTo("Applied"));
            Assert.That(R.Get(R.Get(R.Get(result, "Application"), "RewardBundle"), "IsEmpty"), Is.True);
            Assert.That(Gold, Is.EqualTo(30));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CampaignLossAndDrawKeepNodeOpen(bool draw)
        {
            var result = Commit(Operation(won: false, reward: draw ? R.New("Progression.RewardBundle") : Reward()));
            Assert.That(Status(result), Is.EqualTo("Applied"));
            Assert.That(Gold, Is.Zero);
            Assert.That(Xp, Is.Zero);
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.False);
        }

        [Test]
        public void StandaloneAttemptsAreIndependentButEachIsIdempotent()
        {
            var first = Operation(campaign: false);
            Commit(first); Commit(first);
            Commit(Operation("standalone-2", won: false, campaign: false));
            Assert.That(Gold, Is.EqualTo(60));
            Assert.That(R.List(R.Profile, "chapters").Count, Is.Zero);
        }

        [TestCase("BeforeWrite")]
        [TestCase("BeforeFlush")]
        [TestCase("BeforeReplace")]
        public void FailedWriteLeavesMemoryAndDiskUnchangedAndRetryUsesOriginalReward(string stage)
        {
            object reward = Reward();
            var op = Operation(reward: reward);
            string before = File.ReadAllText(_path);
            _notifications = 0;
            Fault(stage);
            Assert.That(Status(Commit(op)), Is.EqualTo("SaveFailed"));
            Assert.That(Gold, Is.Zero);
            Assert.That(_notifications, Is.Zero);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(before));
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.False);
            R.Set(reward, "xp", 9999);
            R.Set(_store, "Checkpoint", null);
            Assert.That(Status(Commit(op)), Is.EqualTo("Applied"));
            Assert.That(Xp, Is.EqualTo(120));
            Assert.That(R.Field(R.List(R.Profile, "chestQueue")[0], "instanceId"), Is.EqualTo("fixed-chest-id"));
        }

        [Test]
        public void ExceptionAfterReplaceRecognizesDurableSuccess()
        {
            var op = Operation();
            Fault("AfterReplace");
            Assert.That(Status(Commit(op)), Is.EqualTo("Applied"));
            Assert.That(Status(Commit(op)), Is.EqualTo("AlreadyApplied"));
            Assert.That(Gold, Is.EqualTo(30));
        }

        [TestCase("BeforeReplace", false)]
        [TestCase("AfterReplace", true)]
        public void SimulatedProcessInterruptionLoadsWholeOldOrNewSnapshot(string stage, bool committed)
        {
            var old = R.Static("Progression.ProfileService", "Snapshot");
            Commit(Operation());
            var next = R.Static("Progression.ProfileService", "Snapshot");
            R.Call(_store, "Save", old);
            Fault(stage);
            Assert.Throws<TargetInvocationException>(() => R.Call(_store, "Save", next));
            R.Set(_store, "Checkpoint", null);
            Reload();
            Assert.That(Gold, Is.EqualTo(committed ? 30 : 0));
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.EqualTo(committed));
        }

        [Test]
        public void ResetRunPreservesWalletAndRejectsOldResults()
        {
            var old = Operation();
            Commit(old);
            string run = RunId;
            R.Static("Map.MapProgressService", "Reset", "Chapter1");
            Assert.That(RunId, Is.Not.EqualTo(run));
            Assert.That(Gold, Is.EqualTo(30));
            Assert.That(Status(Commit(old)), Is.EqualTo("SaveFailed"));
            Commit(Operation("new-run"));
            Assert.That(Gold, Is.EqualTo(60));
        }

        [Test]
        public void ResetProfilePreservesCompletedMapAndReceipts()
        {
            Commit(Operation());
            string run = RunId;
            R.Static("Progression.ProfileService", "ResetProfile");
            Assert.That(Gold, Is.Zero);
            Assert.That(RunId, Is.EqualTo(run));
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.True);
            Commit(Operation("later"));
            Assert.That(Gold, Is.Zero);
        }

        [Test]
        public void LegacyImportIsOnceOnlyAndDoesNotRegrantCompletedNodes()
        {
            PlayerPrefs.SetString(_legacyKey, "{\"currentNodeId\":\"C1_02\",\"completedNodeIds\":[\"C1_01\"],\"unlockedNodeIds\":[\"C1_01\",\"C1_02\"]}");
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.True);
            Commit(Operation());
            Assert.That(Gold, Is.Zero);
            string run = RunId;
            PlayerPrefs.SetString(_legacyKey, "broken");
            Reload();
            Assert.That(RunId, Is.EqualTo(run));
            R.Static("Map.MapProgressService", "Reset", "Chapter1");
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.False);
            Assert.That(PlayerPrefs.GetString(_legacyKey), Is.EqualTo("broken"));
        }

        [Test]
        public void FailedImportDoesNotPublishChapterAndCanRetry()
        {
            Fault("BeforeReplace");
            Assert.Throws<TargetInvocationException>(() => R.Static("Map.MapProgressService", "Load", _map));
            Assert.That(R.List(R.Profile, "chapters").Count, Is.Zero);
            R.Set(_store, "Checkpoint", null);
            Assert.That(State, Is.Not.Null);
        }

        [Test]
        public void LoadedMapIsDetachedFromProfile()
        {
            var state = State;
            R.Call(state, "MarkCompleted", "C1_01");
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.False);
        }

        [Test]
        public void MigrationPreservesInventoryUpgradesAndChests()
        {
            object profile = R.Static("Progression.ProfileService", "Snapshot");
            R.Set(profile, "version", "0.0.5");
            R.List(profile, "upgradeLevels").Add(R.New("Progression.ProfileAmount", "test_upgrade", 3));
            R.Static("Progression.ProgressionTransactionService", "ApplyReward", profile, Reward());
            R.Call(_store, "Save", profile);
            Reload();
            var state = State;
            Reload();
            Assert.That(Gold, Is.EqualTo(30));
            Assert.That(R.Static("Progression.ProfileService", "GetItemCount", "test_item"), Is.EqualTo(2));
            Assert.That(R.Static("Progression.ProfileService", "GetUpgradeLevel", "test_upgrade"), Is.EqualTo(3));
            Assert.That(R.List(R.Profile, "chestQueue").Count, Is.EqualTo(1));
            Assert.That(R.Field(R.Profile, "version"), Is.EqualTo("0.0.6"));
        }

        [Test]
        public void CorruptPrimaryRecoversBackupAndPreservesIt()
        {
            Commit(Operation());
            R.Static("Progression.ProfileService", "Save");
            string backup = File.ReadAllText(_path + ".bak");
            File.WriteAllText(_path, "{}");
            Reload();
            Assert.That(Gold, Is.EqualTo(30));
            Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.True);
            Assert.That(File.ReadAllText(_path + ".bak"), Is.EqualTo(backup));
        }

        [Test]
        public void TwoCorruptFilesAreNeverOverwrittenWithDefaults()
        {
            File.WriteAllText(_path, "{}");
            File.WriteAllText(_path + ".bak", "broken backup");
            Assert.Throws<TargetInvocationException>(Reload);
            Assert.Throws<TargetInvocationException>(() => R.Static("Progression.ProfileService", "Save"));
            Assert.That(File.ReadAllText(_path), Is.EqualTo("{}"));
            Assert.That(File.ReadAllText(_path + ".bak"), Is.EqualTo("broken backup"));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-1)]
        public void BattleSessionSavesWithoutOverlayAndDoesNotRerollOnRetry(int winner)
        {
            R.Static("Map.MapFlowRuntime", "StartStandaloneBattle");
            object session = R.New("View.BattleRewardSession", "long");
            object player = winner < 0 ? null : Enum.ToObject(R.Type("Core.PlayerId"), winner);
            object reason = Enum.Parse(R.Type("Core.MatchEndReason"), "Timeout");
            object result = R.New("Core.MatchResult", player, reason);
            Fault("BeforeReplace");
            R.Call(session, "Complete", result, winner == 0);
            Assert.That(R.Get(session, "HasPendingSave"), Is.True);
            object op = R.Field(session, "_operation");
            string reward = JsonUtility.ToJson(R.Field(op, "Reward"));
            R.Set(_store, "Checkpoint", null);
            R.Call(session, "Retry");
            Assert.That(R.Get(session, "HasPendingSave"), Is.False);
            Assert.That(JsonUtility.ToJson(R.Get(R.Get(session, "Outcome"), "RewardBundle")), Is.EqualTo(reward));
            int receipts = R.List(R.Profile, "progressionReceipts").Count;
            R.Call(session, "Complete", result, winner == 0);
            Assert.That(R.List(R.Profile, "progressionReceipts").Count, Is.EqualTo(receipts));
            if (winner < 0) Assert.That(Xp, Is.Zero);
            else Assert.That(Xp, Is.GreaterThan(0));
        }

        [TestCase("Chest")]
        [TestCase("Shop")]
        [TestCase("Story")]
        public void NonBattleNodesUseOneTransactionAndIgnoreSecondClick(string kind)
        {
            var host = new GameObject("Map transaction test");
            host.SetActive(false);
            var map = ScriptableObject.CreateInstance(R.Type("Map.MapDefinitionSO"));
            try
            {
                R.Set(map, "chapterId", "Chapter1");
                R.Set(map, "startNodeId", "C1_01");
                var node = R.New("Map.MapNodeDefinition");
                R.Set(node, "id", "C1_01");
                R.Set(node, "type", Enum.Parse(R.Type("Map.MapNodeType"), kind));
                R.List(node, "nextIds").Add("C1_02");
                R.List(R.Field(node, "reward"), "currencies").Add(R.New("Progression.ProfileAmount", "test_gold", 30));
                R.List(map, "nodes").Add(node);
                var flow = host.AddComponent(R.Type("Map.MapFlowOrchestrator"));
                R.Set(flow, "_map", map);
                R.Set(flow, "_state", State);
                R.Call(flow, "OnNodeSelected", "C1_01");
                R.Call(flow, "OnNodeSelected", "C1_01");
                Assert.That(Gold, Is.EqualTo(30));
                Assert.That(R.Call(State, "IsCompleted", "C1_01"), Is.True);
                Reload();
                Assert.That(Gold, Is.EqualTo(30));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(map); }
        }

        [Test]
        public void FailedBattleSaveBlocksNavigationAndRestartUntilRetrySucceeds()
        {
            R.Static("Map.MapFlowRuntime", "StartStandaloneBattle");
            var session = R.New("View.BattleRewardSession", "long");
            var result = R.New("Core.MatchResult", null, Enum.Parse(R.Type("Core.MatchEndReason"), "Timeout"));
            Fault("BeforeReplace");
            R.Call(session, "Complete", result, false);
            var host = new GameObject("Save failure UI test");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent(R.Type("View.BattleDebugController"));
                R.Set(controller, "<RewardSession>k__BackingField", session);
                var overlay = host.AddComponent(R.Type("View.ResultOverlayView"));
                var restart = new Button();
                var back = new Button();
                var retry = new Button();
                var title = new Label();
                R.Set(overlay, "battleController", controller);
                R.Set(overlay, "_restartButton", restart);
                R.Set(overlay, "_backToMenuButton", back);
                R.Set(overlay, "_retrySaveButton", retry);
                R.Set(overlay, "_resultLabel", title);
                R.Call(overlay, "ShowResult");
                Assert.That(title.text, Is.EqualTo("Не удалось сохранить результат"));
                Assert.That(restart.enabledSelf, Is.False);
                Assert.That(back.enabledSelf, Is.False);
                Assert.That(retry.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                R.Call(controller, "RestartMatch"); // Must return before touching the uninitialized runner.
                Assert.That(R.Get(controller, "RewardSession"), Is.SameAs(session));
                R.Set(_store, "Checkpoint", null);
                session.GetType().GetProperty("PresentationStarted", R.Flags).SetValue(session, true);
                R.Call(overlay, "RetrySave");
                Assert.That(title.text, Is.EqualTo("Draw"));
                Assert.That(restart.enabledSelf, Is.True);
                Assert.That(back.enabledSelf, Is.True);
                Assert.That(retry.style.display.value, Is.EqualTo(DisplayStyle.None));
                int count = R.List(R.Profile, "progressionReceipts").Count;
                R.Call(overlay, "ShowResult");
                Assert.That(R.List(R.Profile, "progressionReceipts").Count, Is.EqualTo(count));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
