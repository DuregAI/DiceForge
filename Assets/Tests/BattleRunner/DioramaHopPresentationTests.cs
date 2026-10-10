using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class DioramaHopPresentationTests
    {
        private GameObject hero, board;
        private Component mover;
        private bool reducedMotion;
        private float timeScale;
        private int captureRate;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            timeScale = Time.timeScale;
            captureRate = Time.captureFramerate;
            Time.captureFramerate = 60;
            reducedMotion = (bool)Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").GetValue(null);
            Time.timeScale = 1f;
            Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, false);
            hero = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/07_Art/DemoRCCharacters/Prefabs/bum.prefab"));
            board = new GameObject("Hop geometry test");
            var geometry = board.AddComponent(Runtime.Type("View.DioramaBoard"));
            mover = hero.GetComponent(Runtime.Type("View.BoardLayoutTokenMover"));
            Runtime.Call(mover, "SetGeometry", geometry);
            Runtime.Call(mover, "SetHopPresentation", true);
            Runtime.Call(mover, "SnapToWorld", Vector3.zero, 0);
            hero.transform.localScale = Vector3.one * .62f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(hero);
            Object.DestroyImmediate(board);
            Time.timeScale = timeScale;
            Time.captureFramerate = captureRate;
            Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, reducedMotion);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HopKeepsFormationScalePausesAndCommitsDestinationAfterContactRecovery()
        {
            var visual = hero.transform.Find("Hop presentation");
            Assert.That(visual, Is.Not.Null);
            var life = hero.GetComponent(Runtime.Type("View.GoblinLife"));
            Runtime.Call(life, "React", "UnsupportedTestReaction", .2f);
            Assert.That(Runtime.Field(life, "reaction"), Is.Null, "An unavailable reaction must leave movement animation free.");
            var animator = hero.GetComponentInChildren<Animator>();
            Assert.That(animator.transform.Find("GoblinFriend_Rig/Root"), Is.Not.Null, "Wrapping the model must preserve Animator bone paths.");
            Runtime.Call(mover, "SetVisualOffset", new Vector3(.1f, 0f, .2f));
            Runtime.Call(mover, "MoveToWorld", Vector3.right * 2f, 2, .6f);
            for (int frame = 0; frame < 18; frame++) yield return null;
            Assert.That(hero.transform.position.y, Is.GreaterThan(.1f), $"cell={Runtime.Get(mover, "CurrentCellId")}, animating={Runtime.Get(mover, "IsAnimating")}, diorama={Runtime.Field(mover, "_diorama")}, reduced={Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").GetValue(null)}, dt={Time.deltaTime}, capture={Time.captureFramerate}");
            Assert.That(Runtime.Get(mover, "CurrentCellId"), Is.EqualTo(0));
            Assert.That(hero.transform.localScale, Is.EqualTo(Vector3.one * .62f));
            var frozenPosition = hero.transform.position;
            var frozenBody = visual.localScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(hero.transform.position, Is.EqualTo(frozenPosition));
            Assert.That(visual.localScale, Is.EqualTo(frozenBody));
            Time.timeScale = 1f;
            for (int frame = 0; frame < 60 && (bool)Runtime.Get(mover, "IsAnimating"); frame++) yield return null;
            Assert.That(Runtime.Get(mover, "IsAnimating"), Is.EqualTo(false));
            Assert.That(Runtime.Get(mover, "CurrentCellId"), Is.EqualTo(2));
            Assert.That(Vector3.Distance(hero.transform.position, new Vector3(2.1f, 0f, .2f)), Is.LessThan(.0001f));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(visual.localRotation, Is.EqualTo(Quaternion.identity));
        }

        [UnityTest]
        public IEnumerator InterruptedHopResetsBodyAndReducedMotionHasNoLiftOrSquash()
        {
            var visual = hero.transform.Find("Hop presentation");
            Runtime.Call(mover, "MoveToWorld", Vector3.right * 2f, 2, .6f);
            for (int frame = 0; frame < 2; frame++) yield return null;
            Assert.That(visual.localScale.y, Is.LessThan(1f));
            Runtime.Call(mover, "SnapToWorld", Vector3.forward, 3);
            Assert.That(Runtime.Get(mover, "IsAnimating"), Is.EqualTo(false));
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, true);
            Runtime.Call(mover, "MoveToWorld", Vector3.right + Vector3.forward, 4, .08f);
            while ((bool)Runtime.Get(mover, "IsAnimating"))
            {
                Assert.That(hero.transform.position.y, Is.EqualTo(0f));
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                yield return null;
            }
            Assert.That(hero.transform.position, Is.EqualTo(Vector3.right + Vector3.forward));
            Assert.That(Runtime.Get(mover, "CurrentCellId"), Is.EqualTo(4));
        }
    }
}
