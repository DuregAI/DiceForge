using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Diceforge.View.Editor
{
    public static class FirstTrailPresentationSetup
    {
        private const string PrefabPath = "Assets/_Project/05_Gameplay_Data/Battle/Diorama/FirstTrail8.prefab";

        [MenuItem("Diceforge/Woodland/Apply first level framing")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before changing trail presentation.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                BindVisuals(root.GetComponent<DioramaBoard>());
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var level = AssetDatabase.LoadAssetAtPath<GameModes.DemoLevelDefinition>(
                "Assets/_Project/05_Gameplay_Data/GameModes/Demo_Level_01.asset");
            level.firstVisibleCell = 4;
            level.cameraZoom = 1.15f;
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
        }

        // Resolve imported art once during authoring; runtime visibility uses these saved references.
        public static void BindVisuals(DioramaBoard board)
        {
            var cells = board.landscapeRoot.GetComponentsInChildren<DioramaCell>(true);
            var renderers = board.landscapeRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var cell in cells)
            {
                string name = "TrailCell_" + cell.cellId.ToString("00");
                var surface = renderers.Single(renderer => renderer.name.Split('.')[0] == name);
                var objects = new List<GameObject> { surface.gameObject };
                foreach (var renderer in renderers)
                {
                    if (!renderer.name.StartsWith("Stone edge fleck", StringComparison.Ordinal) &&
                        !renderer.name.StartsWith("Tile foot moss", StringComparison.Ordinal) &&
                        !renderer.name.StartsWith("Tile setting pebble", StringComparison.Ordinal)) continue;
                    Vector3 point = renderer.bounds.center;
                    var nearest = cells.OrderBy(candidate => FlatDistance(point, candidate.transform.position)).First();
                    if (nearest == cell) objects.Add(renderer.gameObject);
                }
                cell.presentationObjects = objects.ToArray();
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
