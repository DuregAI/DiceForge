using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Diceforge.View.Editor
{
    /// <summary>Imports the artist's candidates without replacing the campaign's approved visuals.</summary>
    public static class DemoCharacterAssetsBuilder
    {
        private const string Art = "Assets/_Project/07_Art/DemoRCCharacters";
        private const string Friend = "Assets/_Project/07_Art/GoblinFriendCandidate";
        private static readonly string[] Ids = { "tish", "luma", "bum", "ryzh", "bark" };

        [MenuItem("Diceforge/Demo RC/Build character candidates")]
        public static void BuildCandidates()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Build characters outside Play Mode.");
            Directory.CreateDirectory(Art + "/Materials");
            Directory.CreateDirectory(Art + "/Prefabs");
            Directory.CreateDirectory(Art + "/Animations");
            AssetDatabase.Refresh();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The project's URP Lit shader is missing.");
            var sourceWalk = AssetDatabase.LoadAllAssetsAtPath(Friend + "/GoblinFriend_Walk.fbx")
                .OfType<AnimationClip>().First(c => c.name == "GF_Walk");
            var walk = RotationWalk(sourceWalk);
            foreach (string id in Ids)
            {
                string modelPath = Art + "/Models/" + id + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = true;
                importer.importBlendShapes = false;
                importer.isReadable = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips) clip.loopTime = true;
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
                var palette = Texture(id + "_Palette", false);
                var normal = Texture(id + "_Normal", true);
                string materialPath = Art + "/Materials/" + id + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = id };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", palette);
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", id == "bark" ? .12f : .3f);
                material.SetFloat("_Smoothness", .15f);
                material.SetFloat("_Metallic", 0f);
                material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
                var idle = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                    .First(c => !c.name.StartsWith("__", StringComparison.Ordinal));
                var controller = Controller(id, idle, id == "bark" ? idle : walk);
                var root = new GameObject(char.ToUpperInvariant(id[0]) + id.Substring(1));
                try
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                    model.name = "Model";
                    // Preserve the authored height ratios. Field footprint is reviewed separately.
                    model.transform.localScale = Vector3.one * (id == "bark" ? .56f : .52f);
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterial = material;
                    var animator = model.GetComponent<Animator>();
                    if (animator == null) animator = model.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    root.AddComponent<BoardLayoutTokenMover>();
                    root.AddComponent<GoblinLife>();
                    PrefabUtility.SaveAsPrefabAsset(root, Art + "/Prefabs/" + root.name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip RotationWalk(AnimationClip source)
        {
            string path = Art + "/Animations/CharacterWalk.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = "CharacterWalk" };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = source.frameRate;
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
                if (binding.type == typeof(Transform) && binding.propertyName.StartsWith("m_LocalRotation.", StringComparison.Ordinal))
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(source, binding));
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Texture2D Texture(string name, bool normal)
        {
            string path = Art + "/Models/" + name + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static AnimatorController Controller(string id, AnimationClip idle, AnimationClip walk)
        {
            string path = Art + "/Prefabs/" + id + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            foreach (string name in new[] { "Idle", "Walk", "Selected", "Victory", "Hit", "Return" })
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name)
                    ?? machine.AddState(name);
                state.motion = name == "Walk" ? walk : idle;
                if (name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        public static void CaptureCandidates()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture candidates outside Play Mode.");
            string output = "docs/Art/DemoRCCharacters/UnityCaptures";
            Directory.CreateDirectory(output);
            var preview = new PreviewRenderUtility();
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            try
            {
                var camera = preview.camera;
                camera.orthographic = true;
                camera.backgroundColor = new Color(.13f, .17f, .15f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 30f;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.renderShadows = false;
                var key = preview.lights[0];
                key.transform.rotation = Quaternion.Euler(38f, 140f, 0f);
                key.color = new Color(1f, .91f, .8f);
                key.intensity = 2.2f;
                var fill = preview.lights[1];
                fill.transform.rotation = Quaternion.Euler(22f, -40f, 0f);
                fill.color = new Color(.76f, .87f, 1f);
                fill.intensity = 1.1f;
                preview.ambientColor = new Color(.2f, .22f, .2f);
                foreach (string id in Ids)
                {
                    string name = char.ToUpperInvariant(id[0]) + id.Substring(1);
                    var model = UnityEngine.Object.Instantiate(
                        AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Prefabs/" + name + ".prefab"));
                    preview.AddSingleGO(model);
                    var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                    var meshes = skins.Select(_ => new Mesh()).ToArray();
                    try
                    {
                        var animator = model.GetComponentInChildren<Animator>();
                        animator.Rebind();
                        animator.Update(0f);
                        var renderers = skins.Select(s => {
                            var snapshot = new GameObject(s.name + " pose snapshot");
                            preview.AddSingleGO(snapshot);
                            snapshot.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation);
                            var filter = snapshot.AddComponent<MeshFilter>();
                            var renderer = snapshot.AddComponent<MeshRenderer>();
                            renderer.sharedMaterials = s.sharedMaterials;
                            s.enabled = false;
                            return (filter, renderer);
                        }).ToArray();
                        foreach (string pose in new[] { "Idle", "Walk" })
                        {
                            var controller = (AnimatorController)animator.runtimeAnimatorController;
                            var clip = (AnimationClip)controller.layers[0].stateMachine.states
                                .First(s => s.state.name == pose).state.motion;
                            clip.SampleAnimation(animator.gameObject, clip.length * .23f);
                            // Baking forces the sampled skin to update even in an unfocused Editor.
                            for (int i = 0; i < skins.Length; i++)
                            {
                                skins[i].BakeMesh(meshes[i], false);
                                meshes[i].RecalculateBounds();
                                renderers[i].filter.sharedMesh = meshes[i];
                            }
                            var bounds = WorldBounds(meshes[0].bounds, renderers[0].renderer.transform);
                            for (int i = 1; i < meshes.Length; i++)
                                bounds.Encapsulate(WorldBounds(meshes[i].bounds, renderers[i].renderer.transform));
                            preview.BeginPreview(new Rect(0, 0, 512, 512), GUIStyle.none);
                            camera.aspect = 1f;
                            camera.backgroundColor = new Color(.13f, .17f, .15f);
                            camera.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x) * .9f;
                            camera.transform.position = bounds.center + new Vector3(1.6f, .8f, 4f);
                            camera.transform.LookAt(bounds.center);
                            preview.Render(allowScriptableRenderPipeline: true, updatefov: false);
                            var target = (RenderTexture)preview.EndPreview();
                            var previous = RenderTexture.active;
                            try
                            {
                                RenderTexture.active = target;
                                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                                pixels.Apply();
                                File.WriteAllBytes(output + "/" + id + "-" + pose.ToLowerInvariant() + ".png", pixels.EncodeToPNG());
                            }
                            finally { RenderTexture.active = previous; }
                        }
                        foreach (var rendered in renderers) UnityEngine.Object.DestroyImmediate(rendered.renderer.gameObject);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(model);
                        foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pixels);
                preview.Cleanup();
            }
        }

        private static Bounds WorldBounds(Bounds local, Transform transform)
        {
            var result = new Bounds(transform.TransformPoint(local.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        result.Encapsulate(transform.TransformPoint(local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            return result;
        }
    }
}
