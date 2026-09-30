using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DioramaPresentationTests
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly Type Hud = Type.GetType("Diceforge.View.DioramaHud, Assembly-CSharp", true);

        private static string Turn(bool animating, bool ended, bool playersTurn) =>
            (string)Hud.GetMethod("ResolveTurnKey", Flags).Invoke(null, new object[] { animating, ended, playersTurn });

        private static string Hint(bool animating, bool canInteract, bool legal, bool selected, int steps) =>
            (string)Hud.GetMethod("ResolveHintKey", Flags).Invoke(null, new object[] { animating, canInteract, legal, selected, steps });

        [Test]
        public void AnimationNeverAppearsAsOpponentTurn()
        {
            Assert.That(Turn(true, false, true), Is.EqualTo("MOVING"));
            Assert.That(Turn(false, false, true), Is.EqualTo("YOUR TURN"));
            Assert.That(Turn(false, false, false), Is.EqualTo("OPPONENT'S TURN"));
            Assert.That(Turn(false, true, true), Is.EqualTo("MATCH COMPLETE"));
        }

        [Test]
        public void GuidanceFollowsTheInputPhase()
        {
            Assert.That(Hint(false, true, true, false, 2), Is.EqualTo("Choose a step."));
            Assert.That(Hint(false, true, true, true, 2), Is.EqualTo("Choose a goblin."));
            Assert.That(Hint(true, false, true, true, 1), Is.EqualTo("Moving…"));
            Assert.That(Hint(false, true, false, true, 1), Does.StartWith("No move available"));
        }

        [Test]
        public void ResultWaitsForMovementThenReaction()
        {
            bool moving = true;
            int reactions = 0;
            int results = 0;
            IEnumerator sequence = (IEnumerator)Runtime.Static("View.BattleDebugController", "WaitForPresentation",
                new Func<bool>(() => moving), 0.5f,
                new Action(() => reactions++), new Action(() => results++));

            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(sequence.Current, Is.Null);
            Assert.That(reactions, Is.Zero);
            Assert.That(results, Is.Zero);
            moving = false;
            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(sequence.Current, Is.InstanceOf<WaitForSeconds>());
            Assert.That(reactions, Is.EqualTo(1));
            Assert.That(results, Is.Zero);
            Assert.That(sequence.MoveNext(), Is.False);
            Assert.That(results, Is.EqualTo(1));
        }

        [Test]
        public void ResultIsPublishedOnlyOnce()
        {
            object rules = Runtime.New("Core.RulesetConfig");
            Runtime.Set(rules, "boardSize", 6);
            Runtime.Set(rules, "homeSize", 2);
            Runtime.Set(rules, "startCellA", 0);
            Runtime.Set(rules, "startCellB", 5);
            Runtime.Set(rules, "moveDirA", 1);
            Runtime.Set(rules, "moveDirB", -1);
            Runtime.Set(rules, "maxTurns", 10);
            Runtime.Set(rules, "totalStonesPerPlayer", 1);
            Runtime.Set(Runtime.Field(rules, "headRules"), "restrictHeadMoves", false);
            object runner = Runtime.New("Core.BattleRunner");
            Runtime.Call(runner, "Init", rules, null, null, 12345, null);
            Runtime.Call(Runtime.Get(runner, "State"), "Finish", Runtime.Player(0));

            var host = new GameObject("Result presentation test");
            host.SetActive(false);
            try
            {
                var controller = host.AddComponent(Runtime.Type("View.BattleDebugController"));
                Runtime.Set(controller, "_runner", runner);
                int shown = 0;
                Runtime.Observe(controller, "OnMatchEnded", _ => shown++);
                object result = Runtime.New("Core.MatchResult", Runtime.Player(0),
                    Enum.Parse(Runtime.Type("Core.MatchEndReason"), "Win"));
                Runtime.Call(controller, "FinalizeMatchResult", result);
                Assert.That(shown, Is.EqualTo(1));
                Runtime.Call(controller, "FinalizeMatchResult", result);
                Assert.That(shown, Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }
}
