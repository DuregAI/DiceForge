using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Diceforge.View.Editor
{
    /// <summary>Isolated art experiment. Never changes the shipped board or gameplay anchors.</summary>
    public static class FoliageStudySetup
    {
        public const string Folder = "Assets/_Project/07_Art/FoliageStudy/";
        public const string Board = Folder + "FirstTrail8_FoliageStudy.prefab";
        public const string Original = "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab";

        [MenuItem("Diceforge/Woodland/Build foliage study")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var texture = (TextureImporter)AssetImporter.GetAtPath(Folder + "LeafAlbedo.png");
            texture.maxTextureSize = 1024; texture.mipmapEnabled = true;
            texture.wrapMode = TextureWrapMode.Clamp; texture.anisoLevel = 4; texture.SaveAndReimport();
            var model = (ModelImporter)AssetImporter.GetAtPath(Folder + "RoundedShrub_A.fbx");
            model.importAnimation = false; model.isReadable = false; model.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "RoundedLeaves.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Diceforge/Study Rounded Foliage"));
                AssetDatabase.CreateAsset(material, Folder + "RoundedLeaves.mat");
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "LeafAlbedo.png"));
            material.SetColor("_BaseColor", new Color(.88f, .95f, .72f));
            material.SetFloat("_WindStrength", .025f); material.SetFloat("_AutoHeight", 0);
            material.SetFloat("_Translucency", .22f); material.SetFloat("_Saturation", .82f);
            EditorUtility.SetDirty(material);
            var wrapper = new GameObject("Rounded woodland shrub");
            try
            {
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RoundedShrub_A.fbx"), wrapper.transform);
                foreach (var renderer in imported.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m.name == "StudyLeaves" ? material : AssetDatabase.LoadAssetAtPath<Material>(LivingForestSetup.Folder + "Materials/ForestBark.mat")).ToArray();
                PrefabUtility.SaveAsPrefabAsset(wrapper, Folder + "RoundedShrub_A.prefab");
            }
            finally { Object.DestroyImmediate(wrapper); }

            var root = PrefabUtility.LoadPrefabContents(Original);
            try
            {
                var environment = root.transform.Find("Living forest");
                var center = new Vector3(-.7f, .03f, -1.65f);
                // Clear only the study patch; leave rocks, flowers and all route objects intact.
                foreach (var child in environment.Cast<Transform>().ToArray())
                {
                    var offset = child.localPosition - center; offset.y = 0;
                    if (offset.magnitude < .95f && (child.name.Contains("Shrub") || child.name.Contains("GrassTuft")))
                        Object.DestroyImmediate(child.gameObject);
                }
                Add(environment, center, .95f, 28);
                Add(environment, center + new Vector3(-.53f, 0, .12f), .60f, 167);
                Add(environment, center + new Vector3(.45f, 0, -.10f), .50f, 291);
                PrefabUtility.SaveAsPrefabAsset(root, Board);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void Add(Transform parent, Vector3 position, float scale, float yaw)
        {
            var shrub = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RoundedShrub_A.prefab"), parent);
            shrub.transform.localPosition = position; shrub.transform.localScale = Vector3.one * scale;
            shrub.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        public static void CapturePair()
        {
            const string output = "docs/Art/MapDesign/FoliageStudy-v01/";
            LivingForestReview.Capture(output + "before.png", 3, false, Original, true);
            LivingForestReview.Capture(output + "after.png", 3, false, Board, true);
            LivingForestReview.Capture(output + "after-wind.png", 5, false, Board, true);
        }
    }
}
