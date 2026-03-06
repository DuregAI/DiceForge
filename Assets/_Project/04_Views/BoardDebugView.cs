using System;
using Diceforge.Core;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace Diceforge.View
{
    [Obsolete("Not used any more?")]
    public sealed class BoardDebugView : MonoBehaviour
    {
        private const string PlayerATokenNamePrefix = "StoneA_";
        [Header("Input")]
        [SerializeField] private float tokenClickRadiusPixels = 120f;
        [SerializeField] private float cellClickRadiusPixels = 110f;

        private readonly System.Collections.Generic.List<GameObject> _stonePoolA = new();
        private readonly System.Collections.Generic.List<GameObject> _stonePoolB = new();
        private CellMarker[] _cellMarkers;
        private bool _cellSelectionEnabled;
        private Camera _camera;
        private BoardLayout _selectionLayout;
        private Tilemap _selectionTilemap;
        private string _lastClickedPlayerATokenName;

        public event Action<int> OnCellClicked;

        private void Awake()
        {
            _camera = Camera.main;
        }

        public void ConfigureSelectionSpace(BoardLayout layout, Tilemap positionTilemap)
        {
            if (layout == null || layout.cells == null || layout.cells.Count == 0)
                throw new InvalidOperationException("[BoardDebugView] ConfigureSelectionSpace failed: layout is missing cells.");

            _selectionLayout = layout;
            _selectionTilemap = positionTilemap;
            BuildCells();
        }

        public void HandleMatchStarted(GameState state, MatchLog log)
        {
            BuildCells();
            RefreshPieces();
        }

        public void HandleMoveApplied(MoveRecord record)
        {
            RefreshPieces();
        }

        public void HandleMatchEnded(GameState state)
        {
            RefreshPieces();
        }

        public void SetCellSelectionEnabled(bool enabled)
        {
            _cellSelectionEnabled = enabled;
        }

        public void SetHighlightedCells(System.Collections.Generic.IReadOnlyCollection<int> cells)
        {
            // Intentionally no-op: legacy gizmo highlights were removed.
        }

        private void Update()
        {
            if (!_cellSelectionEnabled)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            Vector2 position = mouse.position.ReadValue();
            int cellIndex;
            if (TryPickPlayerATokenCell(position, out cellIndex, out string tokenName))
            {
                _lastClickedPlayerATokenName = tokenName;
            }
            else if (TryPickCell(position, out cellIndex))
            {
                _lastClickedPlayerATokenName = null;
            }
            else
            {
                return;
            }

            _cellSelectionEnabled = false;
            OnCellClicked?.Invoke(cellIndex);
        }

        public string ConsumeLastClickedPlayerATokenName()
        {
            string tokenName = _lastClickedPlayerATokenName;
            _lastClickedPlayerATokenName = null;
            return tokenName;
        }

        private bool TryPickPlayerATokenCell(Vector2 screenPosition, out int cellIndex, out string tokenName)
        {
            cellIndex = -1;
            tokenName = null;

            BoardLayoutTokenMover[] movers = FindObjectsByType<BoardLayoutTokenMover>(FindObjectsSortMode.None);
            if (movers == null || movers.Length == 0)
                return false;

            float radius = Mathf.Max(8f, tokenClickRadiusPixels);
            float radiusSqr = radius * radius;
            float bestSqr = float.MaxValue;
            int bestCell = -1;
            string bestTokenName = null;

            for (int i = 0; i < movers.Length; i++)
            {
                BoardLayoutTokenMover mover = movers[i];
                if (mover == null || !mover.isActiveAndEnabled)
                    continue;

                if (!IsPlayerATokenName(mover.gameObject.name))
                    continue;

                if (mover.CurrentCellId < 0)
                    continue;

                Vector3 tokenScreen = _camera.WorldToScreenPoint(mover.transform.position);
                if (tokenScreen.z <= 0f)
                    continue;

                Vector2 delta = new Vector2(tokenScreen.x, tokenScreen.y) - screenPosition;
                float sqr = delta.sqrMagnitude;
                if (sqr > radiusSqr || sqr >= bestSqr)
                    continue;

                bestSqr = sqr;
                bestCell = mover.CurrentCellId;
                bestTokenName = mover.gameObject.name;
            }

            if (bestCell < 0)
                return false;

            cellIndex = bestCell;
            tokenName = bestTokenName;
            return true;
        }

        private static bool IsPlayerATokenName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            return objectName.StartsWith(PlayerATokenNamePrefix, StringComparison.Ordinal);
        }

        private bool TryPickCell(Vector2 screenPosition, out int cellIndex)
        {
            cellIndex = -1;

            if (_selectionLayout == null || _selectionLayout.cells == null || _selectionLayout.cells.Count == 0)
                return false;

            float radius = Mathf.Max(8f, cellClickRadiusPixels);
            float radiusSqr = radius * radius;
            float bestSqr = float.MaxValue;
            int bestCell = -1;

            for (int i = 0; i < _selectionLayout.cells.Count; i++)
            {
                CellData cell = _selectionLayout.cells[i];
                Vector3 world = ResolveCellWorldPosition(cell);
                Vector3 cellScreen = _camera.WorldToScreenPoint(world);
                if (cellScreen.z <= 0f)
                    continue;

                Vector2 delta = new Vector2(cellScreen.x, cellScreen.y) - screenPosition;
                float sqr = delta.sqrMagnitude;
                if (sqr > radiusSqr || sqr >= bestSqr)
                    continue;

                bestSqr = sqr;
                bestCell = cell.cellId;
            }

            if (bestCell < 0)
                return false;

            cellIndex = bestCell;
            return true;
        }

        private void BuildCells()
        {
            ClearCells();

            if (_selectionLayout == null || _selectionLayout.cells == null || _selectionLayout.cells.Count == 0)
                return;

            _cellMarkers = new CellMarker[_selectionLayout.cells.Count];
            for (int i = 0; i < _selectionLayout.cells.Count; i++)
            {
                CellData cell = _selectionLayout.cells[i];
                GameObject cellObj = new GameObject($"Cell_{cell.cellId}");
                cellObj.transform.SetParent(transform, false);
                cellObj.transform.position = ResolveCellWorldPosition(cell);

                CellMarker marker = cellObj.AddComponent<CellMarker>();
                marker.Index = cell.cellId;
                _cellMarkers[i] = marker;
            }
        }

        private void ClearCells()
        {
            if (_cellMarkers == null)
                return;

            for (int i = 0; i < _cellMarkers.Length; i++)
            {
                CellMarker marker = _cellMarkers[i];
                if (marker != null)
                    Destroy(marker.gameObject);
            }

            _cellMarkers = null;
        }

        private Vector3 ResolveCellWorldPosition(CellData cell)
        {
            if (_selectionTilemap != null)
                return _selectionTilemap.GetCellCenterWorld(cell.gridPos);

            return cell.worldPos;
        }

        private void RefreshPieces()
        {
            // Legacy debug stone GameObjects are intentionally disabled.
            // Battle visuals are owned by StonesTokensView.
            DisableLegacyStoneVisuals();
        }

        private void DisableLegacyStoneVisuals()
        {
            DisableLegacyStonePool(_stonePoolA);
            DisableLegacyStonePool(_stonePoolB);
        }

        private static void DisableLegacyStonePool(System.Collections.Generic.List<GameObject> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                    pool[i].SetActive(false);
            }
        }
    }
}
