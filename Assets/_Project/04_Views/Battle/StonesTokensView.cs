using System;
using System.Collections.Generic;
using Diceforge.Core;
using Diceforge.Map;
using Diceforge.TokenPlacement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace Diceforge.View
{
    /// <summary>
    /// Multi-token board renderer: one token GameObject per logical stone.
    /// </summary>
    public sealed class StonesTokensView : MonoBehaviour
    {
        private sealed class TokenBinding
        {
            public string stoneId;
            public PlayerId player;
            public int stoneIndex;
            public GameObject root;
            public BoardLayoutTokenMover mover;
            public TokenAssignment placement;
            public bool assigned;
        }

        private readonly List<TokenBinding> _tokensA = new();
        private readonly List<TokenBinding> _tokensB = new();
        private BoardLayout _layout;
        private Tilemap _positionTilemap;
        private Transform _unitsRoot;
        private GameObject _teamAUnitPrefab;
        private GameObject _teamBUnitPrefab;
        private Color _teamAColor;
        private Color _teamBColor;
        private bool _configured;

        [Header("Bar Placement")]
        [SerializeField] private float barSideOffsetX = 0.28f;
        [SerializeField] private float barStackStepY = 0.045f;
        [SerializeField] private float barStackStepZ = 0.03f;

        public void Configure(BoardLayout layout, Tilemap positionTilemap, Transform unitsRoot, GameObject teamAUnitPrefab, GameObject teamBUnitPrefab, Color teamAColor, Color teamBColor)
        {
            _layout = layout;
            _positionTilemap = positionTilemap;
            _unitsRoot = unitsRoot;
            _teamAUnitPrefab = teamAUnitPrefab;
            _teamBUnitPrefab = teamBUnitPrefab != null ? teamBUnitPrefab : teamAUnitPrefab;
            _teamAColor = teamAColor;
            _teamBColor = teamBColor;
            _configured = _layout != null
                && _unitsRoot != null
                && _teamAUnitPrefab != null
                && _teamBUnitPrefab != null;
        }

        public void BuildTokensFromMatchState(GameState matchState)
        {
            if (!_configured || matchState == null)
                return;

            int totalA = CountTotalStones(matchState, PlayerId.A);
            int totalB = CountTotalStones(matchState, PlayerId.B);

            EnsurePool(PlayerId.A, totalA);
            EnsurePool(PlayerId.B, totalB);

            Debug.Log($"[StonesTokensView] BuildTokensFromMatchState totalA={totalA} totalB={totalB} pooledA={_tokensA.Count} pooledB={_tokensB.Count}", this);
            RestoreFromState(matchState);
        }

        public bool HasActiveTokens()
        {
            for (int i = 0; i < _tokensA.Count; i++)
                if (_tokensA[i].root.activeSelf)
                    return true;

            for (int i = 0; i < _tokensB.Count; i++)
                if (_tokensB[i].root.activeSelf)
                    return true;

            return false;
        }

        public bool IsAnimating
        {
            get
            {
                return HasAnimatingToken(_tokensA) || HasAnimatingToken(_tokensB);
            }
        }

        public void HandleMoveApplied(MoveRecord record, GameState state, bool animate, string preferredMovedTokenName = null)
        {
            if (!_configured || state == null)
                return;

            TokenCounts counts = ReadCounts(state);
            TokenMove? move = null;
            if (record.Move.HasValue && record.ApplyResult != ApplyResult.Illegal)
            {
                MoveKind kind = record.Move.Value.Kind;
                move = new TokenMove((int)record.PlayerId,
                    kind == MoveKind.EnterFromBar ? TokenLocation.Bar : TokenLocation.Cell,
                    record.FromCell ?? -1,
                    kind == MoveKind.BearOff ? TokenLocation.BorneOff : TokenLocation.Cell,
                    record.ToCell ?? -1);
            }

            PlacementResult result = TokenPlacementResolver.Apply(ReadAssignments(), move,
                FindPreferredId(preferredMovedTokenName), counts);
            if (!result.Success)
            {
                Debug.LogWarning($"[StonesTokensView] Placement mismatch: {result.Error} Restoring from GameState.", this);
                RestoreFromState(state);
                return;
            }

            // Repeated synchronization must not interrupt an animation already in progress.
            if (result.MovedId == null)
                return;

            ApplyAssignments(result.Assignments, counts, animate ? result.MovedId : null);
            if (animate && record.ToCell.HasValue)
                AnimateMove(FindToken(result.MovedId), record, state.Rules.boardSize);
        }

        private void RestoreFromState(GameState state)
        {
            CancelAllMovement(_tokensA);
            CancelAllMovement(_tokensB);
            TokenCounts counts = ReadCounts(state);
            EnsurePool(PlayerId.A, counts.Total(0));
            EnsurePool(PlayerId.B, counts.Total(1));
            var identities = new List<TokenAssignment>();
            AddIdentities(_tokensA, counts.Total(0), identities);
            AddIdentities(_tokensB, counts.Total(1), identities);
            PlacementResult result = TokenPlacementResolver.Initialize(identities, counts);
            if (!result.Success)
                throw new InvalidOperationException(result.Error);
            ApplyAssignments(result.Assignments, counts, null);
        }

        private static TokenCounts ReadCounts(GameState state) => new TokenCounts(
            state.StonesAByCell.ToArray(), state.StonesBByCell.ToArray(),
            state.GetBarCount(PlayerId.A), state.GetBarCount(PlayerId.B),
            state.GetBorneOff(PlayerId.A), state.GetBorneOff(PlayerId.B));

        private static void AddIdentities(List<TokenBinding> tokens, int count, List<TokenAssignment> result)
        {
            for (int i = 0; i < count; i++)
                result.Add(new TokenAssignment(tokens[i].stoneId, (int)tokens[i].player,
                    tokens[i].stoneIndex, TokenLocation.BorneOff));
        }

        private List<TokenAssignment> ReadAssignments()
        {
            var result = new List<TokenAssignment>(_tokensA.Count + _tokensB.Count);
            foreach (TokenBinding token in _tokensA)
                if (token.assigned) result.Add(token.placement);
            foreach (TokenBinding token in _tokensB)
                if (token.assigned) result.Add(token.placement);
            return result;
        }

        private string FindPreferredId(string rootName)
        {
            if (string.IsNullOrEmpty(rootName)) return null;
            foreach (TokenBinding token in _tokensA)
                if (string.Equals(token.root.name, rootName, StringComparison.Ordinal)) return token.stoneId;
            foreach (TokenBinding token in _tokensB)
                if (string.Equals(token.root.name, rootName, StringComparison.Ordinal)) return token.stoneId;
            return null;
        }

        private TokenBinding FindToken(string id)
        {
            foreach (TokenBinding token in _tokensA)
                if (token.stoneId == id) return token;
            foreach (TokenBinding token in _tokensB)
                if (token.stoneId == id) return token;
            return null;
        }

        private void ApplyAssignments(IReadOnlyList<TokenAssignment> assignments, TokenCounts counts, string animatedId)
        {
            // Validate the entire object mapping before touching any object.
            var resolved = new List<TokenBinding>(assignments.Count);
            foreach (TokenAssignment assignment in assignments)
            {
                TokenBinding token = FindToken(assignment.Id);
                if (token == null) throw new InvalidOperationException($"Missing visual token '{assignment.Id}'.");
                resolved.Add(token);
            }

            foreach (TokenBinding token in _tokensA) token.assigned = false;
            foreach (TokenBinding token in _tokensB) token.assigned = false;
            Vector3 barCenter = CalculateBarCenterWorld();
            var stackIndices = new int[2, counts.BoardSize + 1];
            for (int i = 0; i < assignments.Count; i++)
            {
                TokenAssignment assignment = assignments[i];
                TokenBinding token = resolved[i];
                token.assigned = true;
                token.placement = assignment; // Logical destination is committed before animation.
                bool visible = assignment.Location != TokenLocation.BorneOff;
                if (!visible) token.mover.CancelAllMovement();
                token.root.SetActive(visible);
                if (!visible) continue;

                int slot = assignment.Location == TokenLocation.Cell ? assignment.Cell : counts.BoardSize;
                int index = stackIndices[assignment.Player, slot]++;
                int cellCount = assignment.Location == TokenLocation.Cell
                    ? counts.Get(assignment.Player, TokenLocation.Cell, assignment.Cell) : 1;
                var presentation = token.root.GetComponent<BoardUnitFacing>();
                token.mover.SetVisualOffset(presentation != null
                    ? presentation.ArrangeInCell(index, cellCount)
                    : assignment.Location == TokenLocation.Cell
                        ? CalculateFormationOffset(index, cellCount) : Vector3.zero);
                if (assignment.Id == animatedId) continue;
                if (assignment.Location == TokenLocation.Bar)
                    token.mover.SnapToWorld(CalculateBarStoneWorldPosition(token.player, barCenter, index));
                else
                    token.mover.SnapTo(assignment.Cell);
            }
            HideUnused(_tokensA);
            HideUnused(_tokensB);
        }

        private static void AnimateMove(TokenBinding token, MoveRecord record, int boardSize)
        {
            token.mover.CancelAllMovement();
            if (record.Move.Value.Kind != MoveKind.EnterFromBar && record.FromCell.HasValue &&
                record.PipUsed.HasValue && token.mover.CurrentCellId == record.FromCell.Value &&
                BoardMoveAnimationResolver.TryResolveSignedSteps(boardSize, record.FromCell.Value,
                    record.ToCell.Value, record.PipUsed.Value, out int steps) && steps != 0)
                token.mover.MoveSteps(steps);
            else
                token.mover.MoveTo(record.ToCell.Value);
        }

        private static void HideUnused(List<TokenBinding> tokens)
        {
            foreach (TokenBinding token in tokens)
            {
                if (token.assigned) continue;
                token.mover.CancelAllMovement();
                token.root.SetActive(false);
            }
        }

        private static void CancelAllMovement(List<TokenBinding> tokens)
        {
            foreach (TokenBinding token in tokens) token.mover.CancelAllMovement();
        }

        private static bool HasAnimatingToken(List<TokenBinding> tokens)
        {
            foreach (TokenBinding token in tokens)
                if (token.mover != null && token.mover.IsAnimating) return true;
            return false;
        }

        private Vector3 CalculateBarCenterWorld()
        {
            if (_layout == null || _layout.cells == null || _layout.cells.Count == 0)
            {
                if (_unitsRoot != null)
                    return _unitsRoot.position;
                return transform.position;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < _layout.cells.Count; i++)
            {
                CellData cell = _layout.cells[i];
                Vector3 world = _positionTilemap != null
                    ? _positionTilemap.GetCellCenterWorld(cell.gridPos)
                    : cell.worldPos;

                sum += world;
                count++;
            }

            if (count == 0)
            {
                if (_unitsRoot != null)
                    return _unitsRoot.position;
                return transform.position;
            }

            return sum / count;
        }

        private Vector3 CalculateBarStoneWorldPosition(PlayerId player, Vector3 centerWorld, int stackIndex)
        {
            float side = player == PlayerId.A ? -1f : 1f;
            return centerWorld + new Vector3(side * barSideOffsetX, stackIndex * barStackStepY, stackIndex * barStackStepZ);
        }

        private void EnsurePool(PlayerId player, int requiredCount)
        {
            List<TokenBinding> tokens = player == PlayerId.A ? _tokensA : _tokensB;
            Color color = player == PlayerId.A ? _teamAColor : _teamBColor;
            string prefix = player == PlayerId.A ? "StoneA" : "StoneB";
            GameObject unitPrefab = player == PlayerId.A ? _teamAUnitPrefab : _teamBUnitPrefab;

            while (tokens.Count < requiredCount)
            {
                int index = tokens.Count;
                GameObject instance = Instantiate(unitPrefab, _unitsRoot);
                instance.name = $"{prefix}_{index:D2}";

                BoardLayoutTokenMover mover = instance.GetComponent<BoardLayoutTokenMover>();
                if (mover == null)
                {
                    Debug.LogWarning($"[StonesTokensView] Token '{instance.name}' missing BoardLayoutTokenMover. Adding one.", instance);
                    mover = instance.AddComponent<BoardLayoutTokenMover>();
                }

                mover.SetLayout(_layout);
                mover.SetPositionTilemap(_positionTilemap);

                ApplyTeamColor(instance, color);
                EnsureSorting(instance);

                TokenBinding token = new TokenBinding
                {
                    stoneId = $"{player}-{index}",
                    stoneIndex = index,
                    player = player,
                    root = instance,
                    mover = mover,
                    assigned = false
                };

                instance.SetActive(false);
                tokens.Add(token);
            }
        }

        private static int CountTotalStones(GameState state, PlayerId player)
        {
            int total = state.GetBorneOff(player) + state.GetBarCount(player);
            ReadOnlySpan<int> counts = player == PlayerId.A ? state.StonesAByCell : state.StonesBByCell;
            for (int i = 0; i < counts.Length; i++)
                total += counts[i];

            return total;
        }

        private static Vector3 CalculateFormationOffset(int indexInCell, int cellCount)
        {
            if (cellCount <= 1)
                return Vector3.zero;

            float radius = 0.09f + Mathf.Min(0.12f, cellCount * 0.006f);
            float angle = (Mathf.PI * 2f * indexInCell) / Mathf.Max(1, cellCount);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }

        private static void ApplyTeamColor(GameObject unitRoot, Color teamColor)
        {
            Renderer[] renderers = unitRoot.GetComponentsInChildren<Renderer>(true);
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                renderer.GetPropertyBlock(block);

                Material shared = renderer.sharedMaterial;
                if (shared != null && shared.HasProperty("_BaseColor"))
                    block.SetColor("_BaseColor", teamColor);
                else if (shared != null && shared.HasProperty("_Color"))
                    block.SetColor("_Color", teamColor);
                else
                {
                    block.SetColor("_BaseColor", teamColor);
                    block.SetColor("_Color", teamColor);
                }

                renderer.SetPropertyBlock(block);
            }
        }

        private static void EnsureSorting(GameObject unit)
        {
            SortingGroup sortingGroup = unit.GetComponent<SortingGroup>();
            if (sortingGroup == null)
                sortingGroup = unit.AddComponent<SortingGroup>();

            sortingGroup.sortAtRoot = true;
            sortingGroup.sortingOrder = 0;
            // Keep units above tile layers while still letting foreground decor overlap them.
            sortingGroup.sortingLayerName = "Actors";

            Renderer[] renderers = unit.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingLayerName = "Actors";
                renderers[i].sortingOrder = 0;
            }

            Animator animator = unit.GetComponentInChildren<Animator>(true);
            if (animator != null)
                animator.applyRootMotion = false;
        }

    }
}
