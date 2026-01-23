using Diceforge.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Diceforge.View
{
    public sealed class BoardDebugView : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private float ringRadius = 3f;
        [SerializeField] private float cellRadius = 0.15f;
        [SerializeField] private float playerRadius = 0.25f;
        [SerializeField] private float chipHeight = 0.2f;

        [Header("Colors")]
        [SerializeField] private Color cellColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        [SerializeField] private Color chipColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private Color playerAColor = new Color(0.2f, 0.6f, 1f, 0.9f);
        [SerializeField] private Color playerBColor = new Color(1f, 0.4f, 0.2f, 0.9f);
        [SerializeField] private Color lastMoveColor = new Color(1f, 0.9f, 0.3f, 0.9f);

        [Header("Prefabs")]
        [SerializeField] private GameObject pawnPrefab;
        [SerializeField] private GameObject chipPrefab;

        private GameState _state;
        private MatchLog _log;
        private MoveRecord? _lastRecord;
        private GameObject _pawnA;
        private GameObject _pawnB;
        private readonly System.Collections.Generic.List<GameObject> _chipPool = new System.Collections.Generic.List<GameObject>();
        private CellMarker[] _cellMarkers;
        private bool _cellSelectionEnabled;
        private Camera _camera;

        public event System.Action<int> OnCellClicked;

        private void Awake()
        {
            _camera = Camera.main;
        }

        public void HandleMatchStarted(GameState state, MatchLog log)
        {
            _state = state;
            _log = log;
            _lastRecord = null;
            BuildCells();
            BuildPawns();
            RefreshPieces();
        }

        public void HandleMoveApplied(MoveRecord record)
        {
            _lastRecord = record;
            RefreshPieces();
        }

        public void HandleMatchEnded(GameState state)
        {
            _state = state;
            RefreshPieces();
        }

        public void SetCellSelectionEnabled(bool enabled)
        {
            _cellSelectionEnabled = enabled;
        }

        private void Update()
        {
            if (!_cellSelectionEnabled) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (_camera == null)
                    _camera = Camera.main;
                if (_camera == null)
                    return;

                if (Physics.Raycast(_camera.ScreenPointToRay(Input.mousePosition), out var hit))
                {
                    var marker = hit.collider.GetComponent<CellMarker>();
                    if (marker != null)
                    {
                        _cellSelectionEnabled = false;
                        OnCellClicked?.Invoke(marker.Index);
                    }
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (_state == null)
                return;

            int ringSize = _state.Rules.ringSize;
            if (ringSize <= 0)
                return;

            for (int i = 0; i < ringSize; i++)
            {
                Vector3 pos = CellPosition(i, ringSize);
                Gizmos.color = cellColor;
                Gizmos.DrawWireSphere(pos, cellRadius);
            }

            if (_lastRecord.HasValue)
            {
                int? target = GetLastMoveTarget(_lastRecord.Value);
                if (target.HasValue)
                {
                    Gizmos.color = lastMoveColor;
                    Gizmos.DrawWireSphere(CellPosition(target.Value, ringSize), playerRadius * 1.1f);
                }
            }

#if UNITY_EDITOR
            Handles.color = Color.white;
            Handles.Label(transform.position + Vector3.up * 1.5f, BuildInfoText());
#endif
        }

        private void BuildPawns()
        {
            if (_pawnA == null)
                _pawnA = CreatePiece(pawnPrefab, PrimitiveType.Sphere, "PawnA", playerAColor);
            if (_pawnB == null)
                _pawnB = CreatePiece(pawnPrefab, PrimitiveType.Sphere, "PawnB", playerBColor);
        }

        private void BuildCells()
        {
            if (_cellMarkers != null)
            {
                foreach (var marker in _cellMarkers)
                {
                    if (marker != null)
                        Destroy(marker.gameObject);
                }
            }

            int ringSize = _state?.Rules.ringSize ?? 0;
            if (ringSize <= 0)
                return;

            _cellMarkers = new CellMarker[ringSize];
            for (int i = 0; i < ringSize; i++)
            {
                var cellObj = new GameObject($"Cell_{i}");
                cellObj.transform.SetParent(transform, false);
                cellObj.transform.position = CellPosition(i, ringSize);

                var collider = cellObj.AddComponent<SphereCollider>();
                collider.radius = cellRadius * 2f;

                var marker = cellObj.AddComponent<CellMarker>();
                marker.Index = i;
                _cellMarkers[i] = marker;
            }
        }

        private void RefreshPieces()
        {
            if (_state == null)
                return;

            int ringSize = _state.Rules.ringSize;
            if (_pawnA != null)
                _pawnA.transform.position = CellPosition(_state.PosA, ringSize) + Vector3.up * 0.2f;
            if (_pawnB != null)
                _pawnB.transform.position = CellPosition(_state.PosB, ringSize) + Vector3.up * 0.2f;

            int chipCount = 0;
            for (int i = 0; i < _state.HasChip.Length; i++)
                if (_state.HasChip[i]) chipCount++;

            EnsureChipPool(chipCount);

            int used = 0;
            for (int i = 0; i < _state.HasChip.Length; i++)
            {
                if (!_state.HasChip[i]) continue;

                var chip = _chipPool[used++];
                chip.SetActive(true);
                chip.transform.position = CellPosition(i, ringSize) + Vector3.up * (chipHeight * 0.5f);
            }

            for (int i = used; i < _chipPool.Count; i++)
                _chipPool[i].SetActive(false);
        }

        private void EnsureChipPool(int count)
        {
            while (_chipPool.Count < count)
            {
                var chip = CreatePiece(chipPrefab, PrimitiveType.Cylinder, $"Chip_{_chipPool.Count}", chipColor);
                chip.transform.localScale = new Vector3(cellRadius * 2f, chipHeight * 0.5f, cellRadius * 2f);
                _chipPool.Add(chip);
            }
        }

        private GameObject CreatePiece(GameObject prefab, PrimitiveType primitiveType, string name, Color color)
        {
            GameObject obj = prefab != null ? Instantiate(prefab, transform) : GameObject.CreatePrimitive(primitiveType);
            obj.name = name;
            obj.transform.SetParent(transform, false);

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;

            return obj;
        }

        private Vector3 CellPosition(int index, int ringSize)
        {
            float angle = Mathf.PI * 2f * index / ringSize;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
            return transform.position + offset;
        }

        private int? GetLastMoveTarget(MoveRecord record)
        {
            if (!record.Move.HasValue) return null;

            var move = record.Move.Value;
            if (move.Kind == MoveKind.PlaceChip)
                return record.ChipCell;

            return record.PlayerId == PlayerId.A ? record.PosAAfter : record.PosBAfter;
        }

        private string BuildInfoText()
        {
            string lastMoveText = _lastRecord?.Move?.ToString() ?? "None";
            string chips = $"A:{_state.ChipsInHandA}  B:{_state.ChipsInHandB}";
            string status = _state.IsFinished
                ? $"Finished ({_state.Winner})"
                : $"Turn {_state.TurnIndex} - {_state.CurrentPlayer}";

            string endReason = _lastRecord?.EndReason.ToString() ?? "None";

            return $"Diceforge Debug\n{status}\nRoll: {_state.CurrentRoll}\nChips: {chips}\nLast Move: {lastMoveText}\nEnd: {endReason}";
        }
    }
}
