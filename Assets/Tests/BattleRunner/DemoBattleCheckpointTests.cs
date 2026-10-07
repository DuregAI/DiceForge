using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DemoBattleCheckpointTests
    {
        [Serializable] private sealed class Book { public Level[] levels; }
        [Serializable] private sealed class Level { public string id; public Balance balance; }
        [Serializable] private sealed class Balance { public Step[] manualReplay; }
        [Serializable] private sealed class Step { public int from, step; }

        private static object Capture(object runner) => Runtime.Call(runner, "CaptureDemoCheckpoint");
        private static string Snapshot(object runner) => JsonUtility.ToJson(Capture(runner));
        private static bool Restore(object runner, object checkpoint, out string error)
        {
            object[] args = { checkpoint, null };
            bool restored = (bool)Runtime.Call(runner, "TryRestoreDemoCheckpoint", args);
            error = args[1] as string;
            return restored;
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void EachAuthoredActionResumesWithIdenticalFutureStepsHazardsAndResult(int index)
        {
            var book = JsonUtility.FromJson<Book>(File.ReadAllText("docs/DemoRC/v0.1/data/levels.json"));
            Step[] route = book.levels.Single(l => l.id == "L" + index).balance.manualReplay;
            object original = DemoCampaignTests.Runner(index, out _);
            object resumed = DemoCampaignTests.Runner(index, out _);
            foreach (Step action in route)
            {
                Assert.That(DemoCampaignTests.Apply(original, action.from, action.step), Is.True);
                Assert.That(DemoCampaignTests.Apply(resumed, action.from, action.step), Is.True);
                Assert.That(Snapshot(resumed), Is.EqualTo(Snapshot(original)), "Continued simulation diverged.");
                resumed = DemoCampaignTests.Runner(index, out _);
                int replayedEvents = 0;
                Runtime.Observe(resumed, "OnMoveApplied", _ => replayedEvents++);
                Runtime.Observe(resumed, "OnTrailHazardMoved", _ => replayedEvents++);
                Runtime.Observe(resumed, "OnMatchEnded", _ => replayedEvents++);
                object checkpoint = JsonUtility.FromJson(Snapshot(original), Runtime.Type("Core.DemoBattleCheckpoint"));
                Assert.That(Restore(resumed, checkpoint, out string error), Is.True, error);
                Assert.That(Snapshot(resumed), Is.EqualTo(Snapshot(original)));
                Assert.That(replayedEvents, Is.Zero, "Restoration replayed gameplay events.");
            }
            Assert.That(Runtime.Get(resumed, "MatchEnded"), Is.EqualTo(true));
            Assert.That(Runtime.Get(Runtime.Get(resumed, "MatchResult"), "Reason").ToString(), Is.EqualTo("Win"));
            Assert.That(Runtime.Call(resumed, "Tick"), Is.EqualTo(false));
        }

        [Test]
        public void MalformedCheckpointNeverPartiallyChangesTheRunner()
        {
            object original = DemoCampaignTests.Runner(5, out _);
            Assert.That(DemoCampaignTests.Apply(original, 0, 2), Is.True);
            string saved = Snapshot(original);
            object target = DemoCampaignTests.Runner(5, out _);
            string before = Snapshot(target);
            Action<object>[] corruptions =
            {
                c => Runtime.Set(c, "schemaVersion", 99),
                c => ((int[])Runtime.Field(c, "cellsA"))[0]++,
                c => Runtime.Set(c, "turnIndex", -1),
                c => Runtime.Set(c, "orderedBagCursor", 0),
                c => Runtime.Set(c, "remainingDice", new[] { 2 }),
                c => Runtime.Set(c, "selectedDieIndex", 999),
                c => Runtime.Set(c, "trailHazardCell", -1),
                c => Runtime.Set(c, "finished", true),
                c => Runtime.Set(c, "headMovesUsed", 999)
            };
            foreach (var corrupt in corruptions)
            {
                object checkpoint = JsonUtility.FromJson(saved, Runtime.Type("Core.DemoBattleCheckpoint"));
                corrupt(checkpoint);
                Assert.That(Restore(target, checkpoint, out string error), Is.False);
                Assert.That(error, Is.Not.Null.And.Not.Empty);
                Assert.That(Snapshot(target), Is.EqualTo(before));
            }
        }

        [Test]
        public void TimeoutRestoresAsDrawWithoutAdvancingRyzhAgain()
        {
            object original = DemoCampaignTests.Runner(5, out object rules);
            Runtime.Set(rules, "maxTurns", 1);
            Assert.That(DemoCampaignTests.Apply(original, 0, 2), Is.True);
            Assert.That(DemoCampaignTests.Apply(original, 0, 1), Is.True);
            object target = DemoCampaignTests.Runner(5, out object targetRules);
            Runtime.Set(targetRules, "maxTurns", 1);
            Assert.That(Restore(target, Capture(original), out string error), Is.True, error);
            Assert.That(Snapshot(target), Is.EqualTo(Snapshot(original)));
            Assert.That(Runtime.Get(Runtime.Get(target, "MatchResult"), "Winner"), Is.Null);
            Assert.That(Runtime.Get(Runtime.Get(target, "State"), "TrailHazardCell"), Is.EqualTo(3));
        }

        [Test]
        public void CaptureAndRestoreOwnTheirArrayCopies()
        {
            object original = DemoCampaignTests.Runner(5, out _);
            object checkpoint = Capture(original);
            object target = DemoCampaignTests.Runner(5, out _);
            Assert.That(Restore(target, checkpoint, out string error), Is.True, error);
            string originalBefore = Snapshot(original), targetBefore = Snapshot(target);
            ((int[])Runtime.Field(checkpoint, "cellsA"))[0] = 99;
            ((int[])Runtime.Field(checkpoint, "outcomeDice"))[0] = 99;
            ((int[])Runtime.Field(checkpoint, "remainingDice"))[0] = 99;
            Assert.That(Snapshot(original), Is.EqualTo(originalBefore));
            Assert.That(Snapshot(target), Is.EqualTo(targetBefore));
        }
    }
}
