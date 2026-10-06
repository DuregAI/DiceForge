using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DemoCampaignTests
    {
        [Serializable] private sealed class Book { public Level[] levels; }
        [Serializable] private sealed class Level { public string id; public Balance balance; }
        [Serializable] private sealed class Balance { public Step[] manualReplay; public Step[] minimalReplay; }
        [Serializable] private sealed class Step { public int turn; public string hero; public int step; public int from; }

        private static UnityEngine.Object Preset(int level) => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
            $"Assets/_Project/05_Gameplay_Data/GameModes/GM_Level_{level:00}.asset");

        internal static object Runner(int level, out object rules)
        {
            var preset = Preset(level);
            Assert.That(preset, Is.Not.Null);
            rules = Runtime.Static("Core.RulesetConfig", "FromPreset", Runtime.Field(preset, "rulesetPreset"));
            Runtime.Call(Runtime.Field(preset, "demoLevel"), "Validate", rules);
            Type service = Type.GetType("MatchService, Assembly-CSharp", true);
            object bag = service.GetMethod("BuildBagConfig", Runtime.Members).Invoke(null,
                new[] { Runtime.Field(preset, "diceBagA"), rules, "A" });
            object setup = Runtime.Static("Core.SetupConfig", "FromPreset", Runtime.Field(preset, "setupPreset"));
            var runner = Runtime.New("Core.BattleRunner");
            Runtime.Call(runner, "Init", rules, bag, null, 12345, setup);
            return runner;
        }

        internal static bool Apply(object runner, int from, int step)
        {
            int[] remaining = ((IEnumerable)Runtime.Get(runner, "RemainingDice")).Cast<int>().ToArray();
            Assert.That(Runtime.Call(runner, "SelectDieIndex", Array.IndexOf(remaining, step)), Is.EqualTo(true));
            return (bool)Runtime.Call(runner, "TryApplyHumanMove",
                Runtime.Static("Core.Move", from + step >= 8 ? "BearOff" : "MoveStone", from, step));
        }

        [TestCase(1, false)] [TestCase(1, true)]
        [TestCase(2, false)] [TestCase(2, true)]
        [TestCase(3, false)] [TestCase(3, true)]
        [TestCase(4, false)] [TestCase(4, true)]
        [TestCase(5, false)] [TestCase(5, true)]
        [TestCase(6, false)] [TestCase(6, true)]
        public void AuthoringRoutesRunOnTheSavedCampaignPresets(int index, bool minimal)
        {
            var book = JsonUtility.FromJson<Book>(File.ReadAllText("docs/DemoRC/v0.1/data/levels.json"));
            Level level = book.levels.Single(x => x.id == "L" + index);
            Step[] route = minimal ? level.balance.minimalReplay : level.balance.manualReplay;
            object runner = Runner(index, out object rules);
            object state = Runtime.Get(runner, "State");
            Assert.That(Runtime.Field(rules, "gameMode").ToString(), Is.EqualTo("SoloTrail"));
            Assert.That(Runtime.Call(state, "GetStonesAt", Runtime.Player(1), 7), Is.EqualTo(0));
            int ended = 0;
            Runtime.Observe(runner, "OnMatchEnded", _ => ended++);
            var positions = new Dictionary<string, int> { ["tish"] = 0, ["luma"] = 0, ["bum"] = 0 };
            foreach (Step action in route)
            {
                Assert.That((int)Runtime.Get(state, "TurnIndex"), Is.EqualTo(action.turn - 1));
                Assert.That(positions[action.hero], Is.EqualTo(action.from));
                Assert.That(Apply(runner, action.from, action.step), Is.True, level.id);
                positions[action.hero] = action.from + action.step >= 8 ? -1 : action.from + action.step;
            }
            Assert.That(Runtime.Get(runner, "MatchEnded"), Is.EqualTo(true));
            Assert.That(Runtime.Get(state, "Winner"), Is.EqualTo(Runtime.Player(0)));
            Assert.That(Runtime.Get(state, "BorneOffA"), Is.EqualTo(index >= 4 ? 3 : index == 3 ? 2 : 1));
            Assert.That(ended, Is.EqualTo(1));
        }

        [Test]
        public void AlternativesRefreshBothChoicesAfterExactlyOneAction()
        {
            object runner = Runner(2, out _);
            object state = Runtime.Get(runner, "State");
            Assert.That(Apply(runner, 0, 2), Is.True);
            Assert.That(Runtime.Get(state, "TurnIndex"), Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 1, 2 }, (IEnumerable)Runtime.Get(runner, "RemainingDice"));
            Assert.That(Runtime.Call(state, "GetStonesAt", Runtime.Player(0), 2), Is.EqualTo(1));
            Assert.That(Apply(runner, 2, 1), Is.True);
            Assert.That(Runtime.Get(state, "TurnIndex"), Is.EqualTo(2));
        }

        [Test]
        public void InvalidSelectedStepPreservesTheOtherLegalChoice()
        {
            object runner = Runner(2, out _);
            object state = Runtime.Get(runner, "State");
            Runtime.Call(state, "AddStoneToCell", Runtime.Player(1), 1);
            int moves = 0;
            Runtime.Observe(runner, "OnMoveApplied", _ => moves++);
            Assert.That(Apply(runner, 0, 1), Is.False);
            Assert.That(Runtime.Get(state, "TurnIndex"), Is.EqualTo(0));
            Assert.That(moves, Is.Zero);
            CollectionAssert.AreEqual(new[] { 1, 2 }, (IEnumerable)Runtime.Get(runner, "RemainingDice"));
            Assert.That(Apply(runner, 0, 2), Is.True);
            Assert.That(moves, Is.EqualTo(1));
        }

        [Test]
        public void FirstFriendMayExitWhileTheOtherIsAtTheStart()
        {
            object runner = Runner(3, out _);
            object state = Runtime.Get(runner, "State");
            foreach (int from in new[] { 0, 2, 4, 6 }) Assert.That(Apply(runner, from, 2), Is.True);
            Assert.That(Runtime.Get(state, "BorneOffA"), Is.EqualTo(1));
            Assert.That(Runtime.Get(runner, "MatchEnded"), Is.EqualTo(false));
            Assert.That(Runtime.Call(state, "GetStonesAt", Runtime.Player(0), 0), Is.EqualTo(1));
            foreach (int from in new[] { 0, 2, 4, 6 }) Assert.That(Apply(runner, from, 2), Is.True);
            Assert.That(Runtime.Get(runner, "MatchEnded"), Is.EqualTo(true));
        }

        [Test]
        public void SoloTimeoutHasNoWinnerAndResetRestoresTheWholeTeam()
        {
            object runner = Runner(3, out object rules);
            Runtime.Set(rules, "maxTurns", 1);
            Assert.That(Apply(runner, 0, 1), Is.True);
            object result = Runtime.Get(runner, "MatchResult");
            Assert.That(Runtime.Get(result, "Reason").ToString(), Is.EqualTo("Timeout"));
            Assert.That(Runtime.Get(result, "Winner"), Is.Null);
            Runtime.Call(runner, "Reset");
            object state = Runtime.Get(runner, "State");
            Assert.That(Runtime.Get(runner, "MatchEnded"), Is.EqualTo(false));
            Assert.That(Runtime.Get(state, "BorneOffA"), Is.EqualTo(0));
            Assert.That(Runtime.Call(state, "GetStonesAt", Runtime.Player(0), 0), Is.EqualTo(2));
        }

        [Test]
        public void NamedLumaMovesFromSharedCellWithoutMovingTish()
        {
            object runner = Runner(3, out _);
            var host = new GameObject("Demo token test");
            var units = new GameObject("Demo units");
            try
            {
                var view = host.AddComponent(Runtime.Type("View.StonesTokensView"));
                var preset = Preset(3);
                object map = Runtime.Field(preset, "mapConfig");
                var red = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/05_Gameplay_Data/Battle/Diorama/Goblin_Red.prefab");
                var blue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/05_Gameplay_Data/Battle/Diorama/Goblin_Blue.prefab");
                Runtime.Call(view, "Configure", Runtime.Field(map, "boardLayout"), null, units.transform, red, blue, Color.red, Color.blue);
                Runtime.Call(view, "SetDemoLevel", Runtime.Field(preset, "demoLevel"));
                Runtime.Call(view, "BuildTokensFromMatchState", Runtime.Get(runner, "State"));
                object record = null;
                Runtime.Observe(runner, "OnMoveApplied", value => record = value);
                Assert.That(Apply(runner, 0, 2), Is.True);
                Runtime.Call(view, "HandleMoveApplied", record, Runtime.Get(runner, "State"), false, "StoneA_01");
                object[] luma = { "luma", -1, null, false };
                object[] tish = { "tish", -1, null, false };
                Assert.That(view.GetType().GetMethod("TryGetHero").Invoke(view, luma), Is.EqualTo(true));
                Assert.That(view.GetType().GetMethod("TryGetHero").Invoke(view, tish), Is.EqualTo(true));
                Assert.That(luma[1], Is.EqualTo(2));
                Assert.That(tish[1], Is.EqualTo(0));
                Runtime.Call(view, "RefreshGeometry", Runtime.Get(runner, "State"));
                view.GetType().GetMethod("TryGetHero").Invoke(view, luma);
                Assert.That(luma[1], Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(units);
            }
        }
    }
}
