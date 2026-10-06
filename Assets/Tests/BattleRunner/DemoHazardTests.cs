using System.Collections;
using System.Linq;
using NUnit.Framework;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DemoHazardTests
    {
        private static object State(object runner) => Runtime.Get(runner, "State");
        private static int Hazard(object runner) => (int)Runtime.Get(State(runner), "TrailHazardCell");
        private static bool Move(object runner, int from, int step) => DemoCampaignTests.Apply(runner, from, step);

        [TestCase(4)] [TestCase(6)]
        public void BarkBlocksOnlyLandingAndNeverMoves(int level)
        {
            object runner = DemoCampaignTests.Runner(level, out _);
            int events = 0;
            Runtime.Observe(runner, "OnTrailHazardMoved", _ => events++);
            Assert.That(Hazard(runner), Is.EqualTo(4));
            Assert.That(Move(runner, 0, 2), Is.True);
            if (level == 6) Assert.That(Move(runner, 0, 1), Is.True);
            int turn = (int)Runtime.Get(State(runner), "TurnIndex");
            var remaining = ((IEnumerable)Runtime.Get(runner, "RemainingDice")).Cast<int>().ToArray();
            Assert.That(Move(runner, 2, 2), Is.False);
            Assert.That(Runtime.Get(State(runner), "TurnIndex"), Is.EqualTo(turn));
            CollectionAssert.AreEqual(remaining, (IEnumerable)Runtime.Get(runner, "RemainingDice"));
            Assert.That(Move(runner, 2, 1), Is.True);
            Assert.That(Move(runner, 3, 2), Is.True);
            Assert.That(Runtime.Call(State(runner), "GetStonesAt", Runtime.Player(0), 5), Is.EqualTo(1));
            Assert.That(Hazard(runner), Is.EqualTo(4));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void RyzhMovesOnceAfterBothActionsAndNeverTakesATurn()
        {
            object runner = DemoCampaignTests.Runner(5, out _);
            int events = 0;
            Runtime.Observe(runner, "OnTrailHazardMoved", _ => events++);
            Assert.That(Move(runner, 0, 2), Is.True);
            Assert.That(Hazard(runner), Is.EqualTo(3));
            Assert.That(Runtime.Get(State(runner), "TurnIndex"), Is.EqualTo(0));
            CollectionAssert.AreEqual(new[] { 1 }, (IEnumerable)Runtime.Get(runner, "RemainingDice"));
            Assert.That(Move(runner, 0, 1), Is.True);
            Assert.That(Hazard(runner), Is.EqualTo(4));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(Runtime.Get(State(runner), "CurrentPlayer"), Is.EqualTo(Runtime.Player(0)));
            Assert.That(Runtime.Get(State(runner), "TurnsTakenB"), Is.Zero);
            Assert.That(Runtime.Call(runner, "Tick"), Is.EqualTo(false));
        }

        [Test]
        public void RyzhYieldsPermanentlyToAFriendWithoutBearingOffOrWinning()
        {
            object runner = DemoCampaignTests.Runner(5, out _);
            Assert.That(Move(runner, 0, 1), Is.True);
            Assert.That(Move(runner, 0, 2), Is.True);
            Assert.That(Move(runner, 1, 2), Is.True);
            Assert.That(Move(runner, 2, 1), Is.True);
            Assert.That(Move(runner, 3, 1), Is.True);
            Assert.That(Move(runner, 4, 2), Is.True);
            Assert.That(Hazard(runner), Is.EqualTo(-1));
            Assert.That(Runtime.Get(State(runner), "TrailHazardYielded"), Is.EqualTo(true));
            Assert.That(Runtime.Get(State(runner), "BorneOffA"), Is.Zero);
            Assert.That(Runtime.Get(State(runner), "BorneOffB"), Is.Zero);
            Assert.That(Runtime.Get(runner, "MatchEnded"), Is.EqualTo(false));
            Assert.That(Move(runner, 0, 1), Is.True);
            Assert.That(Move(runner, 1, 2), Is.True);
            Assert.That(Hazard(runner), Is.EqualTo(-1));
            Runtime.Call(runner, "Reset");
            Assert.That(Hazard(runner), Is.EqualTo(3));
            Assert.That(Runtime.Get(State(runner), "TrailHazardYielded"), Is.EqualTo(false));
            Assert.That(Runtime.Call(State(runner), "GetStonesAt", Runtime.Player(0), 0), Is.EqualTo(3));
        }

        [Test]
        public void UnplayableRemainderRequiresOneExplicitPassWhichMovesRyzh()
        {
            object runner = DemoCampaignTests.Runner(5, out _);
            object state = State(runner);
            // Advance the authored rival through its turn event, then arrange a reachable remainder.
            Move(runner, 0, 2);
            Move(runner, 0, 1);
            for (int cell = 0; cell < 8; cell++)
                while ((int)Runtime.Call(state, "GetStonesAt", Runtime.Player(0), cell) > 0)
                    Runtime.Call(state, "RemoveStoneFromCell", Runtime.Player(0), cell);
            Runtime.Call(state, "AddStoneToCell", Runtime.Player(0), 3);
            Runtime.Call(state, "AddStoneToCell", Runtime.Player(0), 3);
            Runtime.Call(state, "AddStoneToCell", Runtime.Player(0), 6);
            Assert.That(Move(runner, 6, 2), Is.True);
            Assert.That(Runtime.Get(state, "TurnIndex"), Is.EqualTo(1));
            Assert.That(Hazard(runner), Is.EqualTo(4));
            Assert.That(Runtime.Call(runner, "HasAnyLegalMove"), Is.EqualTo(false));
            Assert.That(Move(runner, 3, 1), Is.False);
            Assert.That(Runtime.Call(runner, "EndTurnIfNoMoves"), Is.EqualTo(true));
            Assert.That(Hazard(runner), Is.EqualTo(5));
            Assert.That(Runtime.Call(runner, "EndTurnIfNoMoves"), Is.EqualTo(false));
            Assert.That(Runtime.Get(state, "TurnIndex"), Is.EqualTo(2));
        }
    }
}
