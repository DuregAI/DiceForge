using System;
using UnityEditor;
using UnityEngine;
using Diceforge.View;

namespace Diceforge.Editor
{
    /// <summary>Changes the shared outfit atlas without touching the rig or imported FBX.</summary>
    public static class LumaOutfitSetup
    {
        private const string Folder = LumaDialogueSetup.Folder;
        public const string TexturePath = Folder + "/Luma_ForestOutfit_BaseColor_2K.png";
        public const string MaterialPath = Folder + "/Luma_ForestOutfit.mat";

        [MenuItem("Diceforge/Characters/Tripo/Apply Luma forest outfit")]
        public static void Apply()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) throw new InvalidOperationException("Import Luma's baked outfit atlas first.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Luma.mat"));
                material.name = "Luma_ForestOutfit";
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            var profile = Resources.Load<CharacterPresentationProfile>("Characters/Presentation/Luma");
            profile.bodyMaterial = material;
            EditorUtility.SetDirty(profile);
            foreach (string path in new[] {
                LumaDialogueSetup.PrefabPath,
                "Assets/_Project/Resources/Characters/LumaDialogue.prefab",
                "Assets/_Project/07_Art/DemoRCCharacters/Prefabs/Luma.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var appearance in root.GetComponentsInChildren<CharacterAppearance>(true))
                        appearance.Apply(false);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Luma outfit] Teal linen and berry scarf applied to gameplay and dialogue. Original material retained.");
        }
    }
}
