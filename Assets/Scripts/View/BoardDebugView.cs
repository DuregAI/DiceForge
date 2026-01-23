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

        [Header("Colors")]
        [SerializeField] private Color cellColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        [SerializeField] private Color blockedColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);
        [SerializeField] private Color playerAColor = new Color(0.2f, 0.6f, 1f, 0.9f);
        [SerializeField] private Color playerBColor = new Color(1f, 0.4f, 0.2f, 0.9f);
        [SerializeField] private Color lastMoveColor = new Color(1f, 0.9f, 0.3f, 0.9f);

        private GameState _state;
        private MatchLog _log;
        private MoveRecord? _lastRecord;

        public void HandleMatchStarted(GameState state, MatchLog log)
        {
            _state = state;
            _log = log;
            _lastRecord = null;
        }

        public void HandleMoveApplied(MoveRecord record)
        {
            _lastRecord = record;
        }

        public void HandleMatchEnded(GameState state)
        {
            _state = state;
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
                Gizmos.color = _state.Blocked[i] ? blockedColor : cellColor;
                Gizmos.DrawSphere(pos, cellRadius);
            }

            DrawPlayer(_state.PosA, ringSize, playerAColor);
            DrawPlayer(_state.PosB, ringSize, playerBColor);

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

        private void DrawPlayer(int cellIndex, int ringSize, Color color)
        {
            Vector3 pos = CellPosition(cellIndex, ringSize);
            Gizmos.color = color;
            Gizmos.DrawSphere(pos + Vector3.up * 0.2f, playerRadius);
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
            if (move.Kind == MoveKind.PlaceBlock)
                return record.BlockCell;

            return record.PlayerId == PlayerId.A ? record.PosAAfter : record.PosBAfter;
        }

        private string BuildInfoText()
        {
            string lastMoveText = _lastRecord?.Move?.ToString() ?? "None";
            string blocks = $"A:{_state.BlocksLeftA}  B:{_state.BlocksLeftB}";
            string status = _state.IsFinished
                ? $"Finished ({_state.Winner})"
                : $"Turn {_state.TurnIndex} - {_state.CurrentPlayer}";

            string endReason = _lastRecord?.EndReason.ToString() ?? "None";

            return $"Diceforge Debug\n{status}\nBlocks: {blocks}\nLast Move: {lastMoveText}\nEnd: {endReason}";
        }
    }
}
