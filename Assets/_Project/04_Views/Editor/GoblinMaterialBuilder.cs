using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Diceforge.Editor
{
    [InitializeOnLoad]
    public static class GoblinMaterialBuilder
    {
        private const string ModelPath = "Assets/_Project/07_3D_Models/goblin.fbx";
        private const string TexturePath = "Assets/_Project/07_3D_Models/texture_pbr_goblin.png";
        private const string MaterialFolder = "Assets/_Project/07_3D_Models/Materials";
        private const string MaterialPath = "Assets/_Project/07_3D_Models/Materials/Goblin.mat";
        private const string MarkerPath = "Temp/goblin_material_setup.marker";

        static GoblinMaterialBuilder()
        {
            // Marker-driven setup keeps the material remap repeatable without wiring goblin into live battle data.
            EditorApplication.delayCall += TryRunFromMarker;
        }

        [MenuItem("Tools/Goblin/Setup Material")]
        public static void SetupMaterial()
        {
            EnsureFolder(MaterialFolder);

            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(TexturePath);
            if (texture == null)
                throw new InvalidOperationException($"[GoblinMaterialBuilder] Texture not found at '{TexturePath}'.");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("[GoblinMaterialBuilder] Shader 'Universal Render Pipeline/Lit' not found.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "Goblin"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);

            EditorUtility.SetDirty(material);

            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException($"[GoblinMaterialBuilder] ModelImporter not found at '{ModelPath}'.");

            Material importedMaterial = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<Material>()
                .FirstOrDefault();
            if (importedMaterial == null)
                throw new InvalidOperationException($"[GoblinMaterialBuilder] No imported material subasset found in '{ModelPath}'.");

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), importedMaterial.name), material);
            importer.SaveAndReimport();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[GoblinMaterialBuilder] Created and remapped material '{MaterialPath}' using source material '{importedMaterial.name}'.");
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
                SetupMaterial();
                File.Delete(MarkerPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GoblinMaterialBuilder] Auto-setup failed: {exception}");
            }
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
    }
}
