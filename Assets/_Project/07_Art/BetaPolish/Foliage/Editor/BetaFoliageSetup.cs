using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Diceforge.View.Editor
{
    /// <summary>Imports the standalone beta foliage kit. Does not modify a board, scene, or existing material.</summary>
    public static class BetaFoliageSetup
    {
        public const string Folder = "Assets/_Project/07_Art/BetaPolish/Foliage/";
        private const string LeafTexture = "Assets/_Project/07_Art/FoliageStudy/LeafAlbedo.png";
        private static readonly string[] Names = { "RoundleafShrub_A", "RoundleafShrub_B", "ShoreBroadleaf_A" };
        private static readonly int[] Lod0Triangles = { 3384, 3008, 2632 };
        private static readonly int[] Lod1Triangles = { 900, 796, 704 };

        [MenuItem("Diceforge/Woodland/Import beta foliage kit")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before importing the foliage kit.");

            var shader = Shader.Find("Diceforge/Diorama/Soft Foliage");
            if (shader == null) throw new InvalidOperationException("Soft Foliage shader is missing.");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LeafTexture);
            if (texture == null) throw new FileNotFoundException("Leaf study albedo is missing.", LeafTexture);

            EnsureFolder(Folder + "Materials");
            var leaves = LeafMaterial("RoundedLeaves", shader, texture, new Color(.82f, .94f, .78f), .024f);
            var shoreLeaves = LeafMaterial("ShoreLeaves", shader, texture, new Color(.78f, .93f, .82f), .029f);
            var bark = BarkMaterial();
            for (var i = 0; i < Names.Length; i++)
            {
                var name = Names[i];
                var path = Folder + name + ".fbx";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new FileNotFoundException("Foliage FBX is missing.", path);
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importVisibility = false;
                importer.importBlendShapes = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.isReadable = false;
                importer.addCollider = false;
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.preserveHierarchy = true;
                importer.SaveAndReimport();
                BuildPrefab(name, name.StartsWith("Shore", StringComparison.Ordinal) ? shoreLeaves : leaves,
                    bark, Lod0Triangles[i], Lod1Triangles[i]);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Beta foliage kit: 3 prefabs, vertex wind masks, LOD0 2632-3384 triangles / LOD1 704-900 triangles.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Material LeafMaterial(string name, Shader shader, Texture texture, Color tint, float wind)
        {
            var path = Folder + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.enableInstancing = true;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_WindStrength", wind);
            material.SetFloat("_AutoHeight", 0);
            material.SetFloat("_Translucency", .12f);
            material.SetFloat("_Variation", .28f);
            material.SetFloat("_Saturation", .77f);
            material.SetFloat("_ToonStrength", .20f);
            material.SetFloat("_Wrap", .12f);
            material.SetFloat("_AmbientStrength", .8f);
            material.SetFloat("_Sheen", .025f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BarkMaterial()
        {
            var path = Folder + "Materials/RoundedBark.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/07_Art/LivingForest/Materials/ForestBark.mat");
            if (source == null) throw new InvalidOperationException("The source ForestBark material is missing.");
            material = new Material(source) { name = "RoundedBark", enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void BuildPrefab(string name, Material leaves, Material bark, int lod0Triangles, int lod1Triangles)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".fbx");
            if (model == null) throw new InvalidOperationException("Cannot load imported model " + name);
            var root = new GameObject(name);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var importedLod in instance.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(importedLod);
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length != 2) throw new InvalidOperationException(name + " must contain exactly two LOD meshes.");
                foreach (var renderer in renderers)
                {
                    renderer.gameObject.SetActive(true);
                    renderer.enabled = true;
                    renderer.sharedMaterials = new[] { leaves, bark };
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                var lod0 = renderers.Single(r => r.name.EndsWith("_LOD0", StringComparison.Ordinal));
                var lod1 = renderers.Single(r => r.name.EndsWith("_LOD1", StringComparison.Ordinal));
                ValidateMesh(lod0, lod0Triangles);
                ValidateMesh(lod1, lod1Triangles);
                var lodGroup = root.AddComponent<LODGroup>();
                // Opaque shader has no dither fade; select one mesh at a time.
                lodGroup.fadeMode = LODFadeMode.None;
                lodGroup.SetLODs(new[] { new LOD(.06f, new Renderer[] { lod0 }), new LOD(.006f, new Renderer[] { lod1 }) });
                lodGroup.RecalculateBounds();
                // Retain visible leaves while vertex wind expands their static bounds.
                lodGroup.size += .07f;
                PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void ValidateMesh(MeshRenderer renderer, int triangles)
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null || mesh.subMeshCount != 2 || !mesh.HasVertexAttribute(VertexAttribute.Color)
                || !mesh.HasVertexAttribute(VertexAttribute.Normal) || !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                throw new InvalidOperationException(renderer.name + " must have UVs, normals, wind colors, and two material slots.");
            var count = 0L;
            for (var i = 0; i < mesh.subMeshCount; i++) count += mesh.GetIndexCount(i) / 3;
            if (count != triangles) throw new InvalidOperationException(renderer.name + " triangle count: " + count + ", expected " + triangles);
        }
    }
}
