// Run this statement block through Unity CLI eval (skill sprite-editor).
const string path = "Assets/_Project/07_Art/DemoRCCharacters/Portraits/neutral-atlas-v3.png";
UnityEditor.AssetDatabase.ImportAsset(path);
var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
if (importer == null) throw new System.InvalidOperationException("Portrait atlas was not imported.");
importer.textureType = UnityEditor.TextureImporterType.Sprite;
importer.spriteImportMode = UnityEditor.SpriteImportMode.Multiple;
importer.alphaIsTransparency = true;
importer.mipmapEnabled = false;
importer.npotScale = UnityEditor.TextureImporterNPOTScale.None;
importer.maxTextureSize = 2048;
importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
importer.filterMode = UnityEngine.FilterMode.Bilinear;
importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
importer.SaveAndReimport();
var factories = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
factories.Init();
var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
if (provider == null) throw new System.InvalidOperationException("Sprite metadata provider is unavailable.");
provider.InitSpriteEditorDataProvider();
var editing = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteFrameEditCapability>();
if (editing == null) throw new System.InvalidOperationException("Sprite edit capability is unavailable.");
var capabilities = editing.GetEditCapability();
foreach (var required in new[] {
    UnityEditor.U2D.Sprites.EEditCapability.CreateAndDeleteSprite,
    UnityEditor.U2D.Sprites.EEditCapability.EditSpriteName,
    UnityEditor.U2D.Sprites.EEditCapability.EditSpriteRect,
    UnityEditor.U2D.Sprites.EEditCapability.EditPivot })
    if (!capabilities.HasCapability(required))
        throw new System.InvalidOperationException("Atlas importer does not support " + required + ".");
var textureData = provider.GetDataProvider<UnityEditor.U2D.Sprites.ITextureDataProvider>();
if (textureData == null) throw new System.InvalidOperationException("Source texture dimensions are unavailable.");
textureData.GetTextureActualWidthAndHeight(out int width, out int height);
if (width % 3 != 0 || height % 4 != 0)
    throw new System.InvalidOperationException("The source must have exactly three columns and four rows.");
var ids = new[] { "tish", "luma", "bum", "koren", "pip", "fika", "mira", "una", "shal", "kloch", "ryzh", "bark" };
var old = provider.GetSpriteRects();
var rects = new UnityEditor.SpriteRect[ids.Length];
int cellWidth = width / 3;
int cellHeight = height / 4;
for (int index = 0; index < ids.Length; index++)
{
    string name = ids[index] + "_neutral";
    var previous = System.Array.Find(old, r => r.name == name);
    rects[index] = new UnityEditor.SpriteRect {
        name = name,
        spriteID = previous == null ? UnityEngine.GUID.Generate() : previous.spriteID,
        rect = new UnityEngine.Rect(index % 3 * cellWidth, (3 - index / 3) * cellHeight, cellWidth, cellHeight),
        alignment = UnityEngine.SpriteAlignment.Center,
        pivot = new UnityEngine.Vector2(.5f, .5f)
    };
}
var nameIds = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
if (nameIds == null) throw new System.InvalidOperationException("Stable sprite references are unavailable.");
nameIds.SetNameFileIdPairs(rects.Select(r => new UnityEditor.SpriteNameFileIdPair(r.name, r.spriteID)).ToArray());
provider.SetSpriteRects(rects);
provider.Apply();
importer.SaveAndReimport();
var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<UnityEngine.Sprite>().ToArray();
var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Diceforge.Dialogue.DemoNarrativeCatalog>(
    "Assets/_Project/Resources/DemoRC/Narrative.asset");
if (catalog == null || catalog.speakers.Length != 12 || sprites.Length != 12)
    throw new System.InvalidOperationException("Expected twelve authored speakers and twelve imported portraits.");
foreach (var speaker in catalog.speakers)
    speaker.neutralPortrait = sprites.Single(s => s.name == speaker.id + "_neutral");
UnityEditor.EditorUtility.SetDirty(catalog);
UnityEditor.AssetDatabase.SaveAssets();
return new { width, height, sprites = sprites.Select(s => new { s.name, rect = s.rect.ToString() }).ToArray() };
