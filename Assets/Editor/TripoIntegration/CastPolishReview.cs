using System;
using System.IO;
using System.Linq;
using Diceforge.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Diceforge.Editor
{
    public static class CastPolishReview
    {
        public const string DirectoryPath = "C:/Backforge/Tripo3D/work/cast_v05";

        private sealed class Stage : IDisposable
        {
            public readonly GameObject root;
            public readonly Camera camera;
            public readonly Light sun;
            public readonly RenderTexture target;
            private readonly bool fog;
            private readonly RenderTexture previous;

            public Stage()
            {
                fog = RenderSettings.fog; RenderSettings.fog = false;
                root = new GameObject("Temporary cast review") { hideFlags = HideFlags.HideAndDontSave };
                root.transform.position = new Vector3(0, 0, 10000);
                target = new RenderTexture(384, 512, 24, RenderTextureFormat.ARGB32); target.Create();
                previous = RenderTexture.active;
                camera = new GameObject("Portrait Camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform, false);
                camera.transform.localRotation = Quaternion.Euler(0, 180, 0);
                camera.orthographic = true; camera.orthographicSize = .76f;
                camera.cullingMask = 1 << 30; camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .20f, .18f);
                CharacterAppearance.ConfigurePortraitCamera(camera);
                CharacterAppearance.CreatePortraitLights(root.transform);
                sun = new GameObject("World sun isolation control").AddComponent<Light>();
                sun.transform.SetParent(root.transform, false); sun.type = LightType.Directional;
                sun.intensity = 1.8f; sun.color = new Color(1, .91f, .76f);
                sun.transform.localRotation = Quaternion.Euler(48, -35, 0);
            }

            public Texture2D Read()
            {
                camera.Render(); RenderTexture.active = target;
                var png = new Texture2D(384, 512, TextureFormat.RGBA32, false);
                png.ReadPixels(new Rect(0, 0, 384, 512), 0, 0); png.Apply();
                return png;
            }

            public void Dispose()
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(root); RenderSettings.fog = fog;
            }
        }

        private sealed class Model : IDisposable
        {
            public readonly GameObject root;
            public readonly Animator animator;
            public readonly SkinnedMeshRenderer skin;
            public readonly MeshRenderer display;
            public readonly Mesh mesh = new Mesh();
            public Model(Stage stage, string name)
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Resources/Characters/" + name + "Dialogue.prefab"), stage.root.transform);
                foreach (var transform in root.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 30;
                root.GetComponent<CharacterAppearance>().Apply(true);
                animator = root.GetComponentInChildren<Animator>(); skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                var item = new GameObject("CPU pose capture"); item.layer = 30; item.transform.SetParent(skin.transform, false);
                item.AddComponent<MeshFilter>().sharedMesh = mesh;
                display = item.AddComponent<MeshRenderer>(); display.sharedMaterials = skin.sharedMaterials;
                display.renderingLayerMask = CharacterAppearance.PortraitRenderingLayer;
                display.lightProbeUsage = LightProbeUsage.CustomProvided; display.reflectionProbeUsage = ReflectionProbeUsage.Off;
                var properties = new MaterialPropertyBlock(); skin.GetPropertyBlock(properties); display.SetPropertyBlock(properties);
                skin.enabled = false;
                stage.camera.transform.localPosition = new Vector3(0, name == "Bum" ? .72f : name == "Scratchy" ? .59f : .65f, 3);
                stage.camera.orthographicSize = name == "Bum" ? .85f : name == "Scratchy" ? .76f : .72f;
            }
            public void Pose(string state, float seconds)
            {
                animator.runtimeAnimatorController.animationClips.First(c => c.name == state).SampleAnimation(animator.gameObject, seconds);
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, 0);
                Shape("MouthOpen", state == "Dialogue_Talk" ? 50 : state == "Dialogue_Laugh" ? 65 : 0);
                Shape("BrowRaise", state == "Dialogue_Greet" || state == "Dialogue_Laugh" ? 55 : 0);
                Shape("Smile", state == "Dialogue_Greet" || state == "Dialogue_Agree" || state == "Dialogue_Laugh" ? 55 : 0);
                Shape("BrowConcern", state == "Dialogue_Doubt" ? 60 : 0);
                skin.BakeMesh(mesh, true); mesh.RecalculateBounds(); display.localBounds = mesh.bounds;
            }
            private void Shape(string name, float value) { int i = skin.sharedMesh.GetBlendShapeIndex(name); if (i >= 0) skin.SetBlendShapeWeight(i, value); }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(root); }
        }

        public static void CaptureGestures()
        {
            Directory.CreateDirectory(DirectoryPath + "/gestures");
            using (var stage = new Stage())
                foreach (string name in new[] { "Tish", "Luma", "Bum", "Scratchy" })
                    using (var model = new Model(stage, name))
                        foreach (string pose in new[] { "Idle_Scene", "Dialogue_Listen", "Dialogue_Talk", "Dialogue_Greet", "Dialogue_Agree", "Dialogue_Laugh", "Dialogue_Doubt" })
                        {
                            var profile = model.root.GetComponent<CharacterAppearance>().profile;
                            model.Pose(pose, pose == "Idle_Scene" ? 0 : 1.15f * profile.Speed(pose));
                            var image = stage.Read();
                            File.WriteAllBytes(DirectoryPath + "/gestures/" + name.ToLowerInvariant() + "-" + pose + ".png", image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image);
                        }
        }

        public static float[] CheckSunIsolation()
        {
            Directory.CreateDirectory(DirectoryPath + "/lighting");
            using (var stage = new Stage())
            using (var model = new Model(stage, "Tish"))
            {
                stage.sun.transform.localRotation = Quaternion.Euler(25, 155, 0);
                model.Pose("Idle_Scene", 0);
                var pixels = new Color32[4][];
                for (int i = 0; i < 4; i++)
                {
                    model.display.renderingLayerMask = i < 2 ? CharacterAppearance.PortraitRenderingLayer : 1;
                    stage.sun.color = i % 2 == 0 ? new Color(1, .03f, .01f) : new Color(.03f, .06f, 1);
                    var image = stage.Read(); pixels[i] = image.GetPixels32();
                    File.WriteAllBytes(DirectoryPath + "/lighting/" + i + ".png", image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                }
                float[] differences = new float[2];
                for (int pair = 0; pair < 2; pair++)
                    for (int i = 0; i < pixels[0].Length; i++)
                    {
                        var a = pixels[pair * 2][i]; var b = pixels[pair * 2 + 1][i];
                        differences[pair] += (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) / (255f * 3 * pixels[0].Length);
                    }
                return differences;
            }
        }

        public static float[] CheckPortraitAlpha()
        {
            using (var stage = new Stage())
            using (var model = new Model(stage, "Tish"))
            {
                model.Pose("Idle_Scene", 0); stage.camera.backgroundColor = Color.clear;
                var image = stage.Read(); var pixels = image.GetPixels32();
                float[] result = { image.GetPixel(0,0).a, pixels.Count(p => p.a > 245) / (float)pixels.Length };
                File.WriteAllBytes(DirectoryPath + "/portrait-alpha.png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
                return result;
            }
        }
    }
}

