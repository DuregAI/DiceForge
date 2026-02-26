using Diceforge.Core;
using Diceforge.MapSystem;
using UnityEngine;

namespace Diceforge.View
{
    public sealed class BattleBoardViewController : MonoBehaviour
    {
        [SerializeField] private BoardLayoutTokenMover moverA;
        [SerializeField] private BoardLayoutTokenMover moverB;
        [SerializeField] private bool animateSteps = true;
        [SerializeField] private bool useLayoutMode = true;

        private BattleRunner _runner;

        public void SetMovers(BoardLayoutTokenMover a, BoardLayoutTokenMover b)
        {
            moverA = a;
            moverB = b;

            if (_runner?.Rules != null)
                SnapToStartCells();
        }

        public void SetVisualMode(BoardVisualMode mode)
        {
            animateSteps = true;
            useLayoutMode = mode == BoardVisualMode.Tilemap;
        }

        public void Bind(BattleRunner runner)
        {
            if (ReferenceEquals(_runner, runner))
                return;

            UnbindRunner();
            _runner = runner;

            if (_runner == null)
                return;

            _runner.OnMatchStarted += HandleMatchStarted;
            _runner.OnMoveApplied += HandleMoveApplied;
            _runner.OnMatchEnded += HandleMatchEnded;

            if (_runner.Rules != null)
                SnapToStartCells();
        }

        private void OnDisable()
        {
            UnbindRunner();
        }

        private void OnDestroy()
        {
            UnbindRunner();
        }

        private void HandleMatchStarted(GameState state)
        {
            SnapToStartCells();
        }

        private void HandleMoveApplied(MoveRecord record)
        {
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

                if (useLayoutMode && record.PipUsed.HasValue && TryResolveSignedSteps(record.FromCell.Value, toCell, record.PipUsed.Value, out int steps))
                {
                    mover.MoveSteps(steps);
                    return;
                }

                mover.MoveTo(toCell);
                return;
            }

            if (animateSteps && useLayoutMode)
                mover.MoveTo(toCell);
            else
                mover.SnapTo(toCell);
        }

        private void HandleMatchEnded(MatchResult result)
        {
        }

        private bool TryResolveSignedSteps(int fromCell, int toCell, int pipUsed, out int steps)
        {
            steps = 0;

            int boardSize = _runner?.Rules?.boardSize ?? 0;
            if (boardSize <= 0)
                return false;

            int forward = (toCell - fromCell + boardSize) % boardSize;
            int backward = -((fromCell - toCell + boardSize) % boardSize);

            if (forward == pipUsed)
            {
                steps = pipUsed;
                return true;
            }

            if (-backward == pipUsed)
            {
                steps = backward;
                return true;
            }

            return false;
        }

        private BoardLayoutTokenMover GetMover(PlayerId playerId)
        {
            return playerId == PlayerId.A ? moverA : moverB;
        }

        private void SnapToStartCells()
        {
            if (_runner?.Rules == null)
                return;

            moverA?.SnapTo(_runner.Rules.startCellA);
            moverB?.SnapTo(_runner.Rules.startCellB);
        }

        private void UnbindRunner()
        {
            if (_runner == null)
                return;

            _runner.OnMatchStarted -= HandleMatchStarted;
            _runner.OnMoveApplied -= HandleMoveApplied;
            _runner.OnMatchEnded -= HandleMatchEnded;
            _runner = null;
        }
    }
}
