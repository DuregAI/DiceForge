using System;
using System.IO;
using System.Linq;
using Diceforge.Dialogue;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Diceforge.Editor
{
    /// <summary>Imports the supporting cast while preserving existing gameplay prefab identities.</summary>
    public static class SupportingCastSetup
    {
        private const string Incoming = "Assets/_Project/07_Art/Characters/Incoming/Tripo/";

        [MenuItem("Diceforge/Characters/Tripo/Build supporting cast")]
        public static void Build()
        {
            BuildCharacter("Bum", 1.4f, 1.5f, "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Bum.prefab");
            BuildCharacter("Scratchy", 1.15f, 1.25f, "Assets/_Project/07_Art/Characters/Scratchy/ScratchyStandee.prefab");
            SyncNarrativeGestures();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Diceforge/Characters/Tripo/Rebuild Scratchy only")]
        public static void BuildScratchy()
        {
            BuildCharacter("Scratchy", 1.15f, 1.25f, "Assets/_Project/07_Art/Characters/Scratchy/ScratchyStandee.prefab");
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Diceforge/Characters/Tripo/Rebuild Bum only")]
        public static void BuildBum()
        {
            BuildCharacter("Bum", 1.4f, 1.5f, "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Bum.prefab");
            AssetDatabase.SaveAssets();
        }

        private static void BuildCharacter(string name, float dialogueHeight, float boardHeight, string boardPath)
        {
            string folder = Incoming + name + "_v01";
            string modelPath = folder + "/" + name + "_v01.fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing supporting cast FBX: " + name);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = importer.importBlendShapes = true;
            importer.importCameras = importer.importLights = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            var settings = importer.defaultClipAnimations;
            foreach (var clip in settings)
            {
                clip.name = clip.takeName.Split('|').Last();
                clip.loopTime = clip.name == "Idle_Scene" || clip.name == "Dialogue_Listen" || clip.name == "Dialogue_Talk";
                clip.loopPose = false;
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            string[] expected = { "Idle_Scene", "Dialogue_Listen", "Dialogue_Talk", "Dialogue_Greet", "Dialogue_Agree", "Dialogue_Laugh", "Dialogue_Doubt" };
            if (clips.Count != expected.Length || !expected.All(clips.ContainsKey))
                throw new InvalidOperationException(name + " needs seven named clips.");

            string texturePath = folder + "/" + name + "_BaseColor_2K.jpg";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.maxTextureSize = 2048;
            textureImporter.sRGBTexture = textureImporter.mipmapEnabled = true;
            textureImporter.SaveAndReimport();
            string materialPath = folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit is missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .22f);
            material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);

            string controllerPath = folder + "/" + name + "_Dialogue.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                TishDialogueSetup.ConfigureController(controller, clips);
            }
            else foreach (var state in controller.layers[0].stateMachine.states)
                if (clips.TryGetValue(state.state.name, out var clip)) state.state.motion = clip;

            string candidatePath = folder + "/" + name + "_Dialogue.prefab";
            var root = new GameObject(name + "_Dialogue");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.AddComponent<Diceforge.View.TishFaceMotion>();
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                clips["Idle_Scene"].SampleAnimation(visual, 0);
                Normalize(visual, dialogueHeight);
                CastPolishSetup.ApplyToRoot(root, name);
                PrefabUtility.SaveAsPrefabAsset(root, candidatePath);
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Resources/Characters/" + name + "Dialogue.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            string boardControllerPath = folder + "/" + name + "_Board.controller";
            var boardController = AssetDatabase.LoadAssetAtPath<AnimatorController>(boardControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(boardControllerPath);
            var machine = boardController.layers[0].stateMachine;
            var idleState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle") ?? machine.AddState("Idle");
            idleState.motion = clips["Idle_Scene"];
            machine.defaultState = idleState;
            var board = PrefabUtility.LoadPrefabContents(boardPath);
            try
            {
                foreach (Transform child in board.transform.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath), board.transform);
                model.name = "Model";
                model.transform.localScale = Vector3.one * (boardHeight / dialogueHeight);
                model.GetComponentInChildren<Animator>().runtimeAnimatorController = boardController;
                PrefabUtility.SaveAsPrefabAsset(board, boardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(board); }
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(boardController);
            Debug.Log("[Supporting cast] " + name + ": dialogue, facial accents and existing board hops ready.");
        }

        private static void Normalize(GameObject visual, float height)
        {
            var baked = new Mesh();
            var bounds = new Bounds();
            bool hasBounds = false;
            try
            {
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
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); }
            if (!hasBounds || bounds.size.y <= 0) throw new InvalidOperationException("Character has no standing mesh.");
            float scale = height / bounds.size.y;
            visual.transform.localScale *= scale;
            var feet = visual.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("Foot")).ToArray();
            Vector3 center = feet.Length == 2 ? (feet[0].position + feet[1].position) / (2 * scale) : bounds.center;
            visual.transform.localPosition = new Vector3(-center.x * scale, -bounds.min.y * scale, -center.z * scale);
        }

        public static void SyncNarrativeGestures()
        {
            var target = AssetDatabase.LoadAssetAtPath<DemoNarrativeCatalog>("Assets/_Project/Resources/DemoRC/Narrative.asset");
            if (target == null) throw new InvalidOperationException("Narrative catalog missing.");
            var source = ScriptableObject.CreateInstance<DemoNarrativeCatalog>();
            try
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText("docs/DemoRC/v0.2/narrative.json"), source);
                foreach (var item in target.events) item.gesture = source.events.FirstOrDefault(e => e.id == item.id)?.gesture;
                var sourceLines = source.scenes.SelectMany(s => s.frames).SelectMany(f => f.lines).ToDictionary(l => l.id);
                foreach (var line in target.scenes.SelectMany(s => s.frames).SelectMany(f => f.lines))
                    line.gesture = sourceLines.TryGetValue(line.id, out var original) ? original.gesture : null;
                EditorUtility.SetDirty(target);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}

