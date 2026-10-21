using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Diceforge.View.Editor
{
    internal static class LivingForestStaticMeshes
    {
        // Bake only inert source dressing. Landings must still hide independently in L1;
        // water, cloth and weighted foliage retain their own transforms and materials.
        public static void Build(Transform source, Transform destination, DioramaBoard board)
        {
            var protectedObjects = new HashSet<GameObject>(board.landscapeRoot
                .GetComponentsInChildren<DioramaCell>(true).SelectMany(cell => cell.presentationObjects));
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var disabled = new HashSet<Renderer>();
            int inputs = 0, outputs = 0;
            foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (!renderer.enabled || filter == null || filter.sharedMesh == null || protectedObjects.Contains(renderer.gameObject)) continue;
                var slots = renderer.sharedMaterials;
                if (slots.Any(material => material == null || material.renderQueue >= 2500)) continue;
                if (filter.sharedMesh.vertexCount > 60000) continue;
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                {
                    var material = slots[Mathf.Min(submesh, slots.Length - 1)];
                    if (!groups.TryGetValue(material, out var list)) { list = new List<CombineInstance>(); groups.Add(material, list); }
                    list.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = submesh,
                        transform = destination.worldToLocalMatrix * renderer.localToWorldMatrix });
                }
                disabled.Add(renderer); inputs++;
            }
            string folder = LivingForestSetup.Folder + "StaticMeshes/";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            foreach (var group in groups)
            {
                int chunk = 0, count = 0;
                var items = new List<CombineInstance>();
                foreach (var item in group.Value)
                {
                    if (count + item.mesh.vertexCount > 60000 && items.Count > 0)
                    { Save(items, group.Key, folder, destination, chunk++); outputs++; items.Clear(); count = 0; }
                    items.Add(item); count += item.mesh.vertexCount;
                }
                if (items.Count > 0) { Save(items, group.Key, folder, destination, chunk); outputs++; }
            }
            foreach (var renderer in disabled) renderer.enabled = false;
            Directory.CreateDirectory("docs/Validation/LivingForest");
            File.WriteAllText("docs/Validation/LivingForest/static-meshes.json",
                "{\"sourceRenderers\":" + inputs + ",\"combinedRenderers\":" + outputs + ",\"protectedObjects\":" + protectedObjects.Count + "}");
        }

        private static void Save(List<CombineInstance> parts, Material material, string folder, Transform parent, int chunk)
        {
            string name = material.name + "_" + chunk;
            string path = folder + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
            else mesh.Clear();
            mesh.CombineMeshes(parts.ToArray(), true, true, false); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var root = new GameObject("Static dressing " + name); root.transform.SetParent(parent, false);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
