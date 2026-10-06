using Diceforge.Core;
using Diceforge.Map;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Diceforge.View
{
    public sealed class BattleBoardViewController : MonoBehaviour
    {
        [SerializeField] private BoardLayoutTokenMover moverA;
        [SerializeField] private BoardLayoutTokenMover moverB;
        [SerializeField] private bool animateSteps = true;
        [SerializeField] private StonesTokensView stonesTokensView;

        private BattleRunner _runner;
        private string _pendingAnimatedTokenName;
        private DioramaBoard _diorama;
        private Diceforge.GameModes.DemoLevelDefinition demoLevel;
        private DemoTrailHazardView hazardView;
        public void ConfigureDiorama(DioramaBoard board)
        {
            _diorama = board;
            stonesTokensView.SetGeometry(board);
            board.GeometryChanged += RebuildForOrientation;
        }
        private void RebuildForOrientation()
        {
            if (_runner?.State != null) stonesTokensView.RefreshGeometry(_runner.State);
        }

        public bool IsAnimating
        {
            get
            {
                if (hazardView != null && hazardView.IsAnimating) return true;
                if (stonesTokensView != null)
                    return stonesTokensView.IsAnimating;

                return (moverA != null && moverA.IsAnimating) || (moverB != null && moverB.IsAnimating);
            }
        }

        public void SetPendingAnimatedTokenName(string tokenRootName)
        {
            _pendingAnimatedTokenName = tokenRootName;
        }

        public void ReactToSelection(string tokenRootName) => stonesTokensView?.ReactToSelection(tokenRootName);
        public void ReactToMatchEnd(PlayerId? winner) => stonesTokensView?.ReactToMatchEnd(winner);
        public void ConfigureDemo(Diceforge.GameModes.DemoLevelDefinition level)
        {
            demoLevel = level;
            stonesTokensView?.SetDemoLevel(level);
            if (_diorama != null) _diorama.ConfigureTrail(level != null);
        }
        public string HeroForToken(string tokenName) => stonesTokensView?.HeroForToken(tokenName);
        public bool TryGetHero(string heroId, out int cell, out string tokenName, out bool exited)
        {
            cell = -1; tokenName = null; exited = false;
            return stonesTokensView != null && stonesTokensView.TryGetHero(heroId, out cell, out tokenName, out exited);
        }

        public void SetMovers(BoardLayoutTokenMover a, BoardLayoutTokenMover b)
        {
            moverA = a;
            moverB = b;

            if (_runner?.Rules != null)
                SnapToStartCells();
        }

        public void ConfigureTokensView(BoardLayout layout, Tilemap positionTilemap, Transform unitsRoot, GameObject teamAUnitPrefab, GameObject teamBUnitPrefab, Color teamAColor, Color teamBColor)
        {
            if (stonesTokensView == null)
                stonesTokensView = GetComponent<StonesTokensView>() ?? gameObject.AddComponent<StonesTokensView>();

            stonesTokensView.Configure(layout, positionTilemap, unitsRoot, teamAUnitPrefab, teamBUnitPrefab, teamAColor, teamBColor);

            if (_runner?.State != null)
            {
                stonesTokensView.BuildTokensFromMatchState(_runner.State);
                SetSingleMoverVisibility(!stonesTokensView.HasActiveTokens());
            }
        }

        public void Bind(BattleRunner runner)
        {
            if (ReferenceEquals(_runner, runner))
                return;

            UnbindRunner();
            _runner = runner;

            if (_runner == null)
                return;

            if (demoLevel != null && _diorama != null)
            {
                hazardView = GetComponent<DemoTrailHazardView>() ?? gameObject.AddComponent<DemoTrailHazardView>();
                hazardView.Configure(_runner, _diorama, stonesTokensView, demoLevel);
            }

            _runner.OnMatchStarted += HandleMatchStarted;
            _runner.OnMoveApplied += HandleMoveApplied;
            _runner.OnMatchEnded += HandleMatchEnded;

            if (_runner.State != null)
            {
                BuildTokensIfAvailable(_runner.State);
                SnapToStartCells();
            }
        }

        private void OnDisable()
        {
            UnbindRunner();
        }

        private void OnDestroy()
        {
            if (_diorama != null) _diorama.GeometryChanged -= RebuildForOrientation;
            UnbindRunner();
        }

        private void HandleMatchStarted(GameState state)
        {
            _pendingAnimatedTokenName = null;
            BuildTokensIfAvailable(state);
            SnapToStartCells();
        }

        private void HandleMoveApplied(MoveRecord record)
        {
            if (_runner?.State == null)
                return;

            string preferredTokenName = _pendingAnimatedTokenName;
            _pendingAnimatedTokenName = null;

            if (stonesTokensView != null)
            {
                stonesTokensView.HandleMoveApplied(record, _runner.State, animateSteps, preferredTokenName);
                return;
            }

            BoardLayoutTokenMover mover = GetMover(record.PlayerId);
            if (mover == null || !record.ToCell.HasValue)
                return;

            int toCell = record.ToCell.Value;

            if (record.FromCell.HasValue)
            {
                if (!animateSteps)
                {
                    mover.SnapTo(toCell);
                    return;
                }

                if (record.PipUsed.HasValue && TryResolveSignedSteps(record.FromCell.Value, toCell, record.PipUsed.Value, out int steps))
                {
                    mover.MoveSteps(steps);
                    return;
                }

                mover.MoveTo(toCell);
                return;
            }

            if (animateSteps)
                mover.MoveTo(toCell);
            else
                mover.SnapTo(toCell);
        }

        private void HandleMatchEnded(MatchResult result)
        {
        }

        private bool TryResolveSignedSteps(int fromCell, int toCell, int pipUsed, out int steps)
        {
            int boardSize = _runner?.Rules?.boardSize ?? 0;
            return BoardMoveAnimationResolver.TryResolveSignedSteps(boardSize, fromCell, toCell, pipUsed, out steps);
        }

        private BoardLayoutTokenMover GetMover(PlayerId playerId)
        {
            return playerId == PlayerId.A ? moverA : moverB;
        }

        private void SnapToStartCells()
        {
            if (_runner?.Rules == null)
                return;

            if (stonesTokensView != null)
                return;

            moverA?.SnapTo(_runner.Rules.startCellA);
            moverB?.SnapTo(_runner.Rules.startCellB);
        }

        private void BuildTokensIfAvailable(GameState state)
        {
            if (stonesTokensView == null)
                return;

            stonesTokensView.BuildTokensFromMatchState(state);
            SetSingleMoverVisibility(!stonesTokensView.HasActiveTokens());
        }

        private void SetSingleMoverVisibility(bool visible)
        {
            if (moverA != null)
                moverA.gameObject.SetActive(visible);

            if (moverB != null)
                moverB.gameObject.SetActive(visible);
        }

        private void UnbindRunner()
        {
            _pendingAnimatedTokenName = null;
            if (_runner == null)
                return;

            _runner.OnMatchStarted -= HandleMatchStarted;
            _runner.OnMoveApplied -= HandleMoveApplied;
            _runner.OnMatchEnded -= HandleMatchEnded;
            _runner = null;
        }
    }
}
