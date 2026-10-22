using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Diceforge.View.Editor
{
    /// <summary>Rebuilds the submerged shore independently of the gameplay landings.</summary>
    public static class LakeWaterSetup
    {
        public const string Folder = "Assets/_Project/07_Art/LakeWater/";
        public const string Live = "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab";
        public const string Output = "docs/Art/MapDesign/Lake-v01/";
        public const string Before = Folder + "FirstTrail8_BeforeLake.prefab";

        [MenuItem("Diceforge/Woodland/Improve lake water")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Finish Play Mode/player build before authoring water.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            PreserveBefore();
            ConfigureTexture("LakeBedColor.png", false);
            ConfigureTexture("LakeCaustics.png", true);
            var bed = AssetDatabase.LoadAssetAtPath<Material>(Folder + "LakeBed.mat");
            if (bed == null)
            {
                bed = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(bed, Folder + "LakeBed.mat");
            }
            bed.SetColor("_BaseColor", Color.white);
            bed.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "LakeBedColor.png"));
            bed.SetFloat("_Smoothness", .12f); bed.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(bed);
            var root = PrefabUtility.LoadPrefabContents(Live);
            try
            {
                var board = root.GetComponent<DioramaBoard>();
                string layout = EditorJsonUtility.ToJson(board.layout);
                var landings = root.GetComponentsInChildren<DioramaCell>(true);
                var anchors = landings.Select(c => c.transform.localPosition).ToArray();
                ApplyShore(root, bed);
                if (layout != EditorJsonUtility.ToJson(board.layout) ||
                    landings.Where((cell, i) => cell.transform.localPosition != anchors[i]).Any())
                    throw new InvalidOperationException("Lake authoring moved a landing.");
                PrefabUtility.SaveAsPrefabAsset(root, Live);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var lake = AssetDatabase.LoadAssetAtPath<Material>(LivingForestSetup.Folder + "Materials/ForestLake.mat");
            ConfigureLake(lake);
            AssetDatabase.SaveAssets();
        }

        private static void PreserveBefore()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Before) != null) return;
            if (!AssetDatabase.CopyAsset(Live, Before)) throw new InvalidOperationException("Cannot preserve lake comparison.");
            var root = PrefabUtility.LoadPrefabContents(Before);
            try
            {
                var copies = new Dictionary<Material, Material>();
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var source = slots[i];
                        if (source == null || source.shader.name != "Diceforge/Living Forest Water") continue;
                        if (!copies.TryGetValue(source, out var material))
                        {
                            material = new Material(source) { shader = Shader.Find("Diceforge/Review/Before Lake") };
                            AssetDatabase.CreateAsset(material, Folder + "Before_" + source.name + ".mat");
                            copies.Add(source, material);
                        }
                        slots[i] = material;
                    }
                    renderer.sharedMaterials = slots;
                }
                PrefabUtility.SaveAsPrefabAsset(root, Before);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void ConfigureLake(Material lake)
        {
            lake.SetColor("_ShallowColor", new Color(.30f, .85f, .83f));
            lake.SetColor("_DeepColor", new Color(.025f, .38f, .46f));
            lake.SetColor("_FoamColor", new Color(.78f, .87f, .80f));
            lake.SetFloat("_Density", 2.1f); lake.SetFloat("_WaveStrength", .075f);
            lake.SetFloat("_FlowSpeed", .32f); lake.SetFloat("_Roughness", .24f);
            lake.SetFloat("_ReflectionStrength", .64f); lake.SetFloat("_Refraction", .012f);
            lake.SetFloat("_OpenWaterDepth", 4); lake.SetFloat("_LakeRippleScale", 1.4f);
            lake.SetFloat("_LakeReflectionBlur", 1); lake.SetFloat("_LakeReflectionFloor", .23f);
            lake.SetFloat("_LakeFoamAmount", .12f);
            if (lake.HasProperty("_CausticMap"))
                lake.SetTexture("_CausticMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "LakeCaustics.png"));
            if (lake.HasProperty("_CausticStrength")) lake.SetFloat("_CausticStrength", .11f);
            EditorUtility.SetDirty(lake);
        }

        // Called by the full forest builder too, so rebuilding the kit retains the repaired shore.
        public static void ApplyShore(GameObject root, Material bed = null)
        {
            bed = bed != null ? bed : AssetDatabase.LoadAssetAtPath<Material>(Folder + "LakeBed.mat");
            if (bed == null) return;
            var filter = root.transform.Find("Living forest/AsymmetricShelf_A").GetComponentInChildren<MeshFilter>();
            var renderer = filter.GetComponent<MeshRenderer>();
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "LakeBed.asset");
            if (mesh == null) { mesh = new Mesh { name = "Asymmetric shallow lake bed" }; AssetDatabase.CreateAsset(mesh, Folder + "LakeBed.asset"); }
            mesh.Clear();
            const int count = 160;
            float[] radii = { .84f, 1.02f, 1.15f, 1.38f, 1.7f, 3, 14 };
            float[] heights = { -.84f, -.91f, -1.02f, -1.34f, -2.05f, -3.7f, -5 };
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int ring = 0; ring < radii.Length; ring++)
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                float irregular = 1 + .11f * Mathf.Sin(a * 3 + .7f) + .055f * Mathf.Sin(a * 7 + 1.9f) + .045f * Mathf.Cos(a * 5 - .3f);
                float x = 3.75f * radii[ring] * irregular * Mathf.Cos(a) + .25f * Mathf.Sin(a * 2);
                float z = 2.25f * radii[ring] * (1 + .08f * Mathf.Cos(a * 4)) * Mathf.Sin(a) + .18f * Mathf.Cos(a * 3);
                float bar = .13f * Mathf.Exp(-Mathf.Pow((a - 5.2f) / .42f, 2));
                float y = Mathf.Min(-.82f, heights[ring] + bar * (ring < 4 ? 1 : .3f) + .018f * Mathf.Sin(a * 11));
                vertices.Add(filter.transform.InverseTransformPoint(root.transform.TransformPoint(new Vector3(x, y, z))));
                uv.Add(new Vector2(x, z) * .68f);
                if (ring == radii.Length - 1) continue;
                int current = ring * count + i, next = ring * count + (i + 1) % count;
                // Tangent then outward is +Y in Unity's XZ plane.
                triangles.Add(current); triangles.Add(next); triangles.Add(next + count);
                triangles.Add(current); triangles.Add(next + count); triangles.Add(current + count);
            }
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            // FBX wrapper has a -90-degree axis conversion. Winding is authored in world space.
            filter.sharedMesh = mesh; renderer.sharedMaterial = bed;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            EditorUtility.SetDirty(mesh);
        }

        private static void ConfigureTexture(string name, bool linear)
        {
            var importer = AssetImporter.GetAtPath(Folder + name) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing lake texture " + name);
            importer.sRGBTexture = !linear; importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true; importer.maxTextureSize = linear ? 256 : 1024;
            importer.textureCompression = TextureImporterCompression.Compressed; importer.SaveAndReimport();
        }

        public static void Capture()
        {
            Directory.CreateDirectory(Output);
            LivingForestReview.Capture(Output + "before.png", 3, false, Before, false, false, true);
            LivingForestReview.Capture(Output + "after.png", 3, false, Live, false, false, true);
            LivingForestReview.Capture(Output + "overview.png", 3);
        }
    }
}
