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
    public static class BetaWorldSetup
    {
        public const string Folder = "Assets/_Project/07_Art/BetaPolish/";
        private const string Board = "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab";
        private static readonly Dictionary<Material, Material> replacements = new();

        [MenuItem("Diceforge/Woodland/Apply beta world finish")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Finish Play Mode or player build before authoring the forest.");
            Directory.CreateDirectory(Folder + "Materials"); Directory.CreateDirectory(Folder + "Meshes");
            AssetDatabase.Refresh(); replacements.Clear();
            string backup = Folder + "FirstTrail8_BeforeBeta.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(backup) == null) AssetDatabase.CopyAsset(Board, backup);
            var root = PrefabUtility.LoadPrefabContents(Board);
            try
            {
                var board = root.GetComponent<DioramaBoard>();
                var cells = root.GetComponentsInChildren<DioramaCell>(true);
                var anchors = cells.Select(c => c.transform.localPosition).ToArray();
                string layout = EditorJsonUtility.ToJson(board.layout);
                var forest = root.transform.Find("Living forest");
                var previous = forest.Find("Beta world finish");
                if (previous != null) Object.DestroyImmediate(previous.gameObject);
                var dressing = new GameObject("Beta world finish"); dressing.transform.SetParent(forest, false);
                ApplyMaterials(root);
                int plants = ReplacePlants(forest, dressing.transform);
                AddShore(dressing.transform);
                AddRoots(dressing.transform);
                AddChapterDetails(root, dressing);
                if (cells.Where((cell, i) => cell.transform.localPosition != anchors[i]).Any() || layout != EditorJsonUtility.ToJson(board.layout))
                    throw new InvalidOperationException("World finish moved a gameplay landing.");
                PrefabUtility.SaveAsPrefabAsset(root, Board);
                Directory.CreateDirectory("docs/Art/MapDesign/BetaPolish-v01");
                File.WriteAllText("docs/Art/MapDesign/BetaPolish-v01/world-manifest.json",
                    "{\"replacedPlantGroups\":" + plants + ",\"landingsUnchanged\":true,\"chapterVariants\":6}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static void ApplyMaterials(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i]; if (source == null) continue;
                    string name = source.name;
                    if (!new[] { "ForestGround", "ForestStone", "ForestShore", "ForestBoulder" }.Contains(name)) continue;
                    if (!replacements.TryGetValue(source, out var material))
                    {
                        material = CopyMaterial(source, name);
                        if (name == "ForestGround")
                        {
                            ConfigureTexture("GroundDetail.png", false); ConfigureTexture("GroundDetailNormal.png", true);
                            material.SetTexture("_DetailAlbedoMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "GroundDetail.png"));
                            material.SetTexture("_DetailNormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "GroundDetailNormal.png"));
                            material.SetTextureScale("_DetailAlbedoMap", new Vector2(18, 18));
                            material.SetFloat("_DetailAlbedoMapScale", .38f); material.SetFloat("_DetailNormalMapScale", .30f);
                            material.SetFloat("_Smoothness", .10f);
                            material.DisableKeyword("_DETAIL_MULX2"); material.EnableKeyword("_DETAIL_SCALED");
                        }
                        else
                        {
                            material.SetFloat("_Relief", name == "ForestStone" ? .22f : .19f);
                            material.SetFloat("_MossAmount", name == "ForestStone" ? .22f : .48f);
                            material.SetFloat("_ShoreWetness", name == "ForestStone" ? 0 : .8f);
                        }
                        EditorUtility.SetDirty(material); replacements.Add(source, material);
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
            }
        }

        private static Material CopyMaterial(Material source, string name)
        {
            string path = Folder + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
            else material.CopyPropertiesFromMaterial(source);
            material.name = name; material.enableInstancing = true;
            return material;
        }

        private static void ConfigureTexture(string name, bool normal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + name);
            if (importer == null) throw new InvalidOperationException("Missing ground texture " + name);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = 512; importer.anisoLevel = 4; importer.SaveAndReimport();
        }

        private static int ReplacePlants(Transform forest, Transform destination)
        {
            int index = 0, grass = 0;
            foreach (var old in forest.Cast<Transform>().ToArray())
            {
                if (old == destination) continue;
                bool shrub = old.name.StartsWith("Shrub_A") || old.name.StartsWith("CanopyShrub_");
                bool broadleaf = old.name.StartsWith("Fern_A") && old.localPosition.z < 2;
                if (old.name.StartsWith("GrassTuft_A"))
                {
                    old.gameObject.SetActive(++grass % 3 == 0);
                    continue;
                }
                if (!shrub && !broadleaf) continue;
                var name = broadleaf ? "ShoreBroadleaf_A" : old.name.Contains("Canopy") || index % 2 == 0 ? "RoundleafShrub_B" : "RoundleafShrub_A";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BetaFoliageSetup.Folder + name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Import the beta foliage kit first.");
                var bounds = MeshBounds(old);
                var authored = MeshBounds(prefab.transform);
                float width = Mathf.Max(bounds.size.x, bounds.size.z);
                float authoredWidth = Mathf.Max(authored.size.x, authored.size.z);
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, destination);
                replacement.name = name + "_" + index++;
                replacement.transform.localPosition = old.localPosition;
                replacement.transform.localRotation = old.localRotation;
                replacement.transform.localScale = Vector3.one * (width / Mathf.Max(.01f, authoredWidth)) * (broadleaf ? 1.2f : 1.05f);
                old.gameObject.SetActive(false);
            }
            return index;
        }

        private static Bounds MeshBounds(Transform root)
        {
            bool first = true; Bounds bounds = default;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        private static void AddShore(Transform parent)
        {
            var positions = new[] { new Vector3(-3.35f,-.76f,-1.42f), new Vector3(-2.12f,-.76f,-2.04f),
                new Vector3(-.98f,-.76f,-2.16f), new Vector3(.35f,-.76f,-2.12f),
                new Vector3(1.18f,-.76f,-1.93f), new Vector3(3.46f,-.76f,-.74f), new Vector3(3.53f,-.76f,.45f) };
            for (int i = 0; i < positions.Length; i++)
            {
                bool detailed = i == 0 || i == 3 || i == 5;
                var rock = Add(parent, detailed ? "RockGarden_Tripo_A" : "Boulder_C", positions[i], .83f + (i % 3) * .08f, i * 53 + 17);
                if (!detailed) rock.transform.localScale = new Vector3(.87f, 1.7f, .72f);
                foreach (var renderer in rock.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Materials/ForestBoulder.mat");
            }
        }

        private static void AddRoots(Transform parent)
        {
            var points = new[] { new Vector3(-3.04f,.12f,1.20f), new Vector3(-3.31f,.05f,.78f),
                new Vector3(-3.68f,-.10f,.42f), new Vector3(-3.95f,-.43f,.15f), new Vector3(-4.05f,-.72f,-.10f) };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int branch = 0; branch < 3; branch++)
            for (int ring = 0; ring < points.Length; ring++)
            {
                var p = points[ring] + new Vector3(branch * .12f, -.02f * branch, branch * .17f);
                var direction = points[Mathf.Min(ring + 1, points.Length - 1)] - points[Mathf.Max(ring - 1, 0)];
                var side = Vector3.Cross(direction.normalized, Vector3.up).normalized;
                var up = Vector3.Cross(side, direction.normalized).normalized;
                float radius = Mathf.Lerp(.13f - branch * .02f, .018f, ring / (float)(points.Length - 1));
                for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.PI * 2 / 10;
                    vertices.Add(p + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
                    if (ring == points.Length - 1) continue;
                    int n = branch * points.Length * 10 + ring * 10 + i;
                    int next = branch * points.Length * 10 + ring * 10 + (i + 1) % 10;
                    triangles.Add(n); triangles.Add(n + 10); triangles.Add(next + 10);
                    triangles.Add(n); triangles.Add(next + 10); triangles.Add(next);
                }
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "Meshes/ShoreRoots.asset");
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, Folder + "Meshes/ShoreRoots.asset"); }
            mesh.Clear(); mesh.name = "Oak roots over wet shore";
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var go = new GameObject("Oak shore roots"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LivingForestSetup.Folder + "Materials/ForestBark.mat");
        }

        private static GameObject Add(Transform parent, string name, Vector3 position, float scale, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LivingForestSetup.Folder + "Prefabs/" + name + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name; instance.transform.localPosition = position;
            instance.transform.localScale = Vector3.one * scale; instance.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return instance;
        }

        private static void AddChapterDetails(GameObject root, GameObject dressing)
        {
            var variants = dressing.AddComponent<TrailChapterDressing>();
            variants.flag = root.transform.Find("Living forest/Flag_A").GetComponentInChildren<Renderer>();
            variants.defaultFlag = variants.flag.sharedMaterial;
            variants.chapterFlags = new Material[6]; variants.chapterDetails = new GameObject[6];
            Color[] colors = { new(.73f,.24f,.09f), new(.70f,.42f,.10f), new(.48f,.51f,.19f),
                new(.68f,.19f,.13f), new(.18f,.48f,.52f), new(.48f,.31f,.51f) };
            for (int i = 0; i < 6; i++)
            {
                var material = CopyMaterial(variants.defaultFlag, "ChapterFlag" + (i + 1));
                material.SetColor("_BaseColor", colors[i]); EditorUtility.SetDirty(material); variants.chapterFlags[i] = material;
                var details = new GameObject("Visit " + (i + 1)); details.transform.SetParent(dressing.transform, false);
                variants.chapterDetails[i] = details;
                for (int flower = 0; flower < 3; flower++)
                {
                    var plant = Add(details.transform, "Flowers_A", new Vector3(-2.35f + flower * .32f, .03f, -.35f + i * .035f), .6f + flower * .09f, i * 37 + flower * 79);
                    var petal = AssetDatabase.LoadAssetAtPath<Material>(LivingForestSetup.Folder + "Materials/ForestPetal.mat");
                    var petalTint = CopyMaterial(petal, "VisitPetals" + (i + 1));
                    petalTint.SetColor("_BaseColor", Color.Lerp(new Color(.95f,.91f,.65f), colors[i], .40f));
                    foreach (var renderer in plant.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m.name.Contains("Petal") ? petalTint : m).ToArray();
                    EditorUtility.SetDirty(petalTint);
                }
                if (i == 1 || i == 3 || i == 5)
                    Add(details.transform, "Reeds_A", new Vector3(3.0f, -.04f, -.95f), .65f, i * 73);
            }
            variants.Apply("L01");
        }
    }
}
