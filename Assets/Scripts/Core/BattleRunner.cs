using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public sealed class BattleRunner
    {
        private BotEasy _botA;
        private BotEasy _botB;
        private int _seed;
        private Random _rng;
        private readonly List<int> _remainingPips = new List<int>();
        private DiceRoll _currentDice;
        private int _headMovesUsed;
        private int _maxHeadMovesThisTurn;

        public GameState State { get; private set; }
        public MatchLog Log { get; } = new MatchLog();
        public RulesetConfig Rules { get; private set; }
        public DiceRoll CurrentDice => _currentDice;
        public IReadOnlyList<int> RemainingPips => _remainingPips;
        public int HeadMovesUsed => _headMovesUsed;
        public int HeadMovesLimit => _maxHeadMovesThisTurn;

        public event Action<GameState> OnMatchStarted;
        public event Action<GameState> OnTurnStarted;
        public event Action<MoveRecord> OnMoveApplied;
        public event Action<GameState> OnMatchEnded;

        public void Init(RulesetConfig rules, int seed)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();
            _seed = seed;
            _rng = new Random(_seed);

            State = new GameState(Rules);
            CreateBots();
            Log.Clear();
            BeginTurn();

            OnMatchStarted?.Invoke(State);
        }

        public void Reset()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            State.Reset();
            CreateBots();
            Log.Clear();
            BeginTurn();

            OnMatchStarted?.Invoke(State);
        }

        public bool Tick()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            if (State.IsFinished)
                return false;

            var legal = MoveGenerator.GenerateLegalMoves(State, _remainingPips, _headMovesUsed, _maxHeadMovesThisTurn);
            if (legal.Count == 0)
            {
                EndTurn();
                return true;
            }

            var bot = State.CurrentPlayer == PlayerId.A ? _botA : _botB;
            var move = bot.ChooseMove(State, legal);

            return ApplyCurrentMove(move);
        }

        public bool TryApplyHumanMove(Move move)
        {
            if (State == null || State.IsFinished)
                return false;

            var legal = MoveGenerator.GenerateLegalMoves(State, _remainingPips, _headMovesUsed, _maxHeadMovesThisTurn);
            if (legal.Count == 0)
            {
                EndTurn();
                return true;
            }

            if (!legal.Contains(move))
                return false;

            return ApplyCurrentMove(move);
        }

        private void CreateBots()
        {
            _botA = new BotEasy(_seed + 100);
            _botB = new BotEasy(_seed + 200);
        }

        private void BeginTurn()
        {
            if (State.IsFinished) return;

            int dieA = _rng.Next(1, 7);
            int dieB = _rng.Next(1, 7);
            _currentDice = new DiceRoll(dieA, dieB);
            State.SetCurrentDice(_currentDice);

            _remainingPips.Clear();
            if (_currentDice.IsDouble)
            {
                for (int i = 0; i < 4; i++)
                    _remainingPips.Add(dieA);
            }
            else
            {
                _remainingPips.Add(dieA);
                _remainingPips.Add(dieB);
            }

            _headMovesUsed = 0;
            _maxHeadMovesThisTurn = CalculateHeadMoveLimit(State.CurrentPlayer);

            OnTurnStarted?.Invoke(State);
        }

        private int CalculateHeadMoveLimit(PlayerId player)
        {
            if (Rules.headRules == null || !Rules.headRules.restrictHeadMoves)
                return int.MaxValue;

            if (State.GetTurnsTaken(player) == 0)
            {
                int? allowance = Rules.headRules.GetFirstTurnAllowance(_currentDice.DieA, _currentDice.DieB);
                if (allowance.HasValue)
                    return allowance.Value;
            }

            return Rules.headRules.maxHeadMovesPerTurn;
        }

        private MoveRecord BuildRecord(
            PlayerId player,
            Move? move,
            int? fromCell,
            int? toCell,
            int? pipUsed,
            ApplyResult result,
            MatchEndReason endReason)
        {
            return new MoveRecord(
                State.TurnIndex,
                player,
                move,
                fromCell,
                toCell,
                pipUsed,
                _currentDice,
                _remainingPips.ToArray(),
                result,
                endReason,
                State.Winner
            );
        }

        private bool ApplyCurrentMove(Move move)
        {
            var beforeMove = DescribeMoveBeforeApply(move);
            var result = MoveGenerator.ApplyMove(State, move);
            var endReason = MatchEndReason.None;

            if (result == ApplyResult.Finished || State.IsFinished)
                endReason = MatchEndReason.Win;

            if (!State.IsFinished && State.TurnIndex >= Rules.maxTurns - 1)
            {
                var winner = DecideWinnerOnTimeout(State);
                State.Finish(winner);
                endReason = MatchEndReason.Timeout;
            }

            if (result == ApplyResult.Ok || result == ApplyResult.Finished)
            {
                RemovePip(move.PipUsed);
                if (beforeMove.FromCell.HasValue && beforeMove.FromCell.Value == GetHeadCell(State.CurrentPlayer))
                    _headMovesUsed++;
            }

            var record = BuildRecord(
                State.CurrentPlayer,
                move,
                beforeMove.FromCell,
                beforeMove.ToCell,
                move.PipUsed,
                result,
                endReason
            );
            Log.Add(record);
            OnMoveApplied?.Invoke(record);

            if (State.IsFinished)
            {
                OnMatchEnded?.Invoke(State);
                return true;
            }

            if (_remainingPips.Count == 0)
            {
                EndTurn();
                return true;
            }

            var legal = MoveGenerator.GenerateLegalMoves(State, _remainingPips, _headMovesUsed, _maxHeadMovesThisTurn);
            if (legal.Count == 0)
            {
                EndTurn();
                return true;
            }

            return true;
        }

        private void RemovePip(int pip)
        {
            int index = _remainingPips.IndexOf(pip);
            if (index >= 0)
                _remainingPips.RemoveAt(index);
        }

        private void EndTurn()
        {
            if (State.IsFinished)
                return;

            State.AdvanceTurn();
            BeginTurn();
        }

        private static PlayerId DecideWinnerOnTimeout(GameState s)
        {
            return PlayerId.A;
        }

        private (int? FromCell, int? ToCell) DescribeMoveBeforeApply(Move move)
        {
            if (State == null) return (null, null);

            int from = GameState.Mod(move.FromCell, State.Rules.boardSize);
            int to = GameState.Mod(from + move.PipUsed, State.Rules.boardSize);

            if (move.Kind == MoveKind.BearOff)
                return (from, null);

            return (from, to);
        }

        private int GetHeadCell(PlayerId player)
        {
            return player == PlayerId.A ? Rules.startCellA : Rules.startCellB;
        }
    }
}
