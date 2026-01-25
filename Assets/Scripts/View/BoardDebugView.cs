using Diceforge.Core;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] private float stoneHeight = 0.2f;
        [SerializeField] private float stoneSpreadRadius = 0.18f;
        [SerializeField] private float stoneRingSpacing = 0.08f;

        [Header("Colors")]
        [SerializeField] private Color cellColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        [SerializeField] private Color playerAColor = new Color(0.2f, 0.6f, 1f, 0.9f);
        [SerializeField] private Color playerBColor = new Color(1f, 0.4f, 0.2f, 0.9f);
        [SerializeField] private Color lastMoveColor = new Color(1f, 0.9f, 0.3f, 0.9f);

        [Header("Prefabs")]
        [SerializeField] private GameObject stonePrefabA;
        [SerializeField] private GameObject stonePrefabB;

        private GameState _state;
        private MatchLog _log;
        private MoveRecord? _lastRecord;
        private readonly System.Collections.Generic.List<GameObject> _stonePoolA = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<GameObject> _stonePoolB = new System.Collections.Generic.List<GameObject>();
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

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            Vector2 position = mouse.position.ReadValue();
            if (Physics.Raycast(_camera.ScreenPointToRay(position), out var hit))
            {
                var marker = hit.collider.GetComponent<CellMarker>();
                if (marker != null)
                {
                    _cellSelectionEnabled = false;
                    OnCellClicked?.Invoke(marker.Index);
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
            int totalA = 0;
            int totalB = 0;
            for (int i = 0; i < ringSize; i++)
            {
                totalA += _state.StonesAByCell[i];
                totalB += _state.StonesBByCell[i];
            }

            EnsureStonePool(_stonePoolA, stonePrefabA, playerAColor, totalA, "StoneA");
            EnsureStonePool(_stonePoolB, stonePrefabB, playerBColor, totalB, "StoneB");

            int usedA = 0;
            int usedB = 0;
            for (int cell = 0; cell < ringSize; cell++)
            {
                usedA = PlaceStonesOnCell(cell, _state.StonesAByCell[cell], ringSize, _stonePoolA, usedA);
                usedB = PlaceStonesOnCell(cell, _state.StonesBByCell[cell], ringSize, _stonePoolB, usedB);
            }

            for (int i = usedA; i < _stonePoolA.Count; i++)
                _stonePoolA[i].SetActive(false);
            for (int i = usedB; i < _stonePoolB.Count; i++)
                _stonePoolB[i].SetActive(false);
        }

        private int PlaceStonesOnCell(int cell, int count, int ringSize, System.Collections.Generic.List<GameObject> pool, int used)
        {
            if (count <= 0) return used;

            Vector3 center = CellPosition(cell, ringSize);
            for (int i = 0; i < count; i++)
            {
                var stone = pool[used++];
                stone.SetActive(true);
                Vector3 offset = CalculateStoneOffset(i, count);
                stone.transform.position = center + offset;
            }

            return used;
        }

        private Vector3 CalculateStoneOffset(int index, int count)
        {
            if (count <= 1)
                return Vector3.up * (stoneHeight * 0.5f);

            int ringIndex = index / 6;
            int indexInRing = index % 6;
            int ringCount = Mathf.Min(6, count - ringIndex * 6);
            float radius = stoneSpreadRadius + ringIndex * stoneRingSpacing;
            float angle = Mathf.PI * 2f * indexInRing / Mathf.Max(1, ringCount);
            Vector3 planar = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            float height = stoneHeight * 0.5f + ringIndex * (stoneHeight * 0.15f);
            return planar + Vector3.up * height;
        }

        private void EnsureStonePool(System.Collections.Generic.List<GameObject> pool, GameObject prefab, Color color, int count, string namePrefix)
        {
            while (pool.Count < count)
            {
                var stone = CreatePiece(prefab, PrimitiveType.Sphere, $"{namePrefix}_{pool.Count}", color);
                stone.transform.localScale = Vector3.one * cellRadius * 1.4f;
                pool.Add(stone);
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
            return record.ToCell;
        }

        private string BuildInfoText()
        {
            string lastMoveText = _lastRecord?.Move?.ToString() ?? "None";
            string chips = $"A:{_state.StonesInHandA}  B:{_state.StonesInHandB}";
            string status = _state.IsFinished
                ? $"Finished ({_state.Winner})"
                : $"Turn {_state.TurnIndex} - {_state.CurrentPlayer}";

            string endReason = _lastRecord?.EndReason.ToString() ?? "None";

            return $"Diceforge Debug\n{status}\nRoll: {_state.CurrentRoll}\nHand: {chips}\nLast Move: {lastMoveText}\nEnd: {endReason}";
        }
    }
}
