using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Diceforge.View
{
    public sealed class TileShadowSync : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Tilemap sourceTilemap;
        [SerializeField] private Tilemap shadowTilemap;
        [SerializeField] private TileBase shadowTile;

        [Header("Settings")]
        [SerializeField] private bool clearAllFirst = true;

        public Tilemap SourceTilemap => sourceTilemap;
        public Tilemap ShadowTilemap => shadowTilemap;
        public TileBase ShadowTile => shadowTile;
        public bool ClearAllFirst => clearAllFirst;

        public void SyncNow()
        {
            if (!ValidateReferences())
                return;

            var sourceBounds = sourceTilemap.cellBounds;
            var bounds = new BoundsInt(sourceBounds.xMin, sourceBounds.yMin, sourceBounds.zMin, sourceBounds.size.x, sourceBounds.size.y, 1);

            if (bounds.size.x <= 0 || bounds.size.y <= 0)
                return;

#if UNITY_EDITOR
            Undo.RegisterCompleteObjectUndo(shadowTilemap, "Sync Tile Shadows");
#endif

            if (clearAllFirst)
                shadowTilemap.SetTilesBlock(bounds, CreateClearedTilesArray(bounds));

            var tiles = new TileBase[bounds.size.x * bounds.size.y];

            for (var y = 0; y < bounds.size.y; y++)
            {
                for (var x = 0; x < bounds.size.x; x++)
                {
                    var index = x + (y * bounds.size.x);
                    var position = new Vector3Int(bounds.xMin + x, bounds.yMin + y, bounds.zMin);
                    tiles[index] = sourceTilemap.HasTile(position) ? shadowTile : null;
                }
            }

            shadowTilemap.SetTilesBlock(bounds, tiles);
            MarkDirty();
        }

        public void ClearShadows()
        {
            if (shadowTilemap == null)
            {
                Debug.LogWarning("[TileShadowSync] Missing Shadow Tilemap reference.", this);
                return;
            }

#if UNITY_EDITOR
            Undo.RegisterCompleteObjectUndo(shadowTilemap, "Clear Tile Shadows");
#endif

            shadowTilemap.ClearAllTiles();
            MarkDirty();
        }

        private bool ValidateReferences()
        {
            if (sourceTilemap == null)
            {
                Debug.LogWarning("[TileShadowSync] Missing Source Tilemap reference.", this);
                return false;
            }

            if (shadowTilemap == null)
            {
                Debug.LogWarning("[TileShadowSync] Missing Shadow Tilemap reference.", this);
                return false;
            }

            if (shadowTile == null)
            {
                Debug.LogWarning("[TileShadowSync] Missing Shadow Tile reference.", this);
                return false;
            }

            return true;
        }

        private static TileBase[] CreateClearedTilesArray(BoundsInt bounds)
        {
            return new TileBase[bounds.size.x * bounds.size.y];
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(shadowTilemap);

            if (shadowTilemap.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(shadowTilemap.gameObject.scene);
#endif
        }
    }
}
