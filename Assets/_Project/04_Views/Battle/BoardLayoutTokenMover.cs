using System.Collections;
using Diceforge.Map;
using UnityEngine;

namespace Diceforge.View
{
    public sealed class BoardLayoutTokenMover : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardLayout layout;
        [SerializeField] private Transform tokenRoot;

        [Header("Movement")]
        [SerializeField] private float heightOffset = 0.05f;
        [SerializeField] private float moveDuration = 0.25f;
        [SerializeField] private bool rotateAlongPath = true;

        [Header("Runtime")]
        [SerializeField] private int currentCellId;

        private Coroutine _moveRoutine;
        private Coroutine _moveStepsRoutine;

        public int CurrentCellId => currentCellId;

        private void Start()
        {
            SnapTo(0);
        }

        private void OnDisable()
        {
            CancelAllMovement();
        }

        public void SnapTo(int cellId)
        {
            if (!TryGetCellWorldPosition(cellId, out Vector3 targetPosition, out int resolvedCellId))
                return;

            CancelAllMovement();

            if (tokenRoot == null)
                return;

            tokenRoot.position = targetPosition;
            currentCellId = resolvedCellId;
        }

        public void MoveTo(int cellId)
        {
            if (!TryGetCellWorldPosition(cellId, out Vector3 targetPosition, out int resolvedCellId))
                return;

            StopMoveRoutine();

            if (tokenRoot == null)
                return;

            float duration = Mathf.Max(0f, moveDuration);
            if (duration <= Mathf.Epsilon)
            {
                tokenRoot.position = targetPosition;
                currentCellId = resolvedCellId;
                return;
            }

            _moveRoutine = StartCoroutine(MoveRoutine(targetPosition, resolvedCellId, duration));
        }

        public void MoveSteps(int steps)
        {
            if (steps == 0)
                return;

            if (_moveRoutine != null || _moveStepsRoutine != null)
                return;

            if (!TryGetCellIdBounds(out int minCellId, out int maxCellId))
                return;

            _moveStepsRoutine = StartCoroutine(MoveStepsRoutine(steps, minCellId, maxCellId));
        }

        public void Step(int delta)
        {
            if (layout == null || layout.cells == null || layout.cells.Count == 0)
            {
                Debug.LogWarning("BoardLayoutTokenMover has no board layout cells to step through.", this);
                return;
            }

            int minCellId = layout.cells[0].cellId;
            int maxCellId = layout.cells[layout.cells.Count - 1].cellId;
            int nextCellId = Mathf.Clamp(currentCellId + delta, minCellId, maxCellId);
            MoveTo(nextCellId);
        }

        private IEnumerator MoveRoutine(Vector3 targetPosition, int targetCellId, float duration)
        {
            Vector3 startPosition = tokenRoot.position;

            if (rotateAlongPath)
            {
                Vector3 flatDirection = targetPosition - startPosition;
                flatDirection.y = 0f;
                if (flatDirection.sqrMagnitude > 0.0001f)
                    tokenRoot.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                tokenRoot.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            tokenRoot.position = targetPosition;
            currentCellId = targetCellId;
            _moveRoutine = null;
        }

        private IEnumerator MoveStepsRoutine(int steps, int minCellId, int maxCellId)
        {
            int direction = steps > 0 ? 1 : -1;
            int stepCount = Mathf.Abs(steps);

            for (int i = 0; i < stepCount; i++)
            {
                int nextCellId = Mathf.Clamp(currentCellId + direction, minCellId, maxCellId);
                if (nextCellId == currentCellId)
                    break;

                MoveTo(nextCellId);

                while (_moveRoutine != null)
                    yield return null;
            }

            _moveStepsRoutine = null;
        }

        private bool TryGetCellWorldPosition(int requestedCellId, out Vector3 worldPosition, out int resolvedCellId)
        {
            worldPosition = default;
            resolvedCellId = currentCellId;

            if (layout == null)
            {
                Debug.LogWarning("BoardLayoutTokenMover is missing BoardLayout reference.", this);
                return false;
            }

            if (layout.cells == null || layout.cells.Count == 0)
            {
                Debug.LogWarning("BoardLayoutTokenMover layout has no cells.", this);
                return false;
            }

            if (!TryGetCellIdBounds(out int minCellId, out int maxCellId))
                return false;

            int clampedCellId = Mathf.Clamp(requestedCellId, minCellId, maxCellId);

            for (int i = 0; i < layout.cells.Count; i++)
            {
                CellData cell = layout.cells[i];
                if (cell.cellId != clampedCellId)
                    continue;

                worldPosition = cell.worldPos + Vector3.up * heightOffset;
                resolvedCellId = cell.cellId;
                return true;
            }

            Debug.LogWarning($"BoardLayoutTokenMover could not find cellId {clampedCellId} in layout '{layout.name}'.", this);
            return false;
        }

        private bool TryGetCellIdBounds(out int minCellId, out int maxCellId)
        {
            minCellId = 0;
            maxCellId = 0;

            if (layout == null)
            {
                Debug.LogWarning("BoardLayoutTokenMover is missing BoardLayout reference.", this);
                return false;
            }

            if (layout.cells == null || layout.cells.Count == 0)
            {
                Debug.LogWarning("BoardLayoutTokenMover layout has no cells.", this);
                return false;
            }

            minCellId = layout.cells[0].cellId;
            maxCellId = layout.cells[layout.cells.Count - 1].cellId;
            return true;
        }

        private void StopMoveRoutine()
        {
            if (_moveRoutine == null)
                return;

            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }

        public void CancelAllMovement()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            if (_moveStepsRoutine != null)
            {
                StopCoroutine(_moveStepsRoutine);
                _moveStepsRoutine = null;
            }
        }
    }
}
