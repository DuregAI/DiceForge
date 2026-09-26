using Diceforge.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Diceforge.View
{
    public interface IBoardGeometry
    {
        Vector3 CellPosition(int cellId);
        Quaternion CellRotation(int cellId);
        Vector3 FormationOffset(int slot, int count);
        Vector3 WaitingPosition(int player);
        Vector3 ExitPosition(int player);
        Bounds WorldBounds { get; }
    }

    public sealed class TilemapBoardGeometry : IBoardGeometry
    {
        private readonly BoardLayout layout;
        private readonly Tilemap tilemap;
        public TilemapBoardGeometry(BoardLayout layout, Tilemap tilemap) { this.layout = layout; this.tilemap = tilemap; }
        public Vector3 CellPosition(int id)
        {
            foreach (var cell in layout.cells)
                if (cell.cellId == id) return tilemap != null ? tilemap.GetCellCenterWorld(cell.gridPos) : cell.worldPos;
            throw new System.ArgumentOutOfRangeException(nameof(id));
        }
        public Quaternion CellRotation(int id) => Quaternion.identity;
        public Vector3 FormationOffset(int slot, int count)
        {
            if (count <= 1) return Vector3.zero;
            float angle = slot * Mathf.PI * 2 / count;
            return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * .12f;
        }
        public Vector3 WaitingPosition(int player) => WorldBounds.center + Vector3.right * (player == 0 ? -.28f : .28f);
        public Vector3 ExitPosition(int player) => WaitingPosition(player);
        public Bounds WorldBounds
        {
            get { var b = new Bounds(CellPosition(layout.cells[0].cellId), Vector3.zero); foreach (var c in layout.cells) b.Encapsulate(CellPosition(c.cellId)); return b; }
        }
    }
}
