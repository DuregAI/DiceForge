using UnityEditor;
using UnityEngine;

public static class ImportFoliage
{
    public static object Import()
    {
        const string path = "Assets/_Project/Resources/Map/Foliage/woodland-shrub.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var texture = Resources.Load<Texture2D>("Map/Foliage/woodland-shrub");
        return new { loaded = texture != null, width = texture.width, height = texture.height, alpha = importer.DoesSourceTextureHaveAlpha() };
    }
}
