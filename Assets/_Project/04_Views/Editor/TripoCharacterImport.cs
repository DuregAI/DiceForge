using System;
using System.IO;
using System.Linq;
using Diceforge.GameModes;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Diceforge.View.Editor
{
    // Local FBX intake works with Studio exports and needs no API key or runtime dependency.
    public static class TripoCharacterImport
    {
        [MenuItem("Diceforge/Characters/Import selected rigged FBX/Tish")]
        public static void Tish() => Import("tish", 1.25f);
        [MenuItem("Diceforge/Characters/Import selected rigged FBX/Luma")]
        public static void Luma() => Import("luma", 1.25f);
        [MenuItem("Diceforge/Characters/Import selected rigged FBX/Bum")]
        public static void Bum() => Import("bum", 1.65f);
        [MenuItem("Diceforge/Characters/Import selected rigged FBX/Scratchy")]
        public static void Scratchy() => Import("scratchy", 1.05f);

        public static string Import(string id, float height)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Import characters outside Play Mode.");
            string input = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(input) || !input.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select the rigged FBX exported from Tripo in the Project window.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(input);
            var skins = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length == 0 || skins.Any(s => s.bones.Length == 0))
                throw new InvalidOperationException("The selected model has no usable skin/rig. Export a rigged FBX from Tripo.");
            var clips = AssetDatabase.LoadAllAssetsAtPath(input).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            AnimationClip Clip(string name) => clips.FirstOrDefault(c => c.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? clips.FirstOrDefault(c => c.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);
            if (Clip("Idle") == null || Clip("Walk") == null)
                throw new InvalidOperationException("FBX must include identifiable Idle and Walk clips. Set their names in the Animation importer before intake.");
            var catalog = DemoCharacterCatalog.Load();
            var character = catalog?.Find(id);
            if (character == null) throw new InvalidOperationException("Import the Scratchy demo catalog first.");
            string folder = "Assets/_Project/07_Art/Characters/Imported/" + id;
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string controllerPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + id + ".controller");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (string stateName in new[] { "Idle", "Walk", "Selected", "Jump", "Land", "Victory", "Return" })
            {
                var clip = Clip(stateName);
                if (clip == null) continue;
                var state = controller.layers[0].stateMachine.AddState(stateName); state.motion = clip;
                if (stateName == "Idle") controller.layers[0].stateMachine.defaultState = state;
            }
            var root = new GameObject(id);
            string prefabPath;
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source); model.name = "Model";
                model.transform.SetParent(root.transform, false);
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y < .001f) throw new InvalidOperationException("Model bounds have zero height.");
                float scale = height / bounds.size.y;
                model.transform.localScale *= scale;
                model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
                var animator = model.GetComponentInChildren<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                root.AddComponent<BoardLayoutTokenMover>(); root.AddComponent<GoblinLife>();
                prefabPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + id + ".prefab");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Undo.RecordObject(catalog, "Replace " + id + " character candidate");
                character.gameplayPrefab = prefab; character.productionApproved = false;
                character.artNotes = "Tripo intake candidate. Review silhouette, feet, material response and animations on the board before approval.";
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Debug.Log("[Character intake] " + id + " imported as candidate: " + prefabPath);
            Audit();
            return prefabPath;
        }

        [MenuItem("Diceforge/Characters/Audit active character catalog")]
        public static void Audit()
        {
            var catalog = DemoCharacterCatalog.Load();
            if (catalog == null) throw new InvalidOperationException("Missing character catalog.");
            var report = new System.Text.StringBuilder("Character intake audit\n");
            foreach (var c in catalog.characters)
            {
                var prefab = c.gameplayPrefab;
                if (prefab == null) { report.AppendLine(c.id + ": MISSING PREFAB"); continue; }
                var meshes = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                long triangles = meshes.Where(m => m.sharedMesh != null).Sum(m => (long)m.sharedMesh.triangles.Length / 3);
                int materials = prefab.GetComponentsInChildren<Renderer>(true).Sum(r => r.sharedMaterials.Length);
                var animator = prefab.GetComponentInChildren<Animator>(true);
                var controller = animator?.runtimeAnimatorController as AnimatorController;
                var states = controller == null ? Array.Empty<string>() : controller.layers[0].stateMachine.states.Select(s => s.state.name).ToArray();
                report.AppendLine(c.id + " | " + AssetDatabase.GetAssetPath(prefab) + " | triangles=" + triangles +
                    " | material slots=" + materials + " | states=" + string.Join(",", states) + " | approved=" + c.productionApproved);
                if (meshes.Length == 0) report.AppendLine("  PROTOTYPE: no skinned model; not production 3D.");
                if (triangles > 40000) report.AppendLine("  Review geometry budget in the target WebGL build.");
                foreach (string expected in new[] { "Idle", "Walk", "Selected", "Jump", "Land", "Victory" })
                    if (!states.Contains(expected)) report.AppendLine("  Missing authored state: " + expected);
                if (controller != null)
                    foreach (var group in controller.layers[0].stateMachine.states.Where(s => s.state.motion != null).GroupBy(s => s.state.motion).Where(g => g.Count() > 1))
                        report.AppendLine("  Reused clip (verify intentional): " + string.Join(",", group.Select(s => s.state.name)));
                if (animator != null && animator.applyRootMotion) report.AppendLine("  ERROR: root motion must be off for board movement.");
            }
            Directory.CreateDirectory("docs/Validation/ScratchyDemo");
            File.WriteAllText("docs/Validation/ScratchyDemo/character-audit.txt", report.ToString());
            Debug.Log(report.ToString());
        }
    }
}
