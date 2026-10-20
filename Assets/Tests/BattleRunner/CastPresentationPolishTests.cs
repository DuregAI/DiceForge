using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class CastPresentationPolishTests
    {
        [Test]
        public void PortraitTonemappingPreservesTransparentBackground()
        {
            var type = System.Type.GetType("Diceforge.Editor.CastPolishReview, Assembly-CSharp-Editor", true);
            var result = (float[])type.GetMethod("CheckPortraitAlpha").Invoke(null, null);
            Assert.That(result[0], Is.LessThan(.01f), "Dialogue backdrop must remain transparent after grading.");
            Assert.That(result[1], Is.GreaterThan(.05f), "The graded character must remain opaque.");
        }

        [Test]
        public void PortraitLightIgnoresWorldSunAndForwardControlActuallyResponds()
        {
            var type = System.Type.GetType("Diceforge.Editor.CastPolishReview, Assembly-CSharp-Editor", true);
            var differences = (float[])type.GetMethod("CheckSunIsolation").Invoke(null, null);
            Assert.That(differences[0], Is.LessThan(.005f), "Portrait colors must stay stable when the world sun changes color.");
            Assert.That(differences[1], Is.GreaterThan(.02f), "Positive control must respond to the world sun; a flat 2D render is not a valid lighting test.");
        }

        [Test]
        public void CastHasDistinctTemposSeparateEyesAndIdleOnlyBoardControllers()
        {
            float[] speeds = new float[4];
            string[] names = { "Tish", "Luma", "Bum", "Scratchy" };
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Characters/" + name + "Dialogue.prefab");
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                Assert.That(skin.sharedMesh.subMeshCount, Is.EqualTo(2), name);
                Assert.That(skin.sharedMaterials.Length, Is.EqualTo(2), name);
                Assert.That(skin.sharedMaterials[0].GetFloat("_Smoothness"), Is.LessThan(.2f), name + " body stays matte");
                Assert.That(skin.sharedMaterials[1].GetFloat("_Smoothness"), Is.GreaterThan(.4f), name + " eyes reflect light");
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/_Project/Resources/Characters/Presentation/" + name + ".asset");
                speeds[i] = new SerializedObject(asset).FindProperty("idleSpeed").floatValue;
                Assert.That(root.GetComponent(Runtime.Type("View.CharacterDialoguePerformance")), Is.Not.Null, name);
            }
            Assert.That(speeds[2], Is.LessThan(speeds[1]));
            Assert.That(speeds[1], Is.LessThan(speeds[0]));
            Assert.That(speeds[0], Is.LessThan(speeds[3]));
            string[] boards =
            {
                "Assets/_Project/07_Art/Characters/TishTripo/Tish.prefab",
                "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Luma.prefab",
                "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Bum.prefab",
                "Assets/_Project/07_Art/Characters/Scratchy/ScratchyStandee.prefab"
            };
            foreach (string path in boards)
            {
                var animator = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<Animator>();
                Assert.That(animator.runtimeAnimatorController.animationClips.Length, Is.EqualTo(1));
                Assert.That(animator.runtimeAnimatorController.animationClips[0].name, Is.EqualTo("Idle_Scene"));
            }
        }

        [UnityTest]
        public IEnumerator NewLinesCancelOldGesturesAndTimedReactionsReturnSmoothly()
        {
            yield return new EnterPlayMode();
            int rate = Time.captureFramerate;
            float scale = Time.timeScale;
            bool reduced = (bool)Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").GetValue(null);
            Time.captureFramerate = 60; Time.timeScale = 1;
            Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, false);
            try
            {
                foreach (string name in new[] { "Tish", "Luma", "Bum", "Scratchy" })
                {
                    var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Characters/" + name + "Dialogue.prefab"));
                    try
                    {
                        var performance = root.GetComponent(Runtime.Type("View.CharacterDialoguePerformance"));
                        var animator = root.GetComponentInChildren<Animator>();
                        Runtime.Call(performance, "BeginLine", true);
                        Runtime.Call(performance, "PlayGesture", "Doubt");
                        for (int frame = 0; frame < 40; frame++) yield return null;
                        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dialogue_Doubt"), Is.True, name);
                        var hand = System.Array.Find(root.GetComponentsInChildren<Transform>(), t => t.name == "mixamorig:RightHand");
                        Vector3 handBefore = hand.position;
                        Runtime.Call(performance, "BeginLine", false);
                        Assert.That(Vector3.Distance(handBefore, hand.position), Is.LessThan(.00001f), "Changing a line must blend, not snap the skeleton.");
                        for (int frame = 0; frame < 40; frame++) yield return null;
                        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dialogue_Listen"), Is.True, name + " cancels old gesture");
                        var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                        Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("MouthOpen")), Is.LessThan(.01f), name);
                        Runtime.Call(performance, "BeginLine", true);
                        Runtime.Call(performance, "PlayGesture", "Doubt");
                        for (int frame = 0; frame < 230; frame++) yield return null;
                        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dialogue_Talk"), Is.True, name + " returns before the long source clip ends");
                        Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, true);
                        Runtime.Call(performance, "PlayGesture", "Greet");
                        for (int frame = 0; frame < 30; frame++) yield return null;
                        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Dialogue_Talk"), Is.True, name + " respects reduced motion");
                        Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, false);
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            finally
            {
                Time.captureFramerate = rate; Time.timeScale = scale;
                Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, reduced);
            }
            yield return new ExitPlayMode();
        }
    }
}

