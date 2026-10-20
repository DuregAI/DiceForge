using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class RuntimeServiceRecoveryTests
    {
        [Test]
        public void ScratchyClothHandsKeepTheirShapeInEveryDialogueClip()
        {
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Characters/ScratchyDialogue.prefab"));
            var baked = new Mesh();
            try
            {
                var animator = hero.GetComponentInChildren<Animator>();
                var skin = hero.GetComponentInChildren<SkinnedMeshRenderer>();
                var weights = skin.sharedMesh.boneWeights;
                var clips = animator.runtimeAnimatorController.animationClips;
                Assert.That(clips.Length, Is.EqualTo(7));
                foreach (string side in new[] { "Left", "Right" })
                {
                    string name = "mixamorig:" + side + "Hand";
                    int boneIndex = System.Array.FindIndex(skin.bones, b => b.name == name);
                    Assert.That(boneIndex, Is.GreaterThanOrEqualTo(0), name);
                    var hand = skin.bones[boneIndex];
                    var vertices = new System.Collections.Generic.List<int>();
                    for (int i = 0; i < weights.Length; i++)
                    {
                        var w = weights[i];
                        if (w.boneIndex0 == boneIndex && w.weight0 > .999f) vertices.Add(i);
                        int[] indices = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                        float[] values = { w.weight0, w.weight1, w.weight2, w.weight3 };
                        for (int j = 0; j < 4; j++)
                            if (values[j] > .00001f)
                                Assert.That(skin.bones[indices[j]].name.StartsWith(name) && skin.bones[indices[j]].name != name,
                                    Is.False, "Cloth fingers must not inherit the mismatched human finger rig.");
                    }
                    Assert.That(vertices.Count, Is.GreaterThan(50), name + " has an intact palm and fingers");
                    var idle = System.Array.Find(clips, c => c.name == "Idle_Scene");
                    idle.SampleAnimation(animator.gameObject, 0);
                    skin.BakeMesh(baked, true);
                    var initial = baked.vertices;
                    var reference = new Vector3[vertices.Count];
                    for (int i = 0; i < vertices.Count; i++)
                        reference[i] = Quaternion.Inverse(hand.rotation) * (skin.transform.TransformPoint(initial[vertices[i]]) - hand.position);
                    foreach (var clip in clips)
                    {
                        for (int sample = 0; sample <= 8; sample++)
                        {
                            clip.SampleAnimation(animator.gameObject, clip.length * sample / 8);
                            skin.BakeMesh(baked, true);
                            var current = baked.vertices;
                            float maximumDeviation = 0;
                            for (int i = 0; i < vertices.Count; i++)
                            {
                                Vector3 local = Quaternion.Inverse(hand.rotation) * (skin.transform.TransformPoint(current[vertices[i]]) - hand.position);
                                maximumDeviation = Mathf.Max(maximumDeviation, Vector3.Distance(reference[i], local));
                            }
                            Assert.That(maximumDeviation, Is.LessThan(.0001f), side + " hand stretches in " + clip.name + " at sample " + sample);
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(hero); }
        }


        [Test]
        public void ClosedEyelidsKeepTheirFrontFacesVisibleWithBackfaceCulling()
        {
            foreach (string name in new[] { "Tish", "Luma", "Bum", "Scratchy" })
            {
                var hero = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Characters/" + name + "Dialogue.prefab"));
                var baked = new Mesh();
                try
                {
                    var animator = hero.GetComponentInChildren<Animator>();
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                        if (clip.name == "Idle_Scene") clip.SampleAnimation(animator.gameObject, 0);
                    var skin = hero.GetComponentInChildren<SkinnedMeshRenderer>();
                    int blink = skin.sharedMesh.GetBlendShapeIndex("Blink");
                    Assert.That(blink, Is.GreaterThanOrEqualTo(0), name);
                    var deltas = new Vector3[skin.sharedMesh.vertexCount];
                    skin.sharedMesh.GetBlendShapeFrameVertices(blink, 0, deltas, null, null);
                    skin.SetBlendShapeWeight(blink, 100);
                    skin.BakeMesh(baked, true);
                    var vertices = baked.vertices;
                    var triangles = baked.triangles;
                    float frontArea = 0, totalArea = 0;
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                        if (deltas[a].sqrMagnitude < 1e-10f || deltas[b].sqrMagnitude < 1e-10f || deltas[c].sqrMagnitude < 1e-10f) continue;
                        Vector3 p = skin.transform.TransformPoint(vertices[a]);
                        Vector3 q = skin.transform.TransformPoint(vertices[b]);
                        Vector3 r = skin.transform.TransformPoint(vertices[c]);
                        if ((p.z + q.z + r.z) / 3 < .1f) continue;
                        Vector3 cross = Vector3.Cross(q - p, r - p);
                        totalArea += cross.magnitude;
                        if (cross.z > 0) frontArea += cross.magnitude;
                    }
                    Assert.That(totalArea, Is.GreaterThan(.001f), name + " has actual eyelid surfaces");
                    Assert.That(frontArea / totalArea, Is.GreaterThan(.75f), name + " eyelids must cover the eyes in Unity, which culls back faces");
                }
                finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(hero); }
            }
        }

        [UnityTest]
        public IEnumerator BothHeroesSpeakAndReturnToListeningAndRespectReducedMotion()
        {
            yield return new EnterPlayMode();
            int captureRate = Time.captureFramerate;
            float timeScale = Time.timeScale;
            bool reduced = (bool)Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").GetValue(null);
            Time.captureFramerate = 60; Time.timeScale = 1;
            Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, false);
            try
            {
                foreach (string name in new[] { "Tish", "Luma", "Bum", "Scratchy" })
                {
                    var hero = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Characters/" + name + "Dialogue.prefab"));
                    try
                    {
                        var skin = hero.GetComponentInChildren<SkinnedMeshRenderer>();
                        var animator = hero.GetComponentInChildren<Animator>();
                        int mouth = skin.sharedMesh.GetBlendShapeIndex("MouthOpen");
                        int smile = skin.sharedMesh.GetBlendShapeIndex("Smile");
                        Assert.That(mouth, Is.GreaterThanOrEqualTo(0), name);
                        animator.SetInteger("DialogueMode", 2);
                        float maximumMouth = 0;
                        for (int frame = 0; frame < 100; frame++)
                        {
                            yield return null;
                            maximumMouth = Mathf.Max(maximumMouth, skin.GetBlendShapeWeight(mouth));
                        }
                        Assert.That(maximumMouth, Is.GreaterThan(20), name + " speech accent");
                        animator.SetInteger("DialogueMode", 1);
                        for (int frame = 0; frame < 60; frame++) yield return null;
                        Assert.That(skin.GetBlendShapeWeight(mouth), Is.LessThan(.01f), name + " stops talking");
                        Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, true);
                        yield return null;
                        Assert.That(skin.GetBlendShapeWeight(smile), Is.Zero, name + " respects reduced motion");
                        Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, false);
                    }
                    finally { Object.DestroyImmediate(hero); }
                }
            }
            finally
            {
                Time.captureFramerate = captureRate; Time.timeScale = timeScale;
                Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, reduced);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TishFacialAccentsBlinkAndReactWithoutConsumingGameplayRandom()
        {
            yield return new EnterPlayMode();
            int captureRate = Time.captureFramerate;
            float timeScale = Time.timeScale;
            bool reduced = (bool)Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").GetValue(null);
            Time.captureFramerate = 60; Time.timeScale = 1;
            Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, false);
            var hero = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Characters/TishDialogue.prefab"));
            try
            {
                var face = hero.GetComponent(Runtime.Type("View.TishFaceMotion"));
                var skin = hero.GetComponentInChildren<SkinnedMeshRenderer>();
                var animator = hero.GetComponentInChildren<Animator>();
                int blink = skin.sharedMesh.GetBlendShapeIndex("Blink");
                int brows = skin.sharedMesh.GetBlendShapeIndex("BrowRaise");
                Assert.That(blink, Is.GreaterThanOrEqualTo(0));
                string random = JsonUtility.ToJson(Random.state);
                Runtime.Call(face, "LateUpdate");
                Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(random));
                float maximumBlink = 0;
                for (int frame = 0; frame < 300; frame++)
                {
                    yield return null;
                    maximumBlink = Mathf.Max(maximumBlink, skin.GetBlendShapeWeight(blink));
                }
                Assert.That(maximumBlink, Is.GreaterThan(90));
                animator.SetTrigger("Greet");
                for (int frame = 0; frame < 40; frame++) yield return null;
                Assert.That(skin.GetBlendShapeWeight(brows), Is.GreaterThan(20));
                Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, true);
                yield return null;
                Assert.That(skin.GetBlendShapeWeight(blink), Is.Zero);
                Assert.That(skin.GetBlendShapeWeight(brows), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(hero);
                Time.captureFramerate = captureRate; Time.timeScale = timeScale;
                Runtime.Type("View.DioramaBoard").GetProperty("ReducedMotion").SetValue(null, reduced);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AudioAndDiagnosticsRecoverTheirServicesWithoutDuplicatingSources()
        {
            yield return new EnterPlayMode();
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var manager = (Component)Runtime.Type("Audio.AudioManager").GetProperty("Instance").GetValue(null);
            Assert.That(manager, Is.Not.Null);
            int sources = manager.GetComponents<AudioSource>().Length;
            var prefs = Runtime.Field(manager, "_prefs");
            manager.gameObject.SetActive(false);
            Runtime.Set(manager, "_selector", null);
            Runtime.Set(manager, "_storage", null);
            manager.gameObject.SetActive(true);
            Assert.That(Runtime.Field(manager, "_selector"), Is.Not.Null);
            Assert.That(Runtime.Field(manager, "_storage"), Is.Not.Null);
            Assert.That(Runtime.Field(manager, "_prefs"), Is.SameAs(prefs));
            Assert.That(manager.GetComponents<AudioSource>().Length, Is.EqualTo(sources));
            // Exercise the exact public operation that previously blocked entering the map.
            Runtime.Call(manager, "TryPlayNext", false);

            var diagnostics = (Component)Runtime.Static("Diagnostics.DiagnosticsRuntimeController", "EnsureCreated");
            diagnostics.gameObject.SetActive(false);
            Runtime.Set(diagnostics, "_performanceSampler", null);
            diagnostics.gameObject.SetActive(true);
            Assert.That(Runtime.Field(diagnostics, "_performanceSampler"), Is.Not.Null);
            yield return null;
            yield return new ExitPlayMode();
        }
    }
}
