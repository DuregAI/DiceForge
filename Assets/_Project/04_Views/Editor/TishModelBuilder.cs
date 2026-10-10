using System;
using System.Linq;
using Diceforge.GameModes;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diceforge.Editor
{
    public static class TishModelBuilder
    {
        private const string Folder = "Assets/_Project/07_Art/Characters/TishTripo/";
        private const string PrefabPath = Folder + "Tish.prefab";

        [MenuItem("Diceforge/Demo RC/Build animated Tish")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Build Tish in edit mode.");
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "TishAnimated.fbx");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = importer.importLights = false;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Contains("idle") ? "Idle" : "Walk";
                clip.loopTime = true;
                clip.loopPose = true;
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            var motions = AssetDatabase.LoadAllAssetsAtPath(Folder + "TishAnimated.fbx").OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__")).ToArray();
            var idle = motions.Single(c => c.name == "Idle");
            var walk = motions.Single(c => c.name == "Walk");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder + "Tish.controller")
                ?? AnimatorController.CreateAnimatorControllerAtPath(Folder + "Tish.controller");
            var machine = controller.layers[0].stateMachine;
            // GoblinLife crossfades these states when the board mover starts/stops.
            foreach (var motion in new[] { idle, walk })
            {
                var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == motion.name)
                    ?? machine.AddState(motion.name);
                state.motion = motion;
                state.speed = 1f;
                if (motion == idle) machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(Folder + "Tish_BaseColor.png");
            textureImporter.sRGBTexture = true;
            textureImporter.maxTextureSize = 2048;
            textureImporter.mipmapEnabled = true;
            textureImporter.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Tish.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tish" };
                AssetDatabase.CreateAsset(material, Folder + "Tish.mat");
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Tish_BaseColor.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", .24f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            var root = PrefabUtility.LoadPrefabContents("Assets/_Project/05_Gameplay_Data/Battle/Diorama/Goblin_Red.prefab");
            try
            {
                root.name = "Tish";
                var lod = root.GetComponent<LODGroup>();
                if (lod != null) Object.DestroyImmediate(lod);
                foreach (Transform child in root.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "TishAnimated.fbx"), root.transform);
                model.name = "Model";
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();

                // Use the posed mesh rather than FBX's oversized all-animation culling bounds.
                var bounds = new Bounds();
                bool first = true;
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh();
                    renderer.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices)
                    {
                        var point = model.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                    Object.DestroyImmediate(mesh);
                }
                if (first || bounds.size.y < .01f) throw new InvalidOperationException("Tish posed bounds invalid.");
                float scale = 1.36f / bounds.size.y;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Tish] Native Idle/Walk; posed height=" + bounds.size.y + "; model scale=" + scale);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            for (int i = 1; i <= 6; i++)
            {
                var level = AssetDatabase.LoadAssetAtPath<DemoLevelDefinition>($"Assets/_Project/05_Gameplay_Data/GameModes/Demo_Level_{i:D2}.asset");
                if (level == null) throw new InvalidOperationException("Missing demo level " + i);
                level.tishPrefab = prefab;
                EditorUtility.SetDirty(level);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
