using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Diceforge.View.Editor
{
    /// <summary>Replaces only island presentation while preserving the authored gameplay geometry.</summary>
    public static class WoodlandIslandPolishBuilder
    {
        private const string ModelPath = "Assets/_Project/07_Art/FirstTrail/FirstTrailPolished.fbx";
        private const string PrefabPath = "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab";
        private const string Folder = FirstTrailArtMaterials.Folder + "Polished/";
        private const string Hero = "Assets/_Project/05_Gameplay_Data/Battle/WoodlandHero/";
        private const float GeometryTolerance = .005f;
        private static readonly Vector2[] Path =
        {
            new(-2.85f, -1.05f), new(-1.96f, -1.02f), new(-1.08f, -.80f), new(-.29f, -.36f),
            new(.37f, .24f), new(.88f, .98f), new(1.8f, 1.36f), new(2.84f, 1.38f)
        };
        private static readonly Vector2[] WoodlandPatches =
        {
            new(-2.9f, 1.05f), new(-1.45f, 1.1f), new(-.65f, 1.45f), new(2.65f, -.7f)
        };

        [MenuItem("Diceforge/Woodland/Apply polished island art")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before polishing the island.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scene edits before polishing the island.");
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new InvalidOperationException("Export FirstTrailPolished.fbx before applying the island art.");

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var board = root.GetComponent<DioramaBoard>();
                if (board == null || board.layout == null || board.landscapeRoot == null)
                    throw new InvalidOperationException("The playable island has no board or layout.");
                var oldArt = board.landscapeRoot.transform.Cast<Transform>()
                    .SingleOrDefault(child => FindTile(child.gameObject, 0) != null);
                if (oldArt == null)
                    throw new InvalidOperationException("Expected one island art child containing TrailCell_00.");
                var cells = root.GetComponentsInChildren<DioramaCell>(true);
                if (cells.Length != 8 || cells.Select(cell => cell.cellId).Distinct().Count() != 8)
                    throw new InvalidOperationException("Expected eight unique playable cells.");
                string layoutBefore = EditorJsonUtility.ToJson(board.layout);
                var geometry = cells.Select(cell => new CellSnapshot(cell)).ToArray();
                var oldBounds = Enumerable.Range(0, 8).Select(index => RequireTile(oldArt.gameObject, index).bounds).ToArray();

                var art = (GameObject)PrefabUtility.InstantiatePrefab(model, oldArt.parent);
                art.name = "FirstTrailPolished";
                art.transform.localPosition = oldArt.localPosition;
                art.transform.localRotation = oldArt.localRotation;
                art.transform.localScale = oldArt.localScale;
                for (int i = 0; i < 8; i++)
                {
                    var bounds = RequireTile(art, i).bounds;
                    if (Vector3.Distance(bounds.center, oldBounds[i].center) > GeometryTolerance ||
                        Mathf.Abs(bounds.max.y - oldBounds[i].max.y) > GeometryTolerance)
                        throw new InvalidOperationException("Polished TrailCell_" + i.ToString("00") + " moved its playable surface.");
                }

                var materials = BuildMaterials();
                foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                    {
                        if (source == null)
                            throw new InvalidOperationException("Missing source material on " + renderer.name);
                        string name = source.name.Split('.')[0];
                        if (!materials.TryGetValue(name, out var material))
                        {
                            material = CopyMaterial(name, name);
                            materials.Add(name, material);
                        }
                        return material;
                    }).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                for (int i = 0; i < 8; i++)
                    RequireTile(art, i).sharedMaterial = materials["TrialStone" + i % 2];
                foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("Waterfall left lip") || renderer.name.StartsWith("Waterfall right lip"))
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                            source.name.StartsWith("ShoreStoneLight") ? materials["IslandLimestoneLight"] :
                            source.name.StartsWith("ShoreStoneCool") ? materials["IslandLimestoneDark"] :
                            materials["IslandLimestone"]).ToArray();

                var life = root.GetComponent<FirstTrailLife>();
                if (life == null)
                    throw new InvalidOperationException("The island has no FirstTrailLife component.");
                life.pennant = art.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(item => item.name.StartsWith("Exit pennant", StringComparison.Ordinal));
                life.creek = art.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(item => item.name.StartsWith("Water creek flowing", StringComparison.Ordinal));
                if (life.pennant == null || life.creek == null)
                    throw new InvalidOperationException("The polished island must preserve the pennant and creek names.");
                var stage = root.GetComponent<WoodlandHeroStage>();
                if (stage == null)
                    throw new InvalidOperationException("The island has no WoodlandHeroStage component.");
                stage.sunIntensity = 1.8f;
                stage.fillIntensity = .5f;

                AssertGeometry(board, layoutBefore, geometry);
                Object.DestroyImmediate(oldArt.gameObject);
                AssertGeometry(board, layoutBefore, geometry);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved)
                    throw new InvalidOperationException("Unity failed to save the polished island prefab.");
                AssetDatabase.SaveAssets();
                Debug.Log("Polished island art applied; gameplay layout, cell anchors and colliders preserved.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Renderer FindTile(GameObject root, int index)
        {
            string name = "TrailCell_" + index.ToString("00");
            return root.GetComponentsInChildren<Renderer>(true)
                .FirstOrDefault(renderer => renderer.name.Split('.')[0] == name);
        }

        private static Renderer RequireTile(GameObject root, int index)
        {
            return FindTile(root, index) ?? throw new InvalidOperationException("Missing TrailCell_" + index.ToString("00"));
        }

        private static void AssertGeometry(DioramaBoard board, string layoutBefore, CellSnapshot[] geometry)
        {
            if (EditorJsonUtility.ToJson(board.layout) != layoutBefore)
                throw new InvalidOperationException("Island polishing changed the gameplay layout.");
            var current = board.GetComponentsInChildren<DioramaCell>(true);
            if (current.Length != geometry.Length)
                throw new InvalidOperationException("Island polishing changed the playable cell count.");
            foreach (var snapshot in geometry)
                snapshot.Validate();
        }

        private sealed class CellSnapshot
        {
            private readonly DioramaCell cell;
            private readonly int id;
            private readonly Transform parent;
            private readonly Vector3 position, scale;
            private readonly Quaternion rotation;
            private readonly BoxCollider collider;
            private readonly string colliderState;
            private readonly Renderer highlight;

            internal CellSnapshot(DioramaCell value)
            {
                cell = value;
                id = cell.cellId;
                parent = cell.transform.parent;
                position = cell.transform.localPosition;
                rotation = cell.transform.localRotation;
                scale = cell.transform.localScale;
                highlight = cell.highlight;
                collider = cell.GetComponent<BoxCollider>();
                if (collider == null)
                    throw new InvalidOperationException("Playable cell has no box collider: " + id);
                colliderState = EditorJsonUtility.ToJson(collider);
            }

            internal void Validate()
            {
                if (cell == null || cell.cellId != id || cell.transform.parent != parent ||
                    cell.transform.localPosition != position || cell.transform.localRotation != rotation ||
                    cell.transform.localScale != scale || cell.highlight != highlight || collider == null ||
                    EditorJsonUtility.ToJson(collider) != colliderState)
                    throw new InvalidOperationException("Island polishing changed cell geometry: " + id);
            }
        }

        private static Dictionary<string, Material> BuildMaterials()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { FirstTrailArtMaterials.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(Folder, StringComparison.Ordinal))
                    continue;
                var source = AssetDatabase.LoadAssetAtPath<Material>(path);
                result[source.name] = CopyMaterial(source.name, source.name);
            }
            BuildGround(result["TrailGround"]);
            var limestone = BuildLimestoneTexture(out var limestoneNormal);
            Tint(result["TrailNeedles"], new Color(.76f, .94f, .78f), .10f);
            result["TrailNeedlesLight"] = CopyMaterial("TrailNeedlesLight", "TrailNeedles");
            result["TrailNeedlesDeep"] = CopyMaterial("TrailNeedlesDeep", "TrailNeedles");
            Tint(result["TrailNeedlesLight"], new Color(.92f, 1f, .84f), .10f);
            Tint(result["TrailNeedlesDeep"], new Color(.62f, .80f, .66f), .08f);
            Tint(result["ShoreMoss"], new Color(.43f, .57f, .31f), .06f);
            Tint(result["ShoreLeaf"], new Color(.58f, .67f, .34f), .10f);
            Tint(result["TrailWood"], new Color(.86f, .80f, .70f), .08f);
            AddStone(result, "IslandLimestone", "ShoreStone", limestone, limestoneNormal, new Color(.49f, .53f, .50f));
            AddStone(result, "IslandLimestoneLight", "ShoreStoneLight", limestone, limestoneNormal, new Color(.64f, .66f, .60f));
            AddStone(result, "IslandLimestoneDark", "ShoreStoneCool", limestone, limestoneNormal, new Color(.37f, .43f, .41f));
            AddStone(result, "TrialStone0", "TrialStone0", limestone, limestoneNormal, new Color(.96f, .95f, .89f));
            AddStone(result, "TrialStone1", "TrialStone1", limestone, limestoneNormal, new Color(.91f, .92f, .87f));
            AddTint(result, "IslandFern", "ShoreLeaf", new Color(.33f, .55f, .27f));
            AddTint(result, "IslandGrass", "ShoreMoss", new Color(.57f, .65f, .31f));
            AddTint(result, "IslandMushroom", "TrailCloth", new Color(.74f, .22f, .13f));
            AddTint(result, "IslandMushroomStem", "TrialStone0", new Color(.91f, .89f, .77f));
            ConfigureWater(result["TrailLake"], false);
            ConfigureWater(result["TrailWater"], true);
            var splash = result["TrailSplash"];
            splash.SetFloat("_RingStrength", .45f);
            splash.SetFloat("_Opacity", .75f);
            splash.SetColor("_FoamColor", new Color(.62f, .79f, .72f, 1f));
            EditorUtility.SetDirty(splash);
            return result;
        }

        private static Material CopyMaterial(string name, string sourceName)
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(FirstTrailArtMaterials.Folder + sourceName + ".mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>(Hero + sourceName + ".mat");
            if (sourceName == "WoodlandAtlas")
                source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/05_Gameplay_Data/Battle/Diorama/Atlas.mat");
            if (source == null)
                throw new InvalidOperationException("Missing original material for polished island: " + sourceName);
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static void Tint(Material material, Color color, float smoothness)
        {
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
        }

        private static void AddTint(Dictionary<string, Material> materials, string name, string source, Color color)
        {
            var material = CopyMaterial(name, source);
            Tint(material, color, .08f);
            materials[name] = material;
        }

        private static void AddStone(Dictionary<string, Material> materials, string name, string source,
            Texture2D texture, Texture2D normal, Color color)
        {
            var material = CopyMaterial(name, source);
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTexture("_BumpMap", normal);
            material.SetTextureScale("_BumpMap", Vector2.one);
            material.SetFloat("_BumpScale", name.StartsWith("TrialStone", StringComparison.Ordinal) ? .15f : .32f);
            material.EnableKeyword("_NORMALMAP");
            Tint(material, color, .07f);
            materials[name] = material;
        }

        private static void ConfigureWater(Material material, bool river)
        {
            material.shader = Shader.Find("Diceforge/Stylized Water Surface")
                ?? throw new InvalidOperationException("The stylized water shader is missing.");
            material.SetFloat("_CausticStrength", river ? .04f : .07f);
            material.SetFloat("_FoamStrength", river ? .45f : .3f);
            material.SetFloat("_SpecularStrength", .3f);
            material.SetFloat("_PatternScale", river ? 1f : .55f);
            EditorUtility.SetDirty(material);
        }

        private static void BuildGround(Material material)
        {
            const int size = 512;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1), v = y / (float)(size - 1);
                    var point = new Vector2((u - .5f) * 7.5f, (v - .5f) * 4.5f);
                    float distance = 100f;
                    for (int i = 0; i < Path.Length - 1; i++)
                    {
                        var segment = Path[i + 1] - Path[i];
                        float t = Mathf.Clamp01(Vector2.Dot(point - Path[i], segment) / segment.sqrMagnitude);
                        distance = Mathf.Min(distance, Vector2.Distance(point, Path[i] + segment * t));
                    }
                    float camp = Vector2.Distance(point, new Vector2(-2.7f, .15f));
                    distance = Mathf.Min(distance, Mathf.Max(0, camp - .45f));
                    float broad = Mathf.PerlinNoise(u * 4.8f + 17f, v * 4.2f + 31f);
                    float grain = Mathf.PerlinNoise(u * 125f, v * 125f);
                    float shoulder = Mathf.PerlinNoise(u * 17f + 7f, v * 14f + 29f);
                    float pathProtection = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(.10f, .43f, distance + (shoulder - .5f) * .20f));
                    float woodland = 0f;
                    foreach (var patch in WoodlandPatches)
                    {
                        float patchWeight = 1f - Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(.55f, 1.85f,
                                Vector2.Distance(point, patch) + (shoulder - .5f) * .38f));
                        woodland = Mathf.Max(woodland, patchWeight);
                    }
                    float scatteredMoss = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, .65f, broad)) * .52f;
                    float moss = Mathf.Clamp01((woodland * .92f + scatteredMoss) * pathProtection);
                    Color earth = Color.Lerp(new Color(.37f, .31f, .23f), new Color(.55f, .47f, .35f), broad);
                    Color green = Color.Lerp(new Color(.26f, .35f, .22f), new Color(.41f, .49f, .31f), broad);
                    pixels[y * size + x] = Color.Lerp(earth, green, moss) * (.94f + grain * .12f);
                    pixels[y * size + x].a = 1f;
                }
            material.SetTexture("_BaseMap", SaveTexture("IslandGroundColor", size, pixels, TextureWrapMode.Clamp));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", .3f);
            material.SetFloat("_Smoothness", .06f);
            EditorUtility.SetDirty(material);
        }

        private static Texture2D BuildLimestoneTexture(out Texture2D normal)
        {
            const int size = 512;
            var pixels = new Color[size * size];
            var heights = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float broad = PeriodicNoise(u, v, 5f, 4f);
                    float medium = PeriodicNoise(u, v, 19f, 17f);
                    float fine = PeriodicNoise(u, v, 95f, 31f);
                    float veinWave = Mathf.Abs(Mathf.Sin((u * 3f + v * 2f) * Mathf.PI * 2f + (broad - .5f) * 5f));
                    float vein = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.025f, .13f, veinWave));
                    float pits = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.69f, .85f, fine));
                    float weathering = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.30f, .70f, medium));
                    float shade = .83f + broad * .14f + fine * .035f - weathering * .025f - vein * .055f - pits * .045f;
                    pixels[y * size + x] = new Color(shade, shade, shade, 1f);
                    heights[y * size + x] = broad * .12f + medium * .045f + fine * .018f - vein * .02f - pits * .045f;
                }
            var normals = new Color[pixels.Length];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                    float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                    var direction = new Vector3(-dx * 7f, -dy * 7f, 1f).normalized;
                    normals[y * size + x] = new Color(direction.x * .5f + .5f,
                        direction.y * .5f + .5f, direction.z * .5f + .5f, 1f);
                }
            normal = SaveTexture("IslandLimestoneNormal", size, normals, TextureWrapMode.Repeat, true);
            return SaveTexture("IslandLimestoneColor", size, pixels, TextureWrapMode.Repeat);
        }

        private static float PeriodicNoise(float u, float v, float frequency, float offset)
        {
            float x = u * frequency + offset, y = v * frequency + offset * .73f;
            float lower = Mathf.Lerp(Mathf.PerlinNoise(x, y), Mathf.PerlinNoise(x - frequency, y), u);
            float upper = Mathf.Lerp(Mathf.PerlinNoise(x, y - frequency),
                Mathf.PerlinNoise(x - frequency, y - frequency), u);
            return Mathf.Lerp(lower, upper, v);
        }

        private static Texture2D SaveTexture(string name, int size, Color[] pixels, TextureWrapMode wrap, bool normal = false)
        {
            string path = Folder + name + ".png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            if (normal)
                importer.convertToNormalmap = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
