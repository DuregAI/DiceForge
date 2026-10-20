using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Diceforge.Editor
{
    /// <summary>Builds Jo/Luma with the shared dialogue animation contract.</summary>
    public static class LumaDialogueSetup
    {
        public const string Folder = "Assets/_Project/07_Art/Characters/Incoming/Tripo/Luma_v02";
        public const string ModelPath = Folder + "/Luma_v02.fbx";
        public const string PrefabPath = Folder + "/Luma_Dialogue.prefab";

        [MenuItem("Diceforge/Characters/Tripo/Apply Luma to gameplay with existing hops")]
        public static void ApplyGameplay()
        {
            const string target = "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Luma.prefab";
            var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (candidate == null) throw new InvalidOperationException("Build Luma dialogue first.");
            var idle = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Single(c => c.name == "Idle_Scene");
            string path = Folder + "/Luma_Board.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle") ?? machine.AddState("Idle");
            state.motion = idle; machine.defaultState = state;
            var root = PrefabUtility.LoadPrefabContents(target);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(candidate, root.transform);
                model.name = "Model";
                model.transform.localScale = Vector3.one * (1.32f / 1.25f);
                var animator = model.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(root, target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        }

        [MenuItem("Diceforge/Characters/Tripo/Build Luma dialogue")]
        public static void Build()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Import the Luma FBX first.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importBlendShapes = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importCameras = false; importer.importLights = false;
            var settings = importer.defaultClipAnimations;
            foreach (var clip in settings)
            {
                clip.name = clip.takeName.Split('|').Last();
                clip.loopTime = clip.name == "Idle_Scene" || clip.name == "Dialogue_Listen" || clip.name == "Dialogue_Talk";
                clip.loopPose = false;
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = settings; importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            string[] expected = { "Idle_Scene", "Dialogue_Listen", "Dialogue_Talk", "Dialogue_Greet", "Dialogue_Agree", "Dialogue_Laugh", "Dialogue_Doubt" };
            if (clips.Count != expected.Length || !expected.All(clips.ContainsKey)) throw new InvalidOperationException("Luma needs all seven named clips.");

            string texturePath = Folder + "/Luma_BaseColor_2K.jpg";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.maxTextureSize = 2048; textureImporter.sRGBTexture = true; textureImporter.mipmapEnabled = true;
            textureImporter.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Luma.mat");
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit is missing.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "/Luma.mat");
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .22f); material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            string controllerPath = Folder + "/Luma_Dialogue.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                TishDialogueSetup.ConfigureController(controller, clips);
            }
            else foreach (var child in controller.layers[0].stateMachine.states)
                if (clips.ContainsKey(child.state.name)) child.state.motion = clips[child.state.name];
            var root = new GameObject("Luma_Dialogue");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                visual.name = "Visual"; visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.AddComponent<Diceforge.View.TishFaceMotion>();
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                clips["Idle_Scene"].SampleAnimation(visual, 0);
                var baked = new Mesh(); var bounds = new Bounds(); bool hasBounds = false;
                foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.BakeMesh(baked, true);
                    foreach (var vertex in baked.vertices)
                    {
                        Vector3 point = skin.transform.TransformPoint(vertex);
                        if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                UnityEngine.Object.DestroyImmediate(baked);
                if (!hasBounds || bounds.size.y <= 0) throw new InvalidOperationException("Luma has no standing mesh.");
                float scale = 1.25f / bounds.size.y; visual.transform.localScale *= scale;
                var feet = visual.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("Foot")).ToArray();
                Vector3 center = feet.Length == 2 ? (feet[0].position + feet[1].position) / (2 * scale) : bounds.center;
                visual.transform.localPosition = new Vector3(-center.x * scale, -bounds.min.y * scale, -center.z * scale);
                CastPolishSetup.ApplyToRoot(root, "Luma");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Resources/Characters/LumaDialogue.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
            Debug.Log("[Luma dialogue] Seven in-place clips and seven facial blendshapes ready.");
        }
    }
}
