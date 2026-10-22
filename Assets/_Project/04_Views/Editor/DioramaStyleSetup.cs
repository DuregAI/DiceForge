using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Diceforge.View.Editor
{
    /// <summary>Reproducible, isolated lighting/material A/B. Does not overwrite the live board.</summary>
    public static class DioramaStyleSetup
    {
        public const string Folder = "Assets/_Project/07_Art/DioramaStyle/";
        public const string Board = Folder + "FirstTrail8_DioramaStyle.prefab";
        public const string Output = "docs/Art/MapDesign/DioramaStyle-v01/";

        [MenuItem("Diceforge/Woodland/Build soft diorama study")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Finish Play Mode/player build before authoring the study.");
            var root = PrefabUtility.LoadPrefabContents(FoliageStudySetup.Board);
            try
            {
                var stage = root.GetComponent<WoodlandHeroStage>();
                var board = root.GetComponent<DioramaBoard>();
                var pipeline = Copy(stage.pipelineOverride, "DioramaPipeline.asset");
                var pipelineData = new SerializedObject(pipeline);
                var renderers = pipelineData.FindProperty("m_RendererDataList");
                var renderer = Copy((UniversalRendererData)renderers.GetArrayElementAtIndex(0).objectReferenceValue, "DioramaRenderer.asset");
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                pipelineData.FindProperty("m_ShadowDistance").floatValue = 38;
                pipelineData.FindProperty("m_ShadowCascadeCount").intValue = 2;
                pipelineData.FindProperty("m_Cascade2Split").floatValue = .65f;
                pipelineData.FindProperty("m_SoftShadowQuality").intValue = 2;
                pipelineData.ApplyModifiedPropertiesWithoutUndo();
                pipeline.supportsHDR = true;
                foreach (var ao in renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>())
                {
                    var data = new SerializedObject(ao); var settings = data.FindProperty("m_Settings");
                    settings.FindPropertyRelative("Intensity").floatValue = .38f;
                    settings.FindPropertyRelative("Radius").floatValue = .12f;
                    data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(ao);
                }
                renderer.SetDirty(); EditorUtility.SetDirty(renderer); EditorUtility.SetDirty(pipeline);
                stage.pipelineOverride = pipeline; board.desktopPipeline = pipeline;
                stage.sunIntensity = 1.9f; stage.fillIntensity = .70f;
                stage.sunEuler = new Vector3(50, 75, 0); stage.fillEuler = new Vector3(30, -35, 0);
                stage.sunColor = new Color(1, .93f, .80f);
                stage.fillColor = new Color(.72f, .82f, 1);
                stage.ambientProbeColor = new Color(.44f, .47f, .48f);
                stage.ambientGroundColor = new Color(.30f, .25f, .18f);
                stage.sunShadowStrength = .80f;
                stage.sunShadowBias = .025f; stage.sunShadowNormalBias = .10f;
                var grade = Copy(stage.grade, "DioramaGrade.asset");
                grade.TryGet<ColorAdjustments>(out var color);
                color.postExposure.Override(.18f); color.contrast.Override(3); color.saturation.Override(-2);
                EditorUtility.SetDirty(color); EditorUtility.SetDirty(grade); stage.grade = grade;

                var materials = new Dictionary<Material, Material>();
                foreach (var mesh in root.GetComponentsInChildren<Renderer>(true))
                {
                    var array = mesh.sharedMaterials;
                    for (int i = 0; i < array.Length; i++)
                    {
                        var source = array[i];
                        if (source == null || source.shader.name != "Diceforge/Living Forest Foliage" || source.name.Contains("Cloth")) continue;
                        if (!materials.TryGetValue(source, out var replacement))
                        {
                            replacement = Copy(source, "Soft_" + source.name + ".mat");
                            replacement.shader = Shader.Find("Diceforge/Diorama/Soft Foliage");
                            replacement.SetFloat("_ToonStrength", .20f);
                            replacement.SetFloat("_Wrap", .12f);
                            replacement.SetFloat("_AmbientStrength", .8f);
                            replacement.SetFloat("_Sheen", .025f);
                            replacement.SetFloat("_Translucency", .12f);
                            EditorUtility.SetDirty(replacement); materials[source] = replacement;
                        }
                        array[i] = replacement;
                    }
                    mesh.sharedMaterials = array;
                }
                // Remove only alternating needle tufts. Keep flowers, rocks, anchors and silhouettes.
                var environment = root.transform.Find("Living forest");
                int tuft = 0;
                foreach (var child in environment.Cast<Transform>().ToArray())
                    if (child.name.StartsWith("GrassTuft") && ++tuft % 2 == 0) Object.DestroyImmediate(child.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, Board);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var contour = AssetDatabase.LoadAssetAtPath<Material>(Folder + "HeroContour.mat");
            if (contour == null)
            {
                contour = new Material(Shader.Find("Diceforge/Diorama/Hero Contour"));
                AssetDatabase.CreateAsset(contour, Folder + "HeroContour.mat");
            }
            contour.SetFloat("_ContourWidthPixels", .7f); EditorUtility.SetDirty(contour);
            AssetDatabase.SaveAssets();
        }

        private static T Copy<T>(T source, string name) where T : Object
        {
            string path = Folder + name;
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (result != null) return result;
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path)) throw new InvalidOperationException("Cannot copy " + source.name);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static void Capture()
        {
            // First use of a new Forward+ pipeline can finish shader setup on the next editor tick.
            // Warm the candidate before recording the comparison frames.
            LivingForestReview.Capture(Output + "warmup.png", 3, false, Board, true);
            LivingForestReview.Capture(Output + "before.png", 3, false, FoliageStudySetup.Board, true);
            LivingForestReview.Capture(Output + "soft.png", 3, false, Board, true);
            LivingForestReview.Capture(Output + "contour.png", 3, false, Board, true, true);
        }

        [MenuItem("Diceforge/Woodland/Apply verified soft diorama")]
        public static void ApplyToLevel()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Finish Play Mode/player build first.");
            const string live = FoliageStudySetup.Original;
            string backup = Folder + "FirstTrail8_BeforeStyle.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(backup) == null && !AssetDatabase.CopyAsset(live, backup))
                throw new InvalidOperationException("Cannot preserve original board.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Board);
            var root = PrefabUtility.LoadPrefabContents(live);
            try
            {
                EditorUtility.CopySerialized(source.GetComponent<WoodlandHeroStage>(), root.GetComponent<WoodlandHeroStage>());
                root.GetComponent<DioramaBoard>().desktopPipeline = source.GetComponent<DioramaBoard>().desktopPipeline;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == null) continue;
                        var replacement = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Soft_" + materials[i].name + ".mat");
                        if (replacement != null) materials[i] = replacement;
                    }
                    renderer.sharedMaterials = materials;
                }
                // Keep the existing object hierarchy/anchors and animation references.
                PrefabUtility.SaveAsPrefabAsset(root, live);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void AddContour(GameObject hero)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "HeroContour.mat");
            foreach (var source in hero.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var child = new GameObject("Contour comparison"); child.transform.SetParent(source.transform, false);
                var renderer = child.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source.sharedMesh; renderer.bones = source.bones; renderer.rootBone = source.rootBone;
                renderer.localBounds = source.localBounds; renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.sharedMaterials = Enumerable.Repeat(material, source.sharedMesh.subMeshCount).ToArray();
                for (int i = 0; i < source.sharedMesh.blendShapeCount; i++) renderer.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
            }
        }
    }
}
