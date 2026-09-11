using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.Tests.BattleTermination
{
    // Runtime code is still in Assembly-CSharp. Bridge it without restructuring the game assemblies.
    internal static class Runtime
    {
        internal const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static Type Type(string name) => System.Type.GetType("Diceforge." + name + ", Assembly-CSharp", true);
        internal static object New(string name, params object[] args) => Activator.CreateInstance(Type(name), args);
        internal static object Get(object target, string name) => target.GetType().GetProperty(name, Members).GetValue(target);
        internal static object Field(object target, string name) => target.GetType().GetField(name, Members).GetValue(target);
        internal static void Set(object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
        internal static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Members).Invoke(target, args);
        internal static object Static(string name, string method, params object[] args) => Type(name).GetMethod(method, Members).Invoke(null, args);
        internal static object Player(int player) => Enum.ToObject(Type("Core.PlayerId"), player);
        internal static void Observe(object target, string eventName, Action<object> callback)
        {
            EventInfo info = target.GetType().GetEvent(eventName);
            Type argType = info.EventHandlerType.GetMethod("Invoke").GetParameters()[0].ParameterType;
            ParameterExpression arg = Expression.Parameter(argType);
            var body = Expression.Invoke(Expression.Constant(callback), Expression.Convert(arg, typeof(object)));
            info.AddEventHandler(target, Expression.Lambda(info.EventHandlerType, body, arg).Compile());
        }
    }

    public class BattleRunnerTerminationTests
    {
        private object _runner;
        private object _rules;
        private readonly List<string> _events = new List<string>();
        private int _ended;
        private object State => Runtime.Get(_runner, "State");
        private object Result => Runtime.Get(_runner, "MatchResult");
        private bool Finished => (bool)Runtime.Get(_runner, "MatchEnded");
        private int Turn => (int)Runtime.Get(State, "TurnIndex");
        private object[] Records => ((IEnumerable)Runtime.Get(Runtime.Get(_runner, "Log"), "Records")).Cast<object>().ToArray();

        private void Create(int maxTurns, int[] dice, int boardSize = 6, int startA = 0, int startB = 5, int dirA = 1, int dirB = -1)
        {
            _events.Clear();
            _ended = 0;
            _rules = Runtime.New("Core.RulesetConfig");
            Runtime.Set(_rules, "boardSize", boardSize);
            Runtime.Set(_rules, "homeSize", 2);
            Runtime.Set(_rules, "startCellA", startA);
            Runtime.Set(_rules, "startCellB", startB);
            Runtime.Set(_rules, "moveDirA", dirA);
            Runtime.Set(_rules, "moveDirB", dirB);
            Runtime.Set(_rules, "maxTurns", maxTurns);
            Runtime.Set(_rules, "totalStonesPerPlayer", 1);
            Runtime.Set(_rules, "verboseLog", false);
            Runtime.Set(Runtime.Field(_rules, "headRules"), "restrictHeadMoves", false);
            object bag = null;
            if (dice != null)
            {
                Array outcomes = Array.CreateInstance(Runtime.Type("Core.DiceOutcomeData"), 1);
                outcomes.SetValue(Runtime.New("Core.DiceOutcomeData", "test", 1, dice), 0);
                bag = Runtime.New("Core.DiceBagConfigData", Enum.ToObject(Runtime.Type("Core.DiceBagDrawMode"), 0), outcomes);
            }
            _runner = Runtime.New("Core.BattleRunner");
            Runtime.Call(_runner, "Init", _rules, bag, bag, 12345, null);
            Runtime.Observe(_runner, "OnTurnStarted", _ => _events.Add("turn"));
            Runtime.Observe(_runner, "OnMoveApplied", _ => _events.Add("move"));
            Runtime.Observe(_runner, "OnMatchEnded", _ => { _ended++; _events.Add("end"); });
        }

        private void Place(int[] cellsA, int[] cellsB)
        {
            foreach (string field in new[] { "_stonesAByCell", "_stonesBByCell" })
            {
                var cells = (int[])Runtime.Field(State, field);
                Array.Clear(cells, 0, cells.Length);
            }
            foreach (int cell in cellsA) Runtime.Call(State, "AddStoneToCell", Runtime.Player(0), cell);
            foreach (int cell in cellsB) Runtime.Call(State, "AddStoneToCell", Runtime.Player(1), cell);
        }

        private object Move(string kind, int from, int pip) => Runtime.Static("Core.Move", kind, from, pip);
        private bool Apply(string kind, int from, int pip) => (bool)Runtime.Call(_runner, "TryApplyHumanMove", Move(kind, from, pip));

        private void AssertTimeout(int? winner)
        {
            Assert.That(Finished, Is.True);
            Assert.That(Runtime.Get(State, "IsFinished"), Is.EqualTo(true));
            Assert.That(Runtime.Get(Result, "Reason").ToString(), Is.EqualTo("Timeout"));
            Assert.That(Runtime.Get(Result, "Winner"), Is.EqualTo(winner.HasValue ? Runtime.Player(winner.Value) : null));
            Assert.That(Runtime.Get(State, "Winner"), Is.EqualTo(Runtime.Get(Result, "Winner")));
            Assert.That(_ended, Is.EqualTo(1));
            object terminal = Records.Last();
            Assert.That(Runtime.Get(terminal, "EndReason").ToString(), Is.EqualTo("Timeout"));
            Assert.That(Runtime.Get(terminal, "Move"), Is.Null);
            Assert.That(Runtime.Get(terminal, "Winner"), Is.EqualTo(Runtime.Get(Result, "Winner")));
        }

        [TestCase("Tick")]
        [TestCase("EndTurnIfNoMoves")]
        [TestCase("TryApplyHumanMove")]
        public void BlockedBoardStopsAtTheLimitIncludingHumanAndBotPasses(string entry)
        {
            Create(10, new[] { 1 });
            Place(new[] { 0, 2, 4 }, new[] { 1, 3, 5 });
            for (int turn = 0; turn < 10; turn++)
            {
                Assert.That(Finished, Is.False, "The last allowed turn must remain available.");
                if (entry == "TryApplyHumanMove") Apply("MoveStone", 0, 1);
                else Runtime.Call(_runner, entry);
            }
            AssertTimeout(null);
            Assert.That(Turn, Is.EqualTo(10));
            Assert.That(Runtime.Get(State, "TurnsTakenA"), Is.EqualTo(5));
            Assert.That(Runtime.Get(State, "TurnsTakenB"), Is.EqualTo(5));
            Assert.That(_events.Count(e => e == "turn"), Is.EqualTo(9));
            Assert.That(_events, Does.Not.Contain("move"), "Passing is not a stone move.");
            Assert.That(Runtime.Get(Records.Last(), "TurnIndex"), Is.EqualTo(9));
            Assert.That(Runtime.Get(Records.Last(), "PlayerId"), Is.EqualTo(Runtime.Player(1)));
        }

        [Test]
        public void EmptyRollsAlsoReachTheLimit()
        {
            Create(2, null);
            Runtime.Call(_runner, "Tick");
            Assert.That(Finished, Is.False);
            Runtime.Call(_runner, "Tick");
            AssertTimeout(null);
            Assert.That(Turn, Is.EqualTo(2));
        }

        [Test]
        public void LastAllowedTurnCanUseBothDiceBeforeTimingOut()
        {
            Create(1, new[] { 1, 1 });
            Assert.That(Apply("MoveStone", 0, 1), Is.True);
            Assert.That(Finished, Is.False);
            Assert.That(((IEnumerable)Runtime.Get(_runner, "RemainingDice")).Cast<int>(), Is.EqualTo(new[] { 1 }));
            Assert.That(Apply("MoveStone", 1, 1), Is.True);
            AssertTimeout(0);
            CollectionAssert.AreEqual(new[] { "move", "move", "end" }, _events);
        }

        [Test]
        public void BearOffWinOnFinalDieTakesPriorityOverTimeout()
        {
            Create(1, new[] { 1, 1 });
            Place(new[] { 4 }, new[] { 2 });
            Assert.That(Apply("MoveStone", 4, 1), Is.True);
            Assert.That(Finished, Is.False);
            Assert.That(Apply("BearOff", 5, 1), Is.True);
            Assert.That(Finished, Is.True);
            Assert.That(Runtime.Get(Result, "Reason").ToString(), Is.EqualTo("Win"));
            Assert.That(Runtime.Get(Result, "Winner"), Is.EqualTo(Runtime.Player(0)));
            Assert.That(Records.Count(r => Runtime.Get(r, "EndReason").ToString() == "Timeout"), Is.Zero);
            CollectionAssert.AreEqual(new[] { "move", "move", "end" }, _events);
        }

        [Test]
        public void InvalidHumanMoveDoesNotConsumeTheLastTurn()
        {
            Create(1, new[] { 1 });
            Assert.That(Apply("MoveStone", 3, 1), Is.False);
            Assert.That(Finished, Is.False);
            Assert.That(Turn, Is.Zero);
            Assert.That(Records, Is.Empty);
        }

        [Test]
        public void CompletionIsSingleShotAndResetAllowsANewMatch()
        {
            Create(1, null);
            Runtime.Call(_runner, "Tick");
            AssertTimeout(null);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(Runtime.Call(_runner, "Tick"), Is.EqualTo(false));
                Assert.That(Runtime.Call(_runner, "EndTurnIfNoMoves"), Is.EqualTo(false));
                Assert.That(Runtime.Call(_runner, "RerollCurrentTurnOutcome"), Is.EqualTo(false));
                Assert.That(Runtime.Call(_runner, "SelectDieIndex", 0), Is.EqualTo(false));
                Assert.That(Apply("MoveStone", 0, 1), Is.False);
            }
            Assert.That(_ended, Is.EqualTo(1));
            Assert.That(Turn, Is.EqualTo(1));
            Assert.That(Records.Length, Is.EqualTo(1));
            Runtime.Call(_runner, "Reset");
            Assert.That(Finished, Is.False);
            Assert.That(Result, Is.Null);
            Assert.That(Turn, Is.Zero);
            Assert.That(Records, Is.Empty);
            Runtime.Call(_runner, "Tick");
            Assert.That(_ended, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void MoreBorneOffStonesWinsBeforeDistance(int winner)
        {
            Create(1, null);
            Runtime.Set(_rules, "totalStonesPerPlayer", 2);
            Place(winner == 0 ? new[] { 0 } : new[] { 5, 5 }, winner == 1 ? new[] { 5 } : new[] { 0, 0 });
            Runtime.Call(State, "AddBorneOff", Runtime.Player(winner));
            Runtime.Call(_runner, "Tick");
            AssertTimeout(winner);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RemainingDistanceUsesEachSidesStartAndDirection(int winner)
        {
            Create(1, null, boardSize: 24, startA: 20, startB: 3, dirA: 1, dirB: -1);
            Place(new[] { winner == 0 ? 2 : 21 }, new[] { winner == 1 ? 21 : 2 });
            Runtime.Call(_runner, "Tick");
            AssertTimeout(winner);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void BarTokensCountAsFurtherAwayThanStartingCell(int playerOnBar)
        {
            Create(1, null);
            Place(playerOnBar == 0 ? Array.Empty<int>() : new[] { 0 }, playerOnBar == 1 ? Array.Empty<int>() : new[] { 5 });
            Runtime.Call(State, "AddToBar", Runtime.Player(playerOnBar), 1);
            Runtime.Call(_runner, "Tick");
            AssertTimeout(1 - playerOnBar);
        }

        [Test]
        public void DistanceComparisonIncludesEveryStoneInStacks()
        {
            Create(1, null);
            Place(new[] { 0, 0, 5 }, new[] { 3, 3, 3 });
            Runtime.Call(_runner, "Tick");
            AssertTimeout(1); // A needs 13 pips, B needs 12.
        }

        [Test]
        public void DrawHasNoWinnerOrRewardsInCampaignOrStandaloneBattle()
        {
            Create(1, null);
            Runtime.Call(_runner, "Tick");
            AssertTimeout(null);
            Assert.That(Runtime.Get(Result, "IsDraw"), Is.EqualTo(true));
            foreach (bool campaign in new[] { false, true })
            {
                object reward = Runtime.Static("View.PostBattleRewardResolver", "ResolveRewardBundle", Result, false, campaign);
                Assert.That(Runtime.Get(reward, "IsEmpty"), Is.EqualTo(true));
            }
            object standalone = Runtime.Static("Progression.RewardService", "CalculateMatchRewards", Result, "long");
            Assert.That(Runtime.Get(standalone, "IsEmpty"), Is.EqualTo(true));
        }

        [Test]
        public void DrawOverlayShowsDrawAndAllowsRetry()
        {
            var root = new GameObject("Draw result test");
            root.SetActive(false);
            try
            {
                Component overlay = root.AddComponent(Runtime.Type("View.ResultOverlayView"));
                var title = new Label();
                var summary = new Label();
                var restart = new Button();
                Runtime.Set(overlay, "_resultLabel", title);
                Runtime.Set(overlay, "_summaryLabel", summary);
                Runtime.Set(overlay, "_restartButton", restart);
                object reward = Runtime.New("Progression.RewardBundle");
                object applied = Runtime.New("Progression.RewardApplicationResult", reward, 1, 1, null, "Battle");
                object outcome = Runtime.New("View.PostBattleRewardOutcome", false, true, reward, applied, true);
                Runtime.Call(overlay, "PrepareOutcomeView", outcome);
                Assert.That(title.text, Is.EqualTo("Draw"));
                Assert.That(summary.text, Does.Contain("Turn limit reached"));
                Runtime.Call(overlay, "SetNavigationButtonsReady", true, outcome);
                Assert.That(restart.enabledSelf, Is.True);
                Assert.That(restart.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
