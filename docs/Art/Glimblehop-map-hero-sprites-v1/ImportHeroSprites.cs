using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ImportHeroSprites
{
    public static object Import()
    {
        const string path = "Assets/_Project/Resources/Map/HeroSprites/goblin-atlas.png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        if (provider == null) throw new Exception("No sprite data provider");
        provider.InitSpriteEditorDataProvider();
        var capabilities = provider.GetDataProvider<ISpriteFrameEditCapability>();
        if (capabilities == null || !capabilities.GetEditCapability().HasCapability(EEditCapability.CreateAndDeleteSprite))
            throw new Exception("Importer does not support sprite slicing");
        var existing = provider.GetSpriteRects().ToDictionary(r => r.name);
        var rects = new SpriteRect[30];
        for (int i = 0; i < rects.Length; i++)
        {
            string name = i < 20 ? "run_" + i.ToString("D2") : "idle_" + (i - 20).ToString("D2");
            rects[i] = new SpriteRect
            {
                name = name, rect = new Rect(i % 8 * 256, 1024 - (i / 8 + 1) * 256, 256, 256),
                alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, .0625f),
                spriteID = existing.TryGetValue(name, out var old) ? old.spriteID : GUID.Generate()
            };
        }
        provider.SetSpriteRects(rects);
        var ids = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (ids == null) throw new Exception("Missing name/file ID provider");
        ids.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return new { sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count(), path };
    }
}
