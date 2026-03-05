using System;
using System.Collections.Generic;
using Diceforge.Core;
using Diceforge.Map;
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
            public int currentCellId;
            public bool activeOnBoard;
        }

        private readonly List<TokenBinding> _tokensA = new();
        private readonly List<TokenBinding> _tokensB = new();
        private readonly Dictionary<string, TokenBinding> _tokensById = new();

        private BoardLayout _layout;
        private Tilemap _positionTilemap;
        private Transform _unitsRoot;
        private GameObject _unitPrefab;
        private Color _teamAColor;
        private Color _teamBColor;
        private bool _configured;

        [Header("Bar Placement")]
        [SerializeField] private float barSideOffsetX = 0.28f;
        [SerializeField] private float barStackStepY = 0.045f;
        [SerializeField] private float barStackStepZ = 0.03f;

        public void Configure(BoardLayout layout, Tilemap positionTilemap, Transform unitsRoot, GameObject unitPrefab, Color teamAColor, Color teamBColor)
        {
            _layout = layout;
            _positionTilemap = positionTilemap;
            _unitsRoot = unitsRoot;
            _unitPrefab = unitPrefab;
            _teamAColor = teamAColor;
            _teamBColor = teamBColor;
            _configured = _layout != null && _unitsRoot != null && _unitPrefab != null;
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
            ReconcileFromState(matchState, animateMovedToken: false, defaultAnimate: false, movedRecord: null);
        }

        public void UpdateTokenPosition(string stoneId, int cellId)
        {
            if (!_tokensById.TryGetValue(stoneId, out TokenBinding token))
                return;

            if (!token.activeOnBoard)
                token.root.SetActive(true);

            token.activeOnBoard = true;
            token.currentCellId = cellId;
            token.mover.SetVisualOffset(Vector3.zero);

            if (cellId < 0)
            {
                Vector3 barCenter = CalculateBarCenterWorld();
                token.mover.SnapToWorld(CalculateBarStoneWorldPosition(token.player, barCenter, 0), -1);
                return;
            }

            token.mover.SnapTo(cellId);
        }

        public void ClearAll()
        {
            SetActive(_tokensA, false);
            SetActive(_tokensB, false);
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

        public void HandleMoveApplied(MoveRecord record, GameState state, bool defaultAnimate, bool useLayoutMode)
        {
            if (state == null)
                return;

            ReconcileFromState(state, animateMovedToken: true, defaultAnimate: defaultAnimate && useLayoutMode, movedRecord: record);
        }

        private void ReconcileFromState(GameState state, bool animateMovedToken, bool defaultAnimate, MoveRecord? movedRecord)
        {
            Vector3 barCenterWorld = CalculateBarCenterWorld();
            List<TokenPlacement> placementsA = BuildPlacements(state, PlayerId.A, barCenterWorld);
            List<TokenPlacement> placementsB = BuildPlacements(state, PlayerId.B, barCenterWorld);

            bool movedApplied = false;
            if (animateMovedToken && movedRecord.HasValue)
            {
                movedApplied = TryAnimateMovedToken(movedRecord.Value, placementsA, placementsB, defaultAnimate);
            }

            ApplyPlacements(_tokensA, placementsA, movedApplied ? movedRecord : null, defaultAnimate);
            ApplyPlacements(_tokensB, placementsB, movedApplied ? movedRecord : null, defaultAnimate);

            ValidateCells(_tokensA, state.Rules.boardSize, PlayerId.A);
            ValidateCells(_tokensB, state.Rules.boardSize, PlayerId.B);
        }

        private bool TryAnimateMovedToken(MoveRecord record, List<TokenPlacement> placementsA, List<TokenPlacement> placementsB, bool animate)
        {
            if (!record.ToCell.HasValue)
                return false;

            List<TokenBinding> list = record.PlayerId == PlayerId.A ? _tokensA : _tokensB;
            List<TokenPlacement> placements = record.PlayerId == PlayerId.A ? placementsA : placementsB;
            TokenBinding movedToken = null;

            if (record.FromCell.HasValue)
            {
                int from = record.FromCell.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (!list[i].activeOnBoard || list[i].currentCellId != from)
                        continue;

                    movedToken = list[i];
                    break;
                }
            }

            if (movedToken == null && record.Move.HasValue && record.Move.Value.Kind == MoveKind.EnterFromBar)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!list[i].activeOnBoard || list[i].currentCellId >= 0)
                        continue;

                    movedToken = list[i];
                    break;
                }
            }

            if (movedToken == null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (!list[i].activeOnBoard)
                        continue;

                    movedToken = list[i];
                    break;
                }
            }

            if (movedToken == null)
                return false;

            int toCell = record.ToCell.Value;
            for (int i = 0; i < placements.Count; i++)
            {
                if (placements[i].token != movedToken)
                    continue;

                movedToken.currentCellId = toCell;
                movedToken.activeOnBoard = true;
                movedToken.root.SetActive(true);
                movedToken.mover.SetVisualOffset(placements[i].offset);

                if (animate)
                    movedToken.mover.MoveTo(toCell);
                else
                    movedToken.mover.SnapTo(toCell);

                return true;
            }

            return false;
        }

        private void ApplyPlacements(List<TokenBinding> tokens, List<TokenPlacement> placements, MoveRecord? movedRecord, bool animate)
        {
            HashSet<TokenBinding> used = new();

            for (int i = 0; i < placements.Count; i++)
            {
                TokenPlacement placement = placements[i];
                TokenBinding token = placement.token;
                used.Add(token);

                if (!token.root.activeSelf)
                    token.root.SetActive(true);

                token.activeOnBoard = true;
                token.currentCellId = placement.cellId;
                token.mover.SetVisualOffset(placement.offset);

                bool isMovedToken = movedRecord.HasValue
                    && movedRecord.Value.PlayerId == token.player
                    && movedRecord.Value.ToCell.HasValue
                    && placement.cellId == movedRecord.Value.ToCell.Value
                    && movedRecord.Value.FromCell.HasValue;

                if (isMovedToken && animate)
                    continue;

                if (placement.useWorldPosition)
                    token.mover.SnapToWorld(placement.worldPosition, placement.cellId);
                else
                    token.mover.SnapTo(placement.cellId);
            }

            for (int i = 0; i < tokens.Count; i++)
            {
                if (used.Contains(tokens[i]))
                    continue;

                tokens[i].activeOnBoard = false;
                tokens[i].root.SetActive(false);
            }
        }

        private List<TokenPlacement> BuildPlacements(GameState state, PlayerId player, Vector3 barCenterWorld)
        {
            List<TokenBinding> tokens = player == PlayerId.A ? _tokensA : _tokensB;
            ReadOnlySpan<int> counts = player == PlayerId.A ? state.StonesAByCell : state.StonesBByCell;
            List<TokenPlacement> placements = new(tokens.Count);

            int tokenCursor = 0;
            for (int cell = 0; cell < counts.Length; cell++)
            {
                int count = counts[cell];
                for (int i = 0; i < count; i++)
                {
                    if (tokenCursor >= tokens.Count)
                        break;

                    placements.Add(new TokenPlacement
                    {
                        token = tokens[tokenCursor],
                        cellId = cell,
                        offset = CalculateFormationOffset(i, count),
                        useWorldPosition = false,
                        worldPosition = Vector3.zero
                    });

                    tokenCursor++;
                }
            }

            int barCount = state.GetBarCount(player);
            for (int i = 0; i < barCount; i++)
            {
                if (tokenCursor >= tokens.Count)
                    break;

                placements.Add(new TokenPlacement
                {
                    token = tokens[tokenCursor],
                    cellId = -1,
                    offset = Vector3.zero,
                    useWorldPosition = true,
                    worldPosition = CalculateBarStoneWorldPosition(player, barCenterWorld, i)
                });

                tokenCursor++;
            }

            return placements;
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

            while (tokens.Count < requiredCount)
            {
                int index = tokens.Count;
                GameObject instance = Instantiate(_unitPrefab, _unitsRoot);
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
                    currentCellId = 0,
                    activeOnBoard = false
                };

                instance.SetActive(false);
                tokens.Add(token);
                _tokensById[token.stoneId] = token;
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
            sortingGroup.sortingLayerName = "Default";

            Animator animator = unit.GetComponentInChildren<Animator>(true);
            if (animator != null)
                animator.applyRootMotion = false;
        }

        private static void SetActive(List<TokenBinding> tokens, bool active)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                tokens[i].activeOnBoard = active;
                tokens[i].root.SetActive(active);
            }
        }

        private void ValidateCells(List<TokenBinding> tokens, int boardSize, PlayerId player)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!tokens[i].activeOnBoard)
                    continue;

                int cell = tokens[i].currentCellId;
                if (cell == -1 || (cell >= 0 && cell < boardSize))
                    continue;

                Debug.LogWarning($"[StonesTokensView] {player} stone '{tokens[i].stoneId}' resolved invalid cell {cell}.", this);
            }
        }

        private struct TokenPlacement
        {
            public TokenBinding token;
            public int cellId;
            public Vector3 offset;
            public bool useWorldPosition;
            public Vector3 worldPosition;
        }
    }
}

