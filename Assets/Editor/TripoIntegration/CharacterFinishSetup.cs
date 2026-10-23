using System;
using System.IO;
using Diceforge.View;
using UnityEditor;
using UnityEngine;

namespace Diceforge.Editor
{
    /// <summary>Texture and material finish on the existing cast; no model or rig changes.</summary>
    public static class CharacterFinishSetup
    {
        private const string Art = "Assets/_Project/07_Art/Characters/Finish/";

        [MenuItem("Diceforge/Characters/Apply character fabric finish")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Finish Play Mode or player build before authoring character materials.");

            foreach (string name in new[] { "Tish", "Luma", "Bum", "Scratchy" })
            {
                var profile = Resources.Load<CharacterPresentationProfile>("Characters/Presentation/" + name);
                if (profile == null || profile.bodyMaterial == null || profile.eyeMaterial == null)
                    throw new InvalidOperationException("Missing production presentation profile: " + name);

                var body = profile.bodyMaterial;
                var eyes = profile.eyeMaterial;
                Texture2D colour;
                if (name == "Scratchy")
                {
                    colour = ImportTexture(Art + "Scratchy_Flax_BaseColor.png", false, true, 2048);
                    var surface = ImportTexture(Art + "Scratchy_Surface.png", false, false, 1024);
                    var weave = ImportTexture(Art + "Scratchy_Weave_Normal.png", true, false, 1024);
                    body.SetTexture("_MetallicGlossMap", surface);
                    body.EnableKeyword("_METALLICSPECGLOSSMAP");
                    body.SetFloat("_SmoothnessTextureChannel", 0);
                    // URP multiplies map alpha by this scale: jute .095,
                    // yarn .110, worn boots .175, rough brass .190.
                    body.SetFloat("_Smoothness", .19f);
                    body.SetTexture("_BumpMap", weave);
                    body.SetFloat("_BumpScale", .40f);
                }
                else
                {
                    string path = AssetDatabase.GetAssetPath(body.GetTexture("_BaseMap"));
                    if (string.IsNullOrEmpty(path))
                        throw new InvalidOperationException("Missing authored colour atlas: " + name);
                    colour = ImportTexture(path, false, true, 2048);
                    body.SetTexture("_BumpMap", ImportTexture(Art + name + "_Cloth_Normal.png", true, false, 1024));
                    body.SetFloat("_BumpScale", .20f);
                    body.SetFloat("_Smoothness", name == "Bum" ? .13f : .16f);
                }

                body.SetTexture("_BaseMap", colour);
                body.SetTexture("_MainTex", colour);
                body.SetColor("_BaseColor", Color.white);
                body.SetFloat("_Metallic", 0);
                body.SetFloat("_SpecularHighlights", 1);
                body.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                body.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(body);

                // Preserve the existing separate eye submesh and shared glossy
                // response. Fabric and metal maps must never reach the eyes.
                eyes.SetTexture("_BaseMap", colour);
                eyes.SetTexture("_MainTex", colour);
                eyes.SetTexture("_BumpMap", null);
                eyes.SetTexture("_MetallicGlossMap", null);
                eyes.DisableKeyword("_NORMALMAP");
                eyes.DisableKeyword("_METALLICSPECGLOSSMAP");
                eyes.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                eyes.SetColor("_BaseColor", Color.white);
                eyes.SetFloat("_Smoothness", .52f);
                eyes.SetFloat("_Metallic", 0);
                eyes.SetFloat("_SpecularHighlights", 1);
                EditorUtility.SetDirty(eyes);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Character finish] Flax/rust jute palette, local brass bell and restrained cloth relief applied to the existing cast.");
        }

        private static Texture2D ImportTexture(string path, bool normal, bool colour, int maximumSize)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("Missing character finish texture: " + path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Cannot import character finish texture: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = colour;
            importer.convertToNormalmap = false;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = maximumSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 80;
            importer.isReadable = false;
            importer.alphaSource = normal || colour ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
