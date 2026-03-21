using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Diceforge.Editor
{
    [InitializeOnLoad]
    public static class AlienLizardAssetBuilder
    {
        private const string ModelPath = "Assets/_Project/07_3D_Models/alien_lizard_upsidedown_low_poly.fbx";
        private const string CharacterFolder = "Assets/_Project/07_Art/Character/AlienLizard";
        private const string AnimFolder = "Assets/_Project/07_Art/Character/AlienLizard/Anim";
        private const string ControllerPath = "Assets/_Project/07_Art/Character/AlienLizard/Anim/AlienLizard.controller";
        private const string IdleClipPath = "Assets/_Project/07_Art/Character/AlienLizard/Anim/AlienLizard_Idle.anim";
        private const string LegacyEnhancedIdleClipPath = "Assets/_Project/07_Art/Character/AlienLizard/Anim/AlienLizard_IdleEnhanced.anim";
        private const string MaterialPath = "Assets/_Project/07_3D_Models/Materials/allien_lizard_texture.mat";
        private const string SourceMaterialName = "tripo_node_ec0f4cf6-d5cf-4477-bd27-05ac4ef22c11_material";
        private const string AutoBuildMarkerPath = "Temp/alien_lizard_autobuild.marker";

        static AlienLizardAssetBuilder()
        {
            EditorApplication.delayCall += TryAutoBuildFromMarker;
        }

        [MenuItem("Tools/Alien Lizard/Rebuild Assets")]
        public static void BuildAssets()
        {
            EnsureFolder(CharacterFolder);
            EnsureFolder(AnimFolder);

            ConfigureImporter();

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
                throw new InvalidOperationException($"[AlienLizardAssetBuilder] Model prefab not found at '{ModelPath}'.");

            AnimationClip walkClip = LoadRequiredClip("Walk");
            AnimationClip idleClip = CreateIdleClip(modelPrefab);

            UpdateController(idleClip, walkClip);

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(LegacyEnhancedIdleClipPath) != null)
                AssetDatabase.DeleteAsset(LegacyEnhancedIdleClipPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AlienLizardAssetBuilder] Rebuilt lizard idle assets. controller='{ControllerPath}' idle='{IdleClipPath}'.");
        }

        public static void BuildFromBatch()
        {
            BuildAssets();
        }

        private static void TryAutoBuildFromMarker()
        {
            if (!File.Exists(AutoBuildMarkerPath))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryAutoBuildFromMarker;
                return;
            }

            try
            {
                BuildAssets();
                File.Delete(AutoBuildMarkerPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AlienLizardAssetBuilder] Auto-build failed: {exception}");
            }
        }

        private static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"[AlienLizardAssetBuilder] ModelImporter not found at '{ModelPath}'.");

            Material lizardMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (lizardMaterial == null)
                throw new InvalidOperationException($"[AlienLizardAssetBuilder] Required material not found at '{MaterialPath}'.");

            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.bakeAxisConversion = true;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), SourceMaterialName), lizardMaterial);

            ModelImporterClipAnimation[] sourceClips = importer.defaultClipAnimations;
            if (sourceClips == null || sourceClips.Length == 0)
                throw new InvalidOperationException($"[AlienLizardAssetBuilder] Imported FBX '{ModelPath}' has no source animation clips.");

            ModelImporterClipAnimation walkSource = sourceClips.FirstOrDefault(IsWalkClip);
            if (walkSource == null)
            {
                string clipNames = string.Join(", ", sourceClips.Select(clip => clip.name));
                throw new InvalidOperationException($"[AlienLizardAssetBuilder] Expected Walk clip in '{ModelPath}', found: {clipNames}");
            }

            importer.clipAnimations = new[]
            {
                BuildLoopClip(walkSource, "Walk")
            };

            importer.SaveAndReimport();
        }

        private static AnimationClip CreateIdleClip(GameObject modelPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath) != null)
                AssetDatabase.DeleteAsset(IdleClipPath);

            var clip = new AnimationClip
            {
                name = "AlienLizard_Idle",
                frameRate = 30f
            };

            // Conservative idle: no pelvis/spine deformation and no vertical body bob.
            AddBoneEulerCurveIfPresent(modelPrefab, clip, "Neck", "localEulerAnglesRaw.y", new[] { 0f, -3f, 3f, -2f, 0f });
            AddBoneEulerCurveIfPresent(modelPrefab, clip, "Head", "localEulerAnglesRaw.y", new[] { 0f, 7f, -7f, 5f, 0f });
            AddBoneEulerCurveIfPresent(modelPrefab, clip, "Head", "localEulerAnglesRaw.x", new[] { 0f, 2.5f, -1.5f, 1.5f, 0f });
            AddBoneEulerCurveIfPresent(modelPrefab, clip, "Tail_01", "localEulerAnglesRaw.y", new[] { 0f, 10f, -10f, 7f, 0f });
            AddBoneEulerCurveIfPresent(modelPrefab, clip, "Tail_02", "localEulerAnglesRaw.y", new[] { 0f, 16f, -16f, 12f, 0f });

            ConfigureClipLooping(clip);

            AssetDatabase.CreateAsset(clip, IdleClipPath);
            AssetDatabase.ImportAsset(IdleClipPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        }

        private static void UpdateController(AnimationClip idleClip, AnimationClip walkClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = FindOrCreateState(stateMachine, "Idle", new Vector3(240f, 90f, 0f));
            AnimatorState walkState = FindOrCreateState(stateMachine, "Walk", new Vector3(520f, 90f, 0f));

            idleState.motion = idleClip;
            walkState.motion = walkClip;
            stateMachine.defaultState = idleState;

            EnsureTransition(idleState, walkState, AnimatorConditionMode.If, "IsMoving");
            EnsureTransition(walkState, idleState, AnimatorConditionMode.IfNot, "IsMoving");

            EditorUtility.SetDirty(controller);
        }

        private static AnimationClip LoadRequiredClip(string clipName)
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(asset => asset.name == clipName);

            if (clip == null)
            {
                string available = string.Join(", ",
                    AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Select(asset => asset.name));
                throw new InvalidOperationException($"[AlienLizardAssetBuilder] Clip '{clipName}' not found at '{ModelPath}'. Available: {available}");
            }

            return clip;
        }

        private static ModelImporterClipAnimation BuildLoopClip(ModelImporterClipAnimation sourceClip, string clipName)
        {
            sourceClip.name = clipName;
            sourceClip.loopTime = true;
            sourceClip.loopPose = true;
            return sourceClip;
        }

        private static bool IsWalkClip(ModelImporterClipAnimation clip)
        {
            return string.Equals(clip.name, "Walk", StringComparison.OrdinalIgnoreCase) ||
                   clip.name.EndsWith("|Walk", StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
                return;

            string[] segments = assetFolderPath.Split('/');
            string current = segments[0];

            for (int i = 1; i < segments.Length; i++)
            {
                string next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static void AddBoneEulerCurveIfPresent(GameObject modelPrefab, AnimationClip clip, string transformName, string propertyName, float[] values)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (instance == null)
                return;

            try
            {
                Transform target = instance.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform => transform.name == transformName);
                if (target == null)
                    return;

                string path = AnimationUtility.CalculateTransformPath(target, instance.transform);
                SetCurve(clip, path, propertyName, values);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void SetCurve(AnimationClip clip, string path, string propertyName, float[] values)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, values[0]),
                new Keyframe(0.3f, values[1]),
                new Keyframe(0.6f, values[2]),
                new Keyframe(0.9f, values[3]),
                new Keyframe(1.2f, values[4]));

            for (int i = 0; i < curve.length; i++)
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Auto);

            clip.SetCurve(path, typeof(Transform), propertyName, curve);
        }

        private static void ConfigureClipLooping(AnimationClip clip)
        {
            var serializedClip = new SerializedObject(clip);
            SerializedProperty settings = serializedClip.FindProperty("m_AnimationClipSettings");
            if (settings == null)
                return;

            settings.FindPropertyRelative("m_LoopTime").boolValue = true;
            settings.FindPropertyRelative("m_LoopBlend").boolValue = true;
            settings.FindPropertyRelative("m_KeepOriginalOrientation").boolValue = true;
            settings.FindPropertyRelative("m_KeepOriginalPositionY").boolValue = true;
            settings.FindPropertyRelative("m_KeepOriginalPositionXZ").boolValue = true;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(parameter => parameter.name == parameterName && parameter.type == type))
                return;

            controller.AddParameter(parameterName, type);
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine stateMachine, string stateName, Vector3 position)
        {
            for (int i = 0; i < stateMachine.states.Length; i++)
            {
                ChildAnimatorState childState = stateMachine.states[i];
                if (childState.state != null && childState.state.name == stateName)
                    return childState.state;
            }

            return stateMachine.AddState(stateName, position);
        }

        private static void EnsureTransition(AnimatorState source, AnimatorState destination, AnimatorConditionMode conditionMode, string parameterName)
        {
            AnimatorStateTransition transition = source.transitions.FirstOrDefault(candidate => candidate.destinationState == destination);
            if (transition == null)
                transition = source.AddTransition(destination);

            transition.hasExitTime = false;
            transition.duration = 0.1f;

            bool hasCondition = transition.conditions.Any(condition =>
                condition.mode == conditionMode &&
                condition.parameter == parameterName);

            if (!hasCondition)
            {
                foreach (AnimatorCondition condition in transition.conditions.ToArray())
                    transition.RemoveCondition(condition);

                transition.AddCondition(conditionMode, 0f, parameterName);
            }
        }
    }
}
