using System;
using System.Collections.Generic;
using Diceforge.Audio;
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
        private IBoardGeometry _geometry;
        private GameState _lastState;
        private string _finishingId;
        private BattlePresentationProfile presentation;
        private Diceforge.GameModes.DemoLevelDefinition demoLevel;
        public void SetDemoLevel(Diceforge.GameModes.DemoLevelDefinition level) => demoLevel = level;

        public bool TryGetHero(string heroId, out int cell, out string tokenName, out bool exited)
        {
            cell = -1;
            tokenName = null;
            exited = false;
            if (demoLevel == null || demoLevel.heroIds == null) return false;
            for (int i = 0; i < demoLevel.heroIds.Length && i < _tokensA.Count; i++)
            {
                if (demoLevel.heroIds[i] != heroId) continue;
                var token = _tokensA[i];
                if (!token.assigned) return false;
                cell = token.placement.Location == TokenLocation.Cell ? token.placement.Cell : -1;
                tokenName = token.root.name;
                exited = token.placement.Location == TokenLocation.BorneOff;
                return true;
            }
            return false;
        }

        public string HeroForToken(string tokenName)
        {
            if (demoLevel == null || string.IsNullOrEmpty(tokenName)) return null;
            for (int i = 0; i < demoLevel.heroIds.Length && i < _tokensA.Count; i++)
                if (_tokensA[i].root.name == tokenName) return demoLevel.heroIds[i];
            return null;
        }
        public void RestoreDemoHeroes(Diceforge.Progression.DemoHeroCheckpoint[] heroes, GameState state)
        {
            if (demoLevel == null || heroes == null) throw new InvalidOperationException("Demo hero checkpoint is missing.");
            var assignments = new List<TokenAssignment>();
            for (int i = 0; i < demoLevel.heroIds.Length; i++)
            {
                var hero = Array.Find(heroes, h => h.id == demoLevel.heroIds[i]);
                if (hero == null || i >= _tokensA.Count) throw new InvalidOperationException("Demo hero checkpoint does not match the token pool.");
                var token = _tokensA[i];
                assignments.Add(new TokenAssignment(token.stoneId, 0, token.stoneIndex,
                    hero.exited ? TokenLocation.BorneOff : TokenLocation.Cell, hero.cell));
            }
            _lastState = state; _finishingId = null;
            CancelAllMovement(_tokensA); CancelAllMovement(_tokensB);
            ApplyAssignments(assignments, ReadCounts(state), null);
        }
        public void SetGeometry(IBoardGeometry geometry) { _geometry = geometry; }
        public void RefreshGeometry(GameState state)
        {
            _lastState=state;_finishingId=null;
            CancelAllMovement(_tokensA);CancelAllMovement(_tokensB);
            ApplyAssignments(ReadAssignments(),ReadCounts(state),null);
        }
        private void LateUpdate()
        {
            if (_finishingId == null || IsAnimating || _lastState == null) return;
            _finishingId = null;
            ApplyAssignments(ReadAssignments(), ReadCounts(_lastState), null);
        }

        [Header("Bar Placement")]
        [SerializeField] private float barSideOffsetX = 0.28f;
        [SerializeField] private float barStackStepY = 0.045f;
        [SerializeField] private float barStackStepZ = 0.03f;

        public void Configure(BoardLayout layout, Tilemap positionTilemap, Transform unitsRoot, GameObject teamAUnitPrefab, GameObject teamBUnitPrefab, Color teamAColor, Color teamBColor)
        {
            presentation = Resources.Load<BattlePresentationProfile>("BattlePresentationProfile");
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
            _lastState = matchState;
            _finishingId = null;
            if (!_configured || matchState == null)
                return;

            int totalA = CountTotalStones(matchState, PlayerId.A);
            int totalB = demoLevel != null ? 0 : CountTotalStones(matchState, PlayerId.B);

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
            _lastState = state;
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

            var movingToken = FindToken(result.MovedId);
            var hitOrigins = new Dictionary<TokenBinding,Vector3>();
            if (animate && _geometry is DioramaBoard)
            {
                if (!movingToken.root.activeSelf)
                {
                    movingToken.root.SetActive(true);
                    movingToken.mover.SetVisualOffset(Vector3.zero);
                    if(record.FromCell.HasValue)movingToken.mover.SnapTo(record.FromCell.Value);
                    else movingToken.mover.SnapToWorld(_geometry.WaitingPosition((int)record.PlayerId));
                }
                foreach(var assignment in result.Assignments)
                {
                    var token=FindToken(assignment.Id);
                    if(token.placement.Location==TokenLocation.Cell && assignment.Location==TokenLocation.Bar)
                        hitOrigins[token]=token.root.transform.position;
                }
            }
            ApplyAssignments(result.Assignments, counts, animate ? result.MovedId : null);
            if (animate && record.ToCell.HasValue)
            {
                AnimateMove(FindToken(result.MovedId), record, state.Rules.boardSize);
                if (_geometry is DioramaBoard) _finishingId = result.MovedId;
            }
            if (animate && _geometry is DioramaBoard board)
            {
                _finishingId=result.MovedId;
                AudioClip cue = presentation != null ? presentation.moveClip : null;
                if (record.Move?.Kind == MoveKind.BearOff) cue = presentation != null ? presentation.exitClip : null;
                else if (hitOrigins.Count > 0) cue = presentation != null ? presentation.hitClip : null;
                AudioManager.Instance?.PlayGameSfx(cue, presentation != null ? presentation.soundGain : 1f);
                if(record.Move?.Kind==MoveKind.BearOff)
                {
                    movingToken.root.SetActive(true);
                    movingToken.mover.MoveToWorld(board.ExitPosition((int)record.PlayerId),-1,.55f);
                    movingToken.root.GetComponent<GoblinLife>()?.React("Return", presentation != null ? presentation.exitSeconds : .25f);
                }
                foreach(var hit in hitOrigins)
                {
                    hit.Key.root.SetActive(true);hit.Key.root.transform.position=hit.Value;
                    hit.Key.mover.MoveToWorld(board.WaitingPosition((int)hit.Key.player),-1,.5f);
                    hit.Key.root.GetComponent<GoblinLife>()?.React("Hit", presentation != null ? presentation.hitSeconds : .25f);
                }
            }
        }

        public void ReactToSelection(string tokenName)
        {
            if (_geometry is not DioramaBoard || string.IsNullOrEmpty(tokenName)) return;
            foreach (var team in new[] { _tokensA, _tokensB })
                foreach (TokenBinding token in team)
                    if (token.root.activeSelf && token.root.name == tokenName)
                {
                    token.root.GetComponent<GoblinLife>()?.React("Selected", presentation != null ? presentation.selectionSeconds : .2f);
                    return;
                }
        }

        public void ReactToMatchEnd(PlayerId? winner)
        {
            if (_geometry is not DioramaBoard || !winner.HasValue) return;
            bool winnerVisible = false;
            foreach (TokenBinding token in winner == PlayerId.A ? _tokensA : _tokensB)
                if (token.root.activeSelf)
                {
                    token.root.GetComponent<GoblinLife>()?.React("Victory", presentation != null ? presentation.victorySeconds : .5f);
                    winnerVisible = true;
                }
            if (winnerVisible) return;
            foreach (TokenBinding token in winner == PlayerId.A ? _tokensB : _tokensA)
                if (token.root.activeSelf)
                    token.root.GetComponent<GoblinLife>()?.React("Hit", presentation != null ? presentation.hitSeconds : .25f);
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

        private TokenCounts ReadCounts(GameState state) => new TokenCounts(
            state.StonesAByCell.ToArray(), demoLevel != null ? new int[state.Rules.boardSize] : state.StonesBByCell.ToArray(),
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
                if (_geometry is DioramaBoard diorama)
                {
                    int slot3d = assignment.Location == TokenLocation.Cell ? assignment.Cell : counts.BoardSize;
                    int index3d = stackIndices[assignment.Player, slot3d]++;
                    int count3d = assignment.Location == TokenLocation.Cell ? counts.Get(assignment.Player, TokenLocation.Cell, assignment.Cell)
                        : counts.Get(assignment.Player, TokenLocation.Bar);
                    bool show = (assignment.Location != TokenLocation.BorneOff && index3d < 3) || assignment.Id == animatedId;
                    bool wasActive = token.root.activeSelf;
                    token.root.SetActive(show);
                    token.root.transform.localScale=Vector3.one*(count3d<=1?.78f:count3d==2?.62f:.52f);
                    var identity = token.root.GetComponent<DioramaToken>();
                    identity.cellId = assignment.Location == TokenLocation.Cell ? assignment.Cell : -1;
                    if (!show) { token.mover.CancelAllMovement(); continue; }
                    token.mover.SetVisualOffset(diorama.FormationOffset(index3d, Mathf.Min(3,count3d)));
                    token.root.transform.rotation = Quaternion.Euler(0,180,0);
                    if (assignment.Id == animatedId && wasActive) continue;
                    if (assignment.Location == TokenLocation.Bar) token.mover.SnapToWorld(diorama.WaitingPosition(assignment.Player));
                    else token.mover.SnapTo(assignment.Cell);
                    continue;
                }
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
            if (_geometry is DioramaBoard board)
                for(int p=0;p<2;p++)
                {
                    for(int c=0;c<counts.BoardSize;c++) board.SetCount(p,c,counts.Get(p,TokenLocation.Cell,c));
                    board.SetCount(p,-1,counts.Get(p,TokenLocation.Bar));
                }
        }

        private void AnimateMove(TokenBinding token, MoveRecord record, int boardSize)
        {
            token.mover.CancelAllMovement();
            if (demoLevel != null && _geometry is DioramaBoard)
            {
                token.mover.SetJumpHeight((record.PipUsed ?? 1) >= 2 ? .75f : .32f);
                token.mover.MoveToWorld(_geometry.CellPosition(record.ToCell.Value), record.ToCell.Value, DioramaBoard.ReducedMotion ? .08f : .5f);
                return;
            }
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
                GameObject heroPrefab = player == PlayerId.A && demoLevel != null &&
                    index < demoLevel.heroIds.Length && demoLevel.heroIds[index] == "luma"
                    ? _teamBUnitPrefab : unitPrefab;
                if (player == PlayerId.A && demoLevel != null && index < demoLevel.heroIds.Length &&
                    demoLevel.heroIds[index] == "bum" && demoLevel.bumPrefab != null) heroPrefab = demoLevel.bumPrefab;
                GameObject instance = Instantiate(heroPrefab, _unitsRoot);
                instance.name = $"{prefix}_{index:D2}";

                BoardLayoutTokenMover mover = instance.GetComponent<BoardLayoutTokenMover>();
                if (mover == null)
                {
                    Debug.LogWarning($"[StonesTokensView] Token '{instance.name}' missing BoardLayoutTokenMover. Adding one.", instance);
                    mover = instance.AddComponent<BoardLayoutTokenMover>();
                }

                mover.SetLayout(_layout);
                mover.SetPositionTilemap(_positionTilemap);
                mover.SetGeometry(_geometry);
                mover.SetHopPresentation(demoLevel != null && player == PlayerId.A);

                if (_geometry is DioramaBoard)
                {
                    var identity = instance.AddComponent<DioramaToken>();
                    identity.player = (int)player;
                    identity.logicalId = player == PlayerId.A && demoLevel != null && index < demoLevel.heroIds.Length
                        ? demoLevel.heroIds[index] : $"{player}-{index}";
                    var collider = instance.AddComponent<CapsuleCollider>(); collider.center = new Vector3(0,.5f,0); collider.height=1.1f; collider.radius=.33f;
                }
                else ApplyTeamColor(instance, color);
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
