using System.Collections.Generic;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Diceforge.View
{
    public sealed class TilemapPathBaker : MonoBehaviour
    {
        [SerializeField] private Tilemap pathTilemap;
        [SerializeField] private TileBase startTile;
        [SerializeField] private TileBase endTile;
        [SerializeField] private BoardLayout targetLayout;

        private static readonly Vector3Int[] NeighbourOffsets =
        {
            Vector3Int.up,
            Vector3Int.right,
            Vector3Int.down,
            Vector3Int.left
        };

        public bool BakePath()
        {
            if (pathTilemap == null || startTile == null || endTile == null || targetLayout == null)
            {
                Debug.LogError("TilemapPathBaker is missing required references.", this);
                return false;
            }

            var occupiedCells = new HashSet<Vector3Int>();
            int startCount = 0;
            int endCount = 0;
            Vector3Int startPos = default;
            Vector3Int endPos = default;

            foreach (Vector3Int cellPos in pathTilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = pathTilemap.GetTile(cellPos);
                if (tile == null)
                    continue;

                occupiedCells.Add(cellPos);

                if (tile == startTile)
                {
                    startCount++;
                    if (startCount > 1)
                    {
                        Debug.LogError("Exactly one Start tile is required. Found multiple Start tiles.", this);
                        return false;
                    }

                    startPos = cellPos;
                }

                if (tile == endTile)
                {
                    endCount++;
                    if (endCount > 1)
                    {
                        Debug.LogError("Exactly one End tile is required. Found multiple End tiles.", this);
                        return false;
                    }

                    endPos = cellPos;
                }
            }

            if (startCount != 1 || endCount != 1)
            {
                Debug.LogError($"Exactly one Start tile and one End tile are required. Found Start={startCount}, End={endCount}.", this);
                return false;
            }

            if (occupiedCells.Count == 0)
            {
                Debug.LogError("Path tilemap has no occupied cells.", this);
                return false;
            }

            var orderedPath = BuildOrderedPath(occupiedCells, startPos, endPos);
            if (orderedPath == null)
                return false;

            if (orderedPath.Count != occupiedCells.Count)
            {
                Debug.LogError("Extra tiles not connected to path were found. Ensure all occupied tiles are part of one Start-to-End path.", this);
                return false;
            }

            targetLayout.cells.Clear();
            for (int i = 0; i < orderedPath.Count; i++)
            {
                Vector3Int cell = orderedPath[i];
                targetLayout.cells.Add(new CellData
                {
                    cellId = i,
                    gridPos = cell,
                    worldPos = pathTilemap.GetCellCenterWorld(cell),
                    isStart = cell == startPos,
                    isEnd = cell == endPos
                });
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(targetLayout);
            UnityEditor.AssetDatabase.SaveAssets();
#endif

            Debug.Log($"Baked {targetLayout.cells.Count} path cells into '{targetLayout.name}'.", targetLayout);
            return true;
        }

        private List<Vector3Int> BuildOrderedPath(HashSet<Vector3Int> occupiedCells, Vector3Int startPos, Vector3Int endPos)
        {
            var orderedPath = new List<Vector3Int>(occupiedCells.Count);
            var visited = new HashSet<Vector3Int>();

            Vector3Int current = startPos;
            Vector3Int previous = default;
            bool hasPrevious = false;

            while (true)
            {
                orderedPath.Add(current);
                visited.Add(current);

                if (current == endPos)
                    break;

                var nextCandidates = new List<Vector3Int>(2);
                for (int i = 0; i < NeighbourOffsets.Length; i++)
                {
                    Vector3Int neighbour = current + NeighbourOffsets[i];
                    if (!occupiedCells.Contains(neighbour) || visited.Contains(neighbour))
                        continue;

                    if (hasPrevious && neighbour == previous)
                        continue;

                    nextCandidates.Add(neighbour);
                }

                if (nextCandidates.Count == 0)
                {
                    Debug.LogError("Could not continue path from current tile. Ensure the path is fully connected from Start to End.", this);
                    return null;
                }

                if (nextCandidates.Count > 1)
                {
                    Debug.LogError("Path has branches. MVP baker supports only one unbranched path.", this);
                    return null;
                }

                previous = current;
                hasPrevious = true;
                current = nextCandidates[0];

                if (orderedPath.Count > occupiedCells.Count)
                {
                    Debug.LogError("Path traversal exceeded occupied tile count. Check for loops or invalid path.", this);
                    return null;
                }
            }

            return orderedPath;
        }
    }
}
