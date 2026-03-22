using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Diceforge.View;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diceforge.Editor
{
    [InitializeOnLoad]
    public static class GoblinAssetValidator
    {
        private const string ModelPath = "Assets/_Project/07_3D_Models/goblin.fbx";
        private const string MarkerPath = "Temp/goblin_validate.marker";
        private const string ReportPath = "Temp/goblin_validation_report.txt";
        private const string ValidationRootFolder = "Assets/_Project/07_Art/Character/Goblin";
        private const string ValidationAnimFolder = "Assets/_Project/07_Art/Character/Goblin/Anim";
        private const string ValidationControllerPath = "Assets/_Project/07_Art/Character/Goblin/Anim/Goblin_Validation.controller";
        private const string ValidationPrefabPath = "Assets/_Project/99_Prefabs/Battle/Units/GoblinValidationPrefab.prefab";
        private const string KnightPrefabPath = "Assets/_Project/99_Prefabs/Battle/Units/KnightPrefab.prefab";

        static GoblinAssetValidator()
        {
            EditorApplication.delayCall += TryRunFromMarker;
        }

        [MenuItem("Tools/Goblin/Validate Import")]
        public static void ValidateGoblinAsset()
        {
            var report = new StringBuilder();
            report.AppendLine("[GoblinAssetValidator] Starting validation.");

            EnsureFolder(ValidationRootFolder);
            EnsureFolder(ValidationAnimFolder);

            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"[GoblinAssetValidator] ModelImporter not found at '{ModelPath}'.");

            var appliedFixes = new List<string>();
            if (NormalizeImporter(importer, appliedFixes))
            {
                importer.SaveAndReimport();
                importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
                if (importer == null)
                    throw new InvalidOperationException($"[GoblinAssetValidator] ModelImporter reload failed at '{ModelPath}'.");
            }

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
                throw new InvalidOperationException($"[GoblinAssetValidator] Model prefab not found at '{ModelPath}'.");

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            Mesh[] meshes = subAssets.OfType<Mesh>().ToArray();
            Material[] materials = subAssets.OfType<Material>().ToArray();
            AnimationClip[] clips = subAssets
                .OfType<AnimationClip>()
                .Where(clip => !string.Equals(clip.name, "__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Avatar[] avatars = subAssets.OfType<Avatar>().ToArray();

            AnimationClip walkClip = clips.FirstOrDefault(clip => string.Equals(clip.name, "Walk", StringComparison.OrdinalIgnoreCase))
                ?? clips.FirstOrDefault(clip => clip.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0)
                ?? clips.FirstOrDefault();

            var instance = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"[GoblinAssetValidator] Failed to instantiate '{ModelPath}'.");

            try
            {
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                SkinnedMeshRenderer[] skinnedRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
                int totalBones = skinnedRenderers.Sum(renderer => renderer.bones != null ? renderer.bones.Length : 0);
                int uniqueBoneCount = skinnedRenderers
                    .SelectMany(renderer => renderer.bones ?? Array.Empty<Transform>())
                    .Where(bone => bone != null)
                    .Distinct()
                    .Count();
                bool hasNullBoneReference = skinnedRenderers.Any(renderer => renderer.rootBone == null || renderer.bones == null || renderer.bones.Any(bone => bone == null));

                Bounds combinedBounds = CalculateCombinedBounds(renderers, instance.transform.position);
                float lowestPoint = combinedBounds.min.y - instance.transform.position.y;
                float highestPoint = combinedBounds.max.y - instance.transform.position.y;
                string materialStatus = DescribeMaterials(renderers, materials);
                string rigStatus = DescribeRig(skinnedRenderers.Length, uniqueBoneCount, hasNullBoneReference, importer.animationType, animator, avatars.Length);

                float knightHeightRatio = CompareToKnight(combinedBounds);
                WalkSampleSummary walkSummary = SampleWalk(instance, walkClip);

                EnsureValidationAssets(modelPrefab, walkClip);

                report.AppendLine($"Source Asset: {ModelPath}");
                report.AppendLine($"Applied Fixes: {(appliedFixes.Count == 0 ? "none" : string.Join(", ", appliedFixes))}");
                report.AppendLine($"Meshes: {meshes.Length} imported mesh asset(s).");
                report.AppendLine($"Materials: {materials.Length} imported material asset(s). Renderer status: {materialStatus}");
                report.AppendLine($"Root Transform: name='{instance.name}' pos={instance.transform.localPosition} rot={instance.transform.localEulerAngles} scale={instance.transform.localScale}");
                report.AppendLine($"Bounds: size={combinedBounds.size} lowestY={lowestPoint:F3} highestY={highestPoint:F3} knightHeightRatio={(knightHeightRatio > 0f ? knightHeightRatio.ToString("F2") : "n/a")}");
                report.AppendLine($"Rig: {rigStatus}");
                report.AppendLine($"Transforms: {transforms.Length} total, skinned renderers={skinnedRenderers.Length}, unique bones={uniqueBoneCount}, total bone refs={totalBones}");
                report.AppendLine($"Animation Clips: {(clips.Length == 0 ? "<none>" : string.Join(", ", clips.Select(clip => $"{clip.name} ({clip.length:F2}s)")))}");
                report.AppendLine($"Walk Analysis: {walkSummary.Description}");
                report.AppendLine($"Recommendation: {BuildRecommendation(walkClip, rigStatus, materialStatus, lowestPoint, knightHeightRatio, walkSummary)}");
                report.AppendLine($"Validation Controller: {ValidationControllerPath}");
                report.AppendLine($"Validation Prefab: {ValidationPrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }

            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
        }

        private static void TryRunFromMarker()
        {
            if (!File.Exists(MarkerPath))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunFromMarker;
                return;
            }

            try
            {
                ValidateGoblinAsset();
                File.Delete(MarkerPath);
            }
            catch (Exception exception)
            {
                File.WriteAllText(ReportPath, $"[GoblinAssetValidator] Failed: {exception}");
                Debug.LogError($"[GoblinAssetValidator] Validation failed: {exception}");
            }
        }

        private static bool NormalizeImporter(ModelImporter importer, List<string> appliedFixes)
        {
            bool changed = false;

            if (importer.importCameras)
            {
                importer.importCameras = false;
                appliedFixes.Add("disabled camera import");
                changed = true;
            }

            if (importer.importLights)
            {
                importer.importLights = false;
                appliedFixes.Add("disabled light import");
                changed = true;
            }

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                appliedFixes.Add("set rig type to Generic");
                changed = true;
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                appliedFixes.Add("enabled animation import");
                changed = true;
            }

            ModelImporterClipAnimation[] sourceClips = importer.defaultClipAnimations;
            if (sourceClips != null && sourceClips.Length > 0)
            {
                ModelImporterClipAnimation walkSource = sourceClips.FirstOrDefault(IsWalkClip) ?? sourceClips[0];
                string desiredName = "Walk";

                bool needsClipRewrite = importer.clipAnimations == null
                    || importer.clipAnimations.Length != 1
                    || !string.Equals(importer.clipAnimations[0].name, desiredName, StringComparison.Ordinal)
                    || importer.importCameras
                    || importer.importLights;

                if (needsClipRewrite)
                {
                    ModelImporterClipAnimation clip = walkSource;
                    clip.name = desiredName;
                    clip.loopTime = true;
                    clip.loopPose = true;
                    importer.clipAnimations = new[] { clip };
                    appliedFixes.Add($"made clip import explicit as '{desiredName}'");
                    changed = true;
                }
            }

            return changed;
        }

        private static bool IsWalkClip(ModelImporterClipAnimation clip)
        {
            return string.Equals(clip.name, "Walk", StringComparison.OrdinalIgnoreCase)
                || clip.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0
                || clip.takeName.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeMaterials(Renderer[] renderers, Material[] importedMaterials)
        {
            if (renderers.Length == 0)
                return "no renderers found";

            int nullSlots = 0;
            int assignedSlots = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] sharedMaterials = renderers[i].sharedMaterials;
                for (int j = 0; j < sharedMaterials.Length; j++)
                {
                    if (sharedMaterials[j] == null)
                        nullSlots++;
                    else
                        assignedSlots++;
                }
            }

            if (nullSlots > 0)
                return $"{assignedSlots} assigned slot(s), {nullSlots} missing slot(s), imported materials={importedMaterials.Length}";

            return $"{assignedSlots} assigned slot(s), imported materials={importedMaterials.Length}";
        }

        private static string DescribeRig(int skinnedRendererCount, int uniqueBoneCount, bool hasNullBoneReference, ModelImporterAnimationType animationType, Animator animator, int avatarCount)
        {
            if (skinnedRendererCount == 0 || uniqueBoneCount == 0)
                return "absent or not skinned";

            if (hasNullBoneReference)
                return $"present but broken (null bone references detected, rigType={animationType}, avatarAssets={avatarCount})";

            return $"present and usable (rigType={animationType}, animator={(animator != null ? "present" : "missing on imported root")}, avatarAssets={avatarCount})";
        }

        private static Bounds CalculateCombinedBounds(Renderer[] renderers, Vector3 fallbackCenter)
        {
            if (renderers == null || renderers.Length == 0)
                return new Bounds(fallbackCenter, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static float CompareToKnight(Bounds goblinBounds)
        {
            GameObject knightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefabPath);
            if (knightPrefab == null)
                return 0f;

            var knightInstance = PrefabUtility.InstantiatePrefab(knightPrefab) as GameObject;
            if (knightInstance == null)
                return 0f;

            try
            {
                knightInstance.transform.position = Vector3.zero;
                Renderer[] knightRenderers = knightInstance.GetComponentsInChildren<Renderer>(true);
                Bounds knightBounds = CalculateCombinedBounds(knightRenderers, Vector3.zero);
                if (Mathf.Abs(knightBounds.size.y) < 0.0001f)
                    return 0f;

                return goblinBounds.size.y / knightBounds.size.y;
            }
            finally
            {
                Object.DestroyImmediate(knightInstance);
            }
        }

        private static WalkSampleSummary SampleWalk(GameObject instance, AnimationClip walkClip)
        {
            if (walkClip == null)
                return new WalkSampleSummary("walk clip not found", false, false, 0f, 0f);

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new WalkSampleSummary($"clip '{walkClip.name}' found but renderers are missing", false, false, walkClip.length, 0f);

            var sampledBoundsMagnitudes = new List<float>();
            Vector3 rootStart = instance.transform.localPosition;
            Vector3 rootEnd = rootStart;
            bool hasInvalidBounds = false;
            bool hasScaleCurves = AnimationUtility.GetCurveBindings(walkClip)
                .Any(binding => binding.propertyName.Contains("Scale", StringComparison.OrdinalIgnoreCase));

            AnimationMode.StartAnimationMode();
            try
            {
                int samples = Mathf.Max(6, Mathf.CeilToInt(walkClip.length * 10f));
                for (int i = 0; i <= samples; i++)
                {
                    float t = Mathf.Lerp(0f, walkClip.length, i / (float)samples);
                    AnimationMode.SampleAnimationClip(instance, walkClip, t);
                    Bounds bounds = CalculateCombinedBounds(renderers, instance.transform.position);
                    if (float.IsNaN(bounds.size.x) || float.IsNaN(bounds.size.y) || float.IsNaN(bounds.size.z))
                    {
                        hasInvalidBounds = true;
                        continue;
                    }

                    sampledBoundsMagnitudes.Add(bounds.size.magnitude);
                    if (i == samples)
                        rootEnd = instance.transform.localPosition;
                }
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }

            float rootDelta = Vector3.Distance(rootStart, rootEnd);
            float minMagnitude = sampledBoundsMagnitudes.Count > 0 ? sampledBoundsMagnitudes.Min() : 0f;
            float maxMagnitude = sampledBoundsMagnitudes.Count > 0 ? sampledBoundsMagnitudes.Max() : 0f;
            float boundsRatio = minMagnitude > 0.0001f ? maxMagnitude / minMagnitude : 0f;
            bool stableDeformation = !hasInvalidBounds && (boundsRatio == 0f || boundsRatio < 1.75f) && !hasScaleCurves;

            string description = $"clip='{walkClip.name}' duration={walkClip.length:F2}s stableDeformation={stableDeformation} rootDelta={rootDelta:F3} scaleCurves={hasScaleCurves} boundsRatio={(boundsRatio > 0f ? boundsRatio.ToString("F2") : "n/a")} safestMode=in-place with root motion disabled";
            return new WalkSampleSummary(description, stableDeformation, rootDelta > 0.01f, walkClip.length, rootDelta);
        }

        private static string BuildRecommendation(AnimationClip walkClip, string rigStatus, string materialStatus, float lowestPoint, float knightHeightRatio, WalkSampleSummary walkSummary)
        {
            if (walkClip == null)
                return "blocked for alpha movement integration until a valid walk clip is imported";

            if (!rigStatus.Contains("usable", StringComparison.OrdinalIgnoreCase))
                return "blocked for alpha until rig import is cleaned up";

            if (materialStatus.Contains("missing slot", StringComparison.OrdinalIgnoreCase))
                return "usable only after assigning missing materials; keep movement code-driven and root motion disabled";

            if (!walkSummary.StableDeformation)
                return "usable only with caveats; walk exists but needs visual review before alpha integration";

            if (lowestPoint > 0.15f || lowestPoint < -0.15f)
                return "usable with caveats; walk looks programmatically stable, but grounding/pivot likely needs prefab offset before alpha";

            if (knightHeightRatio > 1.6f || knightHeightRatio < 0.45f)
                return "usable with caveats; walk looks stable, but board scale likely needs prefab adjustment before alpha";

            return "ready for alpha movement integration as an in-place animated unit with code-driven movement and root motion disabled";
        }

        private static void EnsureValidationAssets(GameObject modelPrefab, AnimationClip walkClip)
        {
            if (walkClip == null)
                return;

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ValidationControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ValidationControllerPath);

            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState walkState = FindOrCreateState(stateMachine, "Walk", new Vector3(320f, 90f, 0f));
            walkState.motion = walkClip;
            stateMachine.defaultState = walkState;
            EditorUtility.SetDirty(controller);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ValidationPrefabPath) != null)
                AssetDatabase.DeleteAsset(ValidationPrefabPath);

            var root = new GameObject("GoblinValidationPrefab");
            try
            {
                GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
                if (modelInstance == null)
                    throw new InvalidOperationException($"[GoblinAssetValidator] Failed to instantiate model prefab '{ModelPath}' for validation prefab.");

                modelInstance.transform.SetParent(root.transform, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                modelInstance.transform.localScale = Vector3.one;

                Animator animator = modelInstance.GetComponent<Animator>();
                if (animator == null)
                    animator = modelInstance.AddComponent<Animator>();

                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = controller;

                UnitAnimationController unitAnimationController = root.GetComponent<UnitAnimationController>();
                if (unitAnimationController == null)
                    unitAnimationController = root.AddComponent<UnitAnimationController>();

                SerializedObject serializedController = new SerializedObject(unitAnimationController);
                serializedController.FindProperty("animator").objectReferenceValue = animator;
                serializedController.FindProperty("isMovingBoolParam").stringValue = "IsMoving";
                serializedController.FindProperty("speedFloatParam").stringValue = "Speed";
                serializedController.FindProperty("walkStateName").stringValue = "Walk";
                serializedController.FindProperty("idleStateName").stringValue = "Idle";
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                if (!PrefabUtility.SaveAsPrefabAsset(root, ValidationPrefabPath, out bool success) || !success)
                    throw new InvalidOperationException($"[GoblinAssetValidator] Failed to save validation prefab '{ValidationPrefabPath}'.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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

        private readonly struct WalkSampleSummary
        {
            public WalkSampleSummary(string description, bool stableDeformation, bool hasRootMotion, float duration, float rootDelta)
            {
                Description = description;
                StableDeformation = stableDeformation;
                HasRootMotion = hasRootMotion;
                Duration = duration;
                RootDelta = rootDelta;
            }

            public string Description { get; }
            public bool StableDeformation { get; }
            public bool HasRootMotion { get; }
            public float Duration { get; }
            public float RootDelta { get; }
        }
    }
}
