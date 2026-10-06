using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DemoLearningTests
    {
        private static object State() { var s = Runtime.New("Core.DemoLearningState"); Runtime.Set(s, "levelId", "L4"); return s; }
        private static object Reason(int value) => Enum.ToObject(Runtime.Type("Core.DemoInputRejection"), value);
        [Test]
        public void SkipAndPassNeverProduceEvidence()
        {
            var state = State(); var policy = Runtime.New("Core.DemoLearningPolicy", state);
            Runtime.Call(policy, "Skip", true);
            Assert.That(Runtime.Call(policy, "ObserveMove", false, true, "tish", 0, 1, false, false), Is.False);
            Assert.That(Runtime.Call(policy, "ObserveMove", true, false, "tish", 0, 1, false, false), Is.False);
            Assert.That(Runtime.Get(policy, "Moves"), Is.EqualTo(0));
            Assert.That(((IList)Runtime.Field(state, "masteredSkills")).Count, Is.Zero);
        }
        [Test]
        public void DifferentLegalChoiceCountsAndRestartKeepsKnowledgeAndGuidance()
        {
            var state = State(); var policy = Runtime.New("Core.DemoLearningPolicy", state);
            Assert.That(Runtime.Call(policy, "ObserveMove", true, true, "bum", 3, 2, false, true), Is.True);
            Runtime.Call(policy, "ShowOnce", "L04_T06", false); Runtime.Call(policy, "Skip", true);
            string attempt = (string)Runtime.Field(state, "attemptId"); Runtime.Call(policy, "Restart");
            Assert.That(Runtime.Field(state, "attemptId"), Is.Not.EqualTo(attempt));
            Assert.That(Runtime.Get(policy, "Moves"), Is.EqualTo(0));
            Assert.That(Runtime.Field(state, "guidanceHidden"), Is.True);
            CollectionAssert.Contains((IList)Runtime.Field(state, "masteredSkills"), "free_landing");
            Assert.That(Runtime.Call(policy, "ShowOnce", "L04_T06", false), Is.False);
        }
        [Test]
        public void RepeatedErrorDeduplicatesAndCapsAtTwoPerReason()
        {
            var state = State(); var policy = Runtime.New("Core.DemoLearningPolicy", state);
            Assert.That(Runtime.Call(policy, "CanNarrateError", Reason(5), "turn1:2", "tish"), Is.True);
            Assert.That(Runtime.Call(policy, "CanNarrateError", Reason(5), "turn1:2", "tish"), Is.False);
            Runtime.Set(state, "boardRevision", 1);
            Assert.That(Runtime.Call(policy, "CanNarrateError", Reason(5), "turn2:2", "luma"), Is.True);
            Runtime.Set(state, "boardRevision", 2);
            Assert.That(Runtime.Call(policy, "CanNarrateError", Reason(5), "turn3:2", "bum"), Is.False);
        }
        [Test]
        public void ThreeUnaidedMovesFadeOnlyInstructionsAndHelpResetsStreak()
        {
            var policy = Runtime.New("Core.DemoLearningPolicy", State());
            for (int i = 0; i < 3; i++) Runtime.Call(policy, "ObserveMove", true, true, "tish", i, 1, false, false);
            Assert.That(Runtime.Call(policy, "ShowOnce", "L01_T04", true), Is.False);
            Assert.That(Runtime.Call(policy, "ShowOnce", "L01_T08", false), Is.True);
            Runtime.Call(policy, "Help");
            Assert.That(Runtime.Call(policy, "ShowOnce", "L01_T04", true), Is.True);
        }
        [Test]
        public void L6HasOnlyOneAutomaticInstruction()
        {
            for (int n = 1; n <= 12; n++) Assert.That(Runtime.Static("Core.DemoLearningPolicy", "IsAutomaticInstruction", 6, n), Is.EqualTo(n == 1));
        }
        [Test]
        public void NarrativeCatalogHasAllAuthoredEventsAndSevenCompleteScenes()
        {
            var catalog = Resources.Load("DemoRC/Narrative");
            var events = ((IEnumerable)Runtime.Field(catalog, "events")).Cast<object>().ToArray();
            Assert.That(events.Length, Is.EqualTo(138));
            Assert.That(events.Select(e => (string)Runtime.Field(e, "id")).Distinct().Count(), Is.EqualTo(138));
            Assert.That(events.All(e => !string.IsNullOrWhiteSpace((string)Runtime.Field(e, "ru"))), Is.True);
            foreach (object scene in (IEnumerable)Runtime.Field(catalog, "scenes"))
            {
                Assert.That(Runtime.Field(scene, "art"), Is.Not.Null);
                Assert.That(((IList)Runtime.Field(scene, "frames")).Count, Is.EqualTo(3));
            }
            Assert.That(((IList)Runtime.Field(catalog, "scenes")).Count, Is.EqualTo(7));
            object untranslated = events.Single(e => (string)Runtime.Field(e, "id") == "L01_I01");
            Assert.That(Runtime.Call(untranslated, "Text", false), Is.EqualTo(Runtime.Field(untranslated, "ru")));
        }
        [Test]
        public void LearningSerializationKeepsKnowledgeWithoutInterfaceCache()
        {
            var state = State(); var policy = Runtime.New("Core.DemoLearningPolicy", state);
            Runtime.Call(policy, "ObserveMove", true, true, "luma", 7, 2, true, false);
            Runtime.Call(policy, "ShowOnce", "L04_T10", false);
            object loaded = JsonUtility.FromJson(JsonUtility.ToJson(state), state.GetType());
            CollectionAssert.Contains((IList)Runtime.Field(loaded, "masteredSkills"), "individual_exit");
            Assert.That(Runtime.Field(loaded, "pendingHintId"), Is.EqualTo("L04_T10"));
            Assert.That(Runtime.Field(loaded, "boardRevision"), Is.EqualTo(1));
        }
    }
}
