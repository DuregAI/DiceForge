using System.Collections;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Diceforge.View
{
    public sealed class BoardLayoutTokenMover : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoardLayout layout;
        [SerializeField] private Transform tokenRoot;
        [SerializeField] private Tilemap positionTilemap;

        [Header("Movement")]
        [SerializeField] private float heightOffset = 0.05f;
        [SerializeField] private float tilemapDepthOffset = 0.2f;
        [SerializeField] private float moveDuration = 0.25f;
        [SerializeField] private bool rotateAlongPath = true;
        [SerializeField] private bool logMovementState;

        [Header("Runtime")]
        [SerializeField] private int currentCellId;
        [SerializeField] private Vector3 visualOffset;

        private Coroutine _moveRoutine;
        private Coroutine _moveStepsRoutine;
        private UnitAnimationController _animationController;
        private int _movementVisualsRefCount;
        private bool _suppressStopAtMoveEnd;
        private bool _hasPlacement;
        private IBoardGeometry _geometry;
        private bool _diorama;
        private bool _hopPresentation;
        private Transform _hopVisual;
        private float presentationJumpHeight = .32f;
        public void SetJumpHeight(float height) => presentationJumpHeight = Mathf.Max(0, height);
        public void SetGeometry(IBoardGeometry geometry) { _geometry = geometry; _diorama = geometry is DioramaBoard; }
        public void SetHopPresentation(bool enabled)
        {
            _hopPresentation = enabled;
            if (!enabled || _hopVisual != null || tokenRoot == null) return;
            // Keep the logical root, collider, formation scale and Animator's bone paths untouched.
            Transform model = _animationController?.Animator != null ? _animationController.Animator.transform : tokenRoot.Find("Model");
            if (model == null || model == tokenRoot) return;
            while (model.parent != tokenRoot && model.parent != null) model = model.parent;
            if (model.parent != tokenRoot) return;
            _hopVisual = new GameObject("Hop presentation").transform;
            _hopVisual.SetParent(tokenRoot, false);
            model.SetParent(_hopVisual, false);
        }

        private void ResetHopVisual()
        {
            if (_hopVisual == null) return;
            _hopVisual.localScale = Vector3.one;
            _hopVisual.localRotation = Quaternion.identity;
        }
        public void MoveToWorld(Vector3 destination, int resolvedCellId, float duration)
        {
            CancelAllMovement();
            _hasPlacement=true;
            BeginMovementVisuals();
            _moveRoutine=StartCoroutine(MoveRoutine(destination+visualOffset,resolvedCellId,Mathf.Max(.05f,duration)));
        }

        public int CurrentCellId => currentCellId;
        public bool IsAnimating => _moveRoutine != null || _moveStepsRoutine != null;

        public void SetVisualOffset(Vector3 offset)
        {
            visualOffset = offset;
        }

        public void SetLayout(BoardLayout boardLayout)
        {
            layout = boardLayout;
        }

        public void SetPositionTilemap(Tilemap tilemap)
        {
            positionTilemap = tilemap;
        }

        private void Awake()
        {
            if (tokenRoot == null)
                tokenRoot = transform;

            _animationController = GetComponent<UnitAnimationController>() ?? GetComponentInChildren<UnitAnimationController>(true);
            if (_animationController == null)
                _animationController = gameObject.AddComponent<UnitAnimationController>();
        }

        private void Start()
        {
            // A view may already have placed this token on the bar or started its first move.
            if (!_hasPlacement)
                SnapTo(currentCellId);
        }

        private void OnDisable()
        {
            CancelAllMovement();
            _movementVisualsRefCount = 0;
            _animationController?.SetMoving(false);
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
            _hasPlacement = true;
        }

        public void SnapToWorld(Vector3 worldPosition, int resolvedCellId = -1)
        {
            CancelAllMovement();

            if (tokenRoot == null)
                return;

            tokenRoot.position = worldPosition + ResolvePresentationOffset();
            currentCellId = resolvedCellId;
            _hasPlacement = true;
        }

        public void MoveTo(int cellId)
        {
            if (!TryGetCellWorldPosition(cellId, out Vector3 targetPosition, out int resolvedCellId))
                return;

            StopMoveRoutine();

            if (tokenRoot == null)
                return;

            _hasPlacement = true;

            float duration = Mathf.Max(0f, moveDuration);
            if (duration <= Mathf.Epsilon)
            {
                tokenRoot.position = targetPosition;
                currentCellId = resolvedCellId;
                return;
            }

            if (!_suppressStopAtMoveEnd)
                BeginMovementVisuals();

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

            // A coroutine that finishes before its first yield would leave a stale handle.
            if (moveDuration <= Mathf.Epsilon)
            {
                SnapTo(WrapCellId(currentCellId + steps, minCellId, maxCellId));
                return;
            }

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
            int nextCellId = WrapCellId(currentCellId + delta, minCellId, maxCellId);
            MoveTo(nextCellId);
        }

        private IEnumerator MoveRoutine(Vector3 targetPosition, int targetCellId, float duration)
        {
            Vector3 startPosition = tokenRoot.position;
            Quaternion startRotation = tokenRoot.rotation;
            Quaternion targetRotation = startRotation;
            bool polishedHop = _diorama && _hopPresentation;

            if (rotateAlongPath)
            {
                Vector3 flatDirection = targetPosition - startPosition;
                flatDirection.y = 0f;
                if (flatDirection.sqrMagnitude > 0.0001f)
                    targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            }
            if (!polishedHop) tokenRoot.rotation = targetRotation;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (polishedHop && !DioramaBoard.ReducedMotion)
                {
                    DioramaHopMotion.Sample(t, out float progress, out float lift, out Vector3 scale, out float lean);
                    tokenRoot.position = Vector3.Lerp(startPosition, targetPosition, progress) + Vector3.up * (lift * presentationJumpHeight);
                    tokenRoot.rotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.SmoothStep(0f, 1f, t / .4f));
                    if (_hopVisual != null)
                    {
                        _hopVisual.localScale = scale;
                        _hopVisual.localRotation = Quaternion.Euler(lean, 0f, 0f);
                    }
                }
                else
                {
                    ResetHopVisual();
                    tokenRoot.position = Vector3.Lerp(startPosition, targetPosition, t);
                    tokenRoot.rotation = targetRotation;
                    if (_diorama && !DioramaBoard.ReducedMotion)
                        tokenRoot.position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * presentationJumpHeight);
                }
                yield return null;
            }

            tokenRoot.position = targetPosition;
            tokenRoot.rotation = targetRotation;
            ResetHopVisual();
            currentCellId = targetCellId;
            _moveRoutine = null;

            if (!_suppressStopAtMoveEnd)
                EndMovementVisuals();
        }

        private IEnumerator MoveStepsRoutine(int steps, int minCellId, int maxCellId)
        {
            int direction = steps > 0 ? 1 : -1;
            int stepCount = Mathf.Abs(steps);
            float originalDuration = moveDuration;

            BeginMovementVisuals();
            _suppressStopAtMoveEnd = true;

            for (int i = 0; i < stepCount; i++)
            {
                int nextCellId = WrapCellId(currentCellId + direction, minCellId, maxCellId);
                if (_diorama) moveDuration = Mathf.Min(.32f, 1.45f / stepCount);
                MoveTo(nextCellId);
                moveDuration = originalDuration;

                while (_moveRoutine != null)
                    yield return null;
            }

            _suppressStopAtMoveEnd = false;
            moveDuration = originalDuration;
            EndMovementVisuals();
            _moveStepsRoutine = null;
        }

        private Vector3 ResolveWorldPosition(CellData cell)
        {
            if (_geometry != null) return _geometry.CellPosition(cell.cellId) + ResolvePresentationOffset();
            if (positionTilemap != null)
                return positionTilemap.GetCellCenterWorld(cell.gridPos) + ResolvePresentationOffset();

            return cell.worldPos + ResolvePresentationOffset();
        }

        private Vector3 ResolvePresentationOffset()
        {
            if (_diorama) return visualOffset;
            // Mesh units render as opaque 3D geometry, so push them slightly behind the tilemap plane.
            // This keeps them above the board visually while allowing foreground sprite decor to overlap.
            Vector3 depthAxis = positionTilemap != null ? positionTilemap.transform.forward : Vector3.forward;
            return Vector3.up * heightOffset + depthAxis * tilemapDepthOffset + visualOffset;
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

                worldPosition = ResolveWorldPosition(cell);
                resolvedCellId = cell.cellId;
                return true;
            }

            Debug.LogWarning($"BoardLayoutTokenMover could not find cellId {clampedCellId} in layout '{layout.name}'.", this);
            return false;
        }


        private static int WrapCellId(int requestedCellId, int minCellId, int maxCellId)
        {
            int range = maxCellId - minCellId + 1;
            if (range <= 0)
                return minCellId;

            int normalized = (requestedCellId - minCellId) % range;
            if (normalized < 0)
                normalized += range;

            return minCellId + normalized;
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
            ResetHopVisual();
            if (!_suppressStopAtMoveEnd)
                EndMovementVisuals();
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

            _suppressStopAtMoveEnd = false;
            ResetHopVisual();
            _movementVisualsRefCount = 0;
            _animationController?.SetMoving(false);
        }

        private void BeginMovementVisuals()
        {
            _movementVisualsRefCount++;
            if (_movementVisualsRefCount == 1)
            {
                _animationController?.SetMoving(true);
                if (logMovementState)
                    Debug.Log($"[BoardLayoutTokenMover] {name} movement start.", this);
            }
        }

        private void EndMovementVisuals()
        {
            _movementVisualsRefCount = Mathf.Max(0, _movementVisualsRefCount - 1);
            if (_movementVisualsRefCount == 0)
            {
                _animationController?.SetMoving(false);
                if (logMovementState)
                    Debug.Log($"[BoardLayoutTokenMover] {name} movement end.", this);
            }
        }
    }
}

