using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Diceforge.Editor
{
    /// <summary>Builds the isolated animation candidate; never updates the live character catalog.</summary>
    public static class TishDialogueSetup
    {
        public const string Folder = "Assets/_Project/07_Art/Characters/Incoming/Tripo/TishDialogue_v01";
        public const string ModelPath = Folder + "/Tish_Dialogue_v01.fbx";
        public const string PrefabPath = Folder + "/Tish_Dialogue_Preview.prefab";
        public const string ScenePath = Folder + "/Tish_Dialogue_Preview.unity";
        private static readonly string[] Loops = { "Idle_Scene", "Dialogue_Listen", "Dialogue_Talk" };
        private static readonly string[] Gestures = { "Greet", "Agree", "Laugh", "Doubt" };

        [MenuItem("Diceforge/Characters/Tripo/Apply Tish to gameplay with existing hops")]
        public static void ApplyGameplay()
        {
            const string target = "Assets/_Project/07_Art/Characters/TishTripo/Tish.prefab";
            var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (candidate == null) throw new InvalidOperationException("Build Tish dialogue candidate first.");
            var idle = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Single(c => c.name == "Idle_Scene");
            string controllerPath = Folder + "/Tish_Board.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Idle") ?? machine.AddState("Idle");
            state.motion = idle;
            machine.defaultState = state;
            var root = PrefabUtility.LoadPrefabContents(target);
            try
            {
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(candidate, root.transform);
                model.name = "Model"; // Existing hop driver finds this presentation root.
                model.transform.localScale = Vector3.one * (1.36f / 1.25f);
                var animator = model.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(root, target);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Diceforge/Characters/Tripo/Build Tish dialogue candidate")]
        public static void Build()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Import the Tish dialogue FBX first.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importCameras = false;
            importer.importLights = false;
            var settings = importer.defaultClipAnimations;
            foreach (var clip in settings)
            {
                clip.name = clip.takeName.Split('|').Last();
                clip.loopTime = Loops.Contains(clip.name);
                clip.loopPose = false; // Endpoints already meet in the authored clip.
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = settings;
            importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            if (clips.Count != 7 || !Loops.All(clips.ContainsKey) || !Gestures.All(g => clips.ContainsKey("Dialogue_" + g)))
                throw new InvalidOperationException("Expected exactly the seven named dialogue/idle clips.");

            string texturePath = Folder + "/Tish_BaseColor_2K.jpg";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.maxTextureSize = 2048;
            textureImporter.sRGBTexture = true;
            textureImporter.mipmapEnabled = true;
            textureImporter.SaveAndReimport();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Tish_Preview.mat");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, Folder + "/Tish_Preview.mat");
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .22f);
            material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);

            string controllerPath = Folder + "/Tish_Dialogue.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                ConfigureController(controller, clips);
            }
            else
            {
                foreach (var child in controller.layers[0].stateMachine.states)
                    if (clips.ContainsKey(child.state.name)) child.state.motion = clips[child.state.name];
            }
            var root = new GameObject("Tish_Dialogue_Preview");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.AddComponent<Diceforge.View.TishFaceMotion>();
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                clips["Idle_Scene"].SampleAnimation(visual, 0);
                // SkinnedRenderer.bounds includes the full animation envelope, not the standing height.
                var baked = new Mesh();
                var bounds = new Bounds();
                bool hasBounds = false;
                foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.BakeMesh(baked, true);
                    foreach (var vertex in baked.vertices)
                    {
                        var point = renderer.transform.TransformPoint(vertex);
                        if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                UnityEngine.Object.DestroyImmediate(baked);
                if (!hasBounds || bounds.size.y <= 0) throw new InvalidOperationException("No skinned geometry to measure.");
                float scale = 1.25f / bounds.size.y;
                visual.transform.localScale *= scale;
                var feet = visual.GetComponentsInChildren<Transform>().Where(t => t.name.EndsWith("Foot")).ToArray();
                var center = feet.Length == 2 ? (feet[0].position + feet[1].position) / (2 * scale) : bounds.center;
                visual.transform.localPosition = new Vector3(-center.x * scale, -bounds.min.y * scale, -center.z * scale);
                CastPolishSetup.ApplyToRoot(root, "Tish");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources/Characters"))
                    AssetDatabase.CreateFolder("Assets/_Project/Resources", "Characters");
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Project/Resources/Characters/TishDialogue.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Tish dialogue] Candidate ready: 7 clips, Generic rig, in-place, 2K URP material.");
        }

        internal static void ConfigureController(AnimatorController controller, System.Collections.Generic.Dictionary<string, AnimationClip> clips)
        {
            controller.AddParameter("DialogueMode", AnimatorControllerParameterType.Int);
            var machine = controller.layers[0].stateMachine;
            var states = clips.ToDictionary(p => p.Key, p => machine.AddState(p.Key));
            foreach (var pair in states) { pair.Value.motion = clips[pair.Key]; pair.Value.writeDefaultValues = false; }
            machine.defaultState = states["Idle_Scene"];
            foreach (string source in Loops)
                for (int mode = 0; mode < Loops.Length; mode++)
                    if (source != Loops[mode]) AddModeTransition(states[source], states[Loops[mode]], mode, false);
            foreach (string gesture in Gestures)
            {
                controller.AddParameter(gesture, AnimatorControllerParameterType.Trigger);
                var state = states["Dialogue_" + gesture];
                var entry = machine.AddAnyStateTransition(state);
                entry.hasExitTime = false; entry.hasFixedDuration = true; entry.duration = .25f;
                entry.canTransitionToSelf = false;
                entry.AddCondition(AnimatorConditionMode.If, 0, gesture);
                for (int mode = 0; mode < Loops.Length; mode++) AddModeTransition(state, states[Loops[mode]], mode, true);
            }
        }

        private static void AddModeTransition(AnimatorState from, AnimatorState to, int mode, bool waitForEnd)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = waitForEnd;
            transition.exitTime = 1;
            transition.hasFixedDuration = true;
            transition.duration = .25f;
            transition.AddCondition(AnimatorConditionMode.Equals, mode, "DialogueMode");
        }

        [MenuItem("Diceforge/Characters/Tripo/Create Tish dialogue preview scene")]
        public static void CreatePreviewScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build the dialogue candidate first.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                PrefabUtility.InstantiatePrefab(prefab, scene);
                var camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, .68f, 3);
                camera.transform.LookAt(new Vector3(0, .64f, 0));
                camera.orthographic = true; camera.orthographicSize = .78f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .18f, .17f);
                var light = new GameObject("Key Light").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.6f; light.transform.rotation = Quaternion.Euler(35, -35, 0);
                var fill = new GameObject("Fill Light").AddComponent<Light>();
                fill.type = LightType.Directional; fill.intensity = .65f; fill.transform.rotation = Quaternion.Euler(15, 160, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
