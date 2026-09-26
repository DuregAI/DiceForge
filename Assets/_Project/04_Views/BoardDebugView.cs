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
        private DioramaBoard _diorama;
        private BattleDebugController _battle;
        private Vector2 _touchStart;
        public void ConfigureGeometry(DioramaBoard board) { _diorama=board; _battle=FindAnyObjectByType<BattleDebugController>(); }

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
            if(!enabled && _diorama!=null)_diorama.Preview(null,null,-1);
        }

        public void SetHighlightedCells(System.Collections.Generic.IReadOnlyCollection<int> cells)
        {
            _diorama?.Highlight(cells);
        }

        private void Update()
        {
            if (_diorama != null) { UpdateDioramaInput(); return; }
            if (Diceforge.Transitions.ScreenTransition.IsBusy) return;
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
            if (_diorama != null) return TryPickDiorama(screenPosition, out cellIndex, out tokenName);

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
            if (_diorama != null) return TryPickDiorama(screenPosition, out cellIndex, out _);

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
            if (_diorama != null) return _diorama.CellPosition(cell.cellId);
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

        private void UpdateDioramaInput()
        {
            if (!_cellSelectionEnabled || Diceforge.Transitions.ScreenTransition.IsBusy || DioramaHud.BlocksGameplay) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            Vector2 pos = default; bool pressed = false;
            if(Mouse.current!=null)
            {
                var hover=Mouse.current.position.ReadValue();
                if(!DioramaHud.IsOverInterface(hover) && TryPickDiorama(hover,out int hoverCell,out _))
                    _diorama.Preview(_battle.PreviewMove(hoverCell),_battle.PresentationState,hoverCell);
                else _diorama.Preview(null,null,-1);
            }
            var touch = Touchscreen.current;
            if (touch != null)
            {
                if (touch.primaryTouch.press.wasPressedThisFrame) _touchStart=touch.primaryTouch.position.ReadValue();
                if (touch.primaryTouch.press.wasReleasedThisFrame)
                {
                    pos=touch.primaryTouch.position.ReadValue();
                    pressed=Vector2.Distance(pos,_touchStart)<20 && !DioramaHud.IsOverInterface(_touchStart);
                }
            }
            if (!pressed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            { pos=Mouse.current.position.ReadValue(); pressed=true; }
            if (!pressed || DioramaHud.IsOverInterface(pos)) return;
            if(!TryPickDiorama(pos,out int cell,out string token))return;
            _lastClickedPlayerATokenName=token;
            _cellSelectionEnabled=false;
            OnCellClicked?.Invoke(cell);
        }
        private bool TryPickDiorama(Vector2 position,out int cell,out string token)
        {
            cell=-1;token=null;
            if(_camera==null)_camera=Camera.main;
            if(_camera==null)return false;
            var hits=Physics.RaycastAll(_camera.ScreenPointToRay(position),100);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                var unit=hit.collider.GetComponentInParent<DioramaToken>();
                if(unit!=null && unit.cellId>=0 && (_battle==null || unit.player==(int)_battle.CurrentPlayer))
                {cell=unit.cellId;token=unit.gameObject.name;return true;}
                var tile=hit.collider.GetComponentInParent<DioramaCell>();
                if(tile!=null){cell=tile.cellId;return true;}
            }
            return false;
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
