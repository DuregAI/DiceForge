using System;
using System.Linq;
using Diceforge.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Diceforge.Editor
{
    public static class CastPolishSetup
    {
        private const string Profiles = "Assets/_Project/Resources/Characters/Presentation/";

        [MenuItem("Diceforge/Characters/Tripo/Apply cast light and performance polish")]
        public static void Apply()
        {
            System.IO.Directory.CreateDirectory(Profiles);
            AssetDatabase.Refresh();
            foreach (string name in new[] { "Tish", "Luma", "Bum", "Scratchy" })
            {
                var resource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Characters/" + name + "Dialogue.prefab");
                var material = resource.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
                string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(material)).Replace('\\', '/');
                string eyePath = folder + "/" + name + "_Eyes.mat";
                var eye = AssetDatabase.LoadAssetAtPath<Material>(eyePath);
                if (eye == null) { eye = new Material(material); AssetDatabase.CreateAsset(eye, eyePath); }
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", name == "Scratchy" ? .10f : name == "Bum" ? .13f : .16f);
                material.SetFloat("_Metallic", 0); material.SetFloat("_SpecularHighlights", 0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                eye.SetTexture("_BaseMap", material.GetTexture("_BaseMap"));
                eye.SetColor("_BaseColor", Color.white); eye.SetFloat("_Smoothness", .52f);
                eye.SetFloat("_Metallic", 0); eye.SetFloat("_SpecularHighlights", 1);
                eye.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                EditorUtility.SetDirty(material); EditorUtility.SetDirty(eye);
                string profilePath = Profiles + name + ".asset";
                var profile = AssetDatabase.LoadAssetAtPath<CharacterPresentationProfile>(profilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<CharacterPresentationProfile>();
                    SetPersonality(profile, name);
                    AssetDatabase.CreateAsset(profile, profilePath);
                }
                profile.bodyMaterial = material; profile.eyeMaterial = eye;
                EditorUtility.SetDirty(profile);
                foreach (string path in Paths(name))
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try { ApplyToRoot(root, name); PrefabUtility.SaveAsPrefabAsset(root, path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
            }
            ConfigurePortraitRenderer();
            AssetDatabase.SaveAssets();
        }

        private static void ConfigurePortraitRenderer()
        {
            const string gradePath = Profiles + "PortraitGrade.asset";
            var grade = AssetDatabase.LoadAssetAtPath<VolumeProfile>(gradePath);
            if (grade == null)
            {
                grade = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(grade, gradePath);
                var tone = grade.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
                var color = grade.Add<ColorAdjustments>(true);
                color.postExposure.Override(.25f); color.saturation.Override(-3); color.contrast.Override(0);
                foreach (var component in grade.components) AssetDatabase.AddObjectToAsset(component, grade);
                EditorUtility.SetDirty(grade);
            }
            const string path = Profiles + "PortraitRenderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (renderer == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/05_Gameplay_Data/Battle/Diorama/MobilePipeline.asset");
                var settings = new SerializedObject(source);
                renderer = UnityEngine.Object.Instantiate((UniversalRendererData)settings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue);
                renderer.name = "Character Portrait Renderer";
                renderer.rendererFeatures.Clear();
                AssetDatabase.CreateAsset(renderer, path);
            }
            string[] pipelines =
            {
                AssetDatabase.GetAssetPath(GraphicsSettingsDefault()),
                "Assets/_Project/05_Gameplay_Data/Battle/Diorama/DesktopPipeline.asset",
                "Assets/_Project/05_Gameplay_Data/Battle/Diorama/MobilePipeline.asset",
                "Assets/_Project/05_Gameplay_Data/Battle/WoodlandHero/HeroPipeline.asset"
            };
            foreach (string asset in pipelines)
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(asset);
                var serialized = new SerializedObject(pipeline);
                var list = serialized.FindProperty("m_RendererDataList");
                if (list.arraySize > 1 && list.GetArrayElementAtIndex(1).objectReferenceValue != renderer)
                    throw new InvalidOperationException("Portrait renderer slot is already occupied: " + asset);
                list.arraySize = 2; list.GetArrayElementAtIndex(1).objectReferenceValue = renderer;
                serialized.FindProperty("m_SupportsLightLayers").boolValue = true;
                serialized.FindProperty("m_AllowPostProcessAlphaOutput").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
            }
        }

        private static UniversalRenderPipelineAsset GraphicsSettingsDefault()
            => UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset
               ?? throw new InvalidOperationException("URP is required for the character portrait light layers.");

        private static string[] Paths(string name)
        {
            string resource = "Assets/_Project/Resources/Characters/" + name + "Dialogue.prefab";
            switch (name)
            {
                case "Tish": return new[] { TishDialogueSetup.PrefabPath, resource, "Assets/_Project/07_Art/Characters/TishTripo/Tish.prefab" };
                case "Luma": return new[] { LumaDialogueSetup.PrefabPath, resource, "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Luma.prefab" };
                case "Bum": return new[] { "Assets/_Project/07_Art/Characters/Incoming/Tripo/Bum_v01/Bum_Dialogue.prefab", resource, "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Bum.prefab" };
                default: return new[] { "Assets/_Project/07_Art/Characters/Incoming/Tripo/Scratchy_v01/Scratchy_Dialogue.prefab", resource, "Assets/_Project/07_Art/Characters/Scratchy/ScratchyStandee.prefab" };
            }
        }

        public static void ApplyToRoot(GameObject root, string name)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CharacterPresentationProfile>(Profiles + name + ".asset");
            if (profile == null) return;
            profile.bodyMaterial.SetFloat("_Smoothness", name == "Scratchy" ? .10f : name == "Bum" ? .13f : .16f);
            profile.bodyMaterial.SetFloat("_Metallic", 0);
            profile.bodyMaterial.SetFloat("_SpecularHighlights", 0);
            profile.bodyMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(profile.bodyMaterial);
            var face = root.GetComponentInChildren<TishFaceMotion>();
            if (face == null) throw new InvalidOperationException(name + " has no shared facial component.");
            face.profile = profile;
            var appearance = face.GetComponent<CharacterAppearance>() ?? face.gameObject.AddComponent<CharacterAppearance>();
            appearance.profile = profile; appearance.Apply(false);
            var animator = face.GetComponentInChildren<Animator>();
            var controller = animator.runtimeAnimatorController as AnimatorController;
            bool dialogue = controller != null && controller.parameters.Any(p => p.name == "DialogueMode");
            var performance = face.GetComponent<CharacterDialoguePerformance>();
            if (dialogue)
            {
                performance = performance ?? face.gameObject.AddComponent<CharacterDialoguePerformance>();
                performance.profile = profile;
            }
            else if (performance != null) performance.profile = profile;
            if (controller == null) return;
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                state.state.speed = profile.Speed(state.state.name);
                foreach (var transition in state.state.transitions)
                {
                    transition.hasFixedDuration = true;
                    transition.duration = profile.blendSeconds;
                    if (transition.hasExitTime) transition.exitTime = .94f;
                }
            }
            foreach (var transition in controller.layers[0].stateMachine.anyStateTransitions)
            { transition.hasFixedDuration = true; transition.duration = profile.blendSeconds; transition.canTransitionToSelf = false; }
            EditorUtility.SetDirty(controller);
        }

        private static void SetPersonality(CharacterPresentationProfile p, string name)
        {
            p.characterId = name.ToLowerInvariant();
            switch (name)
            {
                case "Tish":
                    p.idleSpeed = 1; p.listenSpeed = 1; p.talkSpeed = 1.02f;
                    p.greetSpeed = 1.06f; p.agreeSpeed = 1.08f; p.laughSpeed = 1.03f; p.doubtSpeed = 1;
                    p.blendSeconds = .20f; p.gazeAmount = 30; p.speechFrequency = 15.5f; break;
                case "Luma":
                    p.idleSpeed = .94f; p.listenSpeed = .94f; p.talkSpeed = .96f;
                    p.greetSpeed = .95f; p.agreeSpeed = .98f; p.laughSpeed = 1; p.doubtSpeed = .94f;
                    p.blendSeconds = .24f; p.blinkMin = 3.5f; p.blinkMax = 6.2f;
                    p.gazeAmount = 25; p.gazeSpeed = 58; p.speechFrequency = 14; p.expressionAmount = 58; p.speechAmount = 55; break;
                case "Bum":
                    p.idleSpeed = .83f; p.listenSpeed = .86f; p.talkSpeed = .88f;
                    p.greetSpeed = .84f; p.agreeSpeed = .86f; p.laughSpeed = .90f; p.doubtSpeed = .85f;
                    p.blendSeconds = .30f; p.greetSeconds = 3.5f; p.agreeSeconds = 1.8f; p.laughSeconds = 2.8f; p.doubtSeconds = 3;
                    p.blinkMin = 4; p.blinkMax = 6.8f; p.gazeAmount = 19; p.gazeSpeed = 45;
                    p.speechFrequency = 12.5f; p.expressionAmount = 50; p.speechAmount = 48; break;
                case "Scratchy":
                    p.idleSpeed = 1.08f; p.listenSpeed = 1.04f; p.talkSpeed = 1.10f;
                    p.greetSpeed = 1.10f; p.agreeSpeed = 1.12f; p.laughSpeed = 1.12f; p.doubtSpeed = 1.06f;
                    p.blendSeconds = .18f; p.greetSeconds = 2.7f; p.agreeSeconds = 1.3f; p.laughSeconds = 2.2f; p.doubtSeconds = 2.2f;
                    p.blinkMin = 2.8f; p.blinkMax = 4.6f; p.gazeAmount = 34; p.gazeSpeed = 85;
                    p.speechFrequency = 17; p.expressionAmount = 68; p.speechAmount = 58; break;
            }
        }
    }
}

