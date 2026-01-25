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
        private int _currentRoll;

        public GameState State { get; private set; }
        public MatchLog Log { get; } = new MatchLog();
        public RulesetConfig Rules { get; private set; }
        public int CurrentRoll => _currentRoll;

        public event Action<GameState> OnMatchStarted;
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
            RollForTurn();

            OnMatchStarted?.Invoke(State);
        }

        public void Reset()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            State.Reset();
            CreateBots();
            Log.Clear();
            RollForTurn();

            OnMatchStarted?.Invoke(State);
        }

        public bool Tick()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            if (State.IsFinished)
                return false;

            var legal = MoveGenerator.GenerateLegalMoves(State, _currentRoll);
            if (legal.Count == 0)
                return HandleNoMoves();

            var bot = State.CurrentPlayer == PlayerId.A ? _botA : _botB;
            var move = bot.ChooseMove(State, legal);

            return ApplyCurrentMove(move);
        }

        public bool TryApplyHumanMove(Move move)
        {
            if (State == null || State.IsFinished)
                return false;

            var legal = MoveGenerator.GenerateLegalMoves(State, _currentRoll);
            if (legal.Count == 0)
                return HandleNoMoves();

            if (!legal.Contains(move))
                return false;

            return ApplyCurrentMove(move);
        }

        private void CreateBots()
        {
            _botA = new BotEasy(_seed + 100);
            _botB = new BotEasy(_seed + 200);
        }

        private void RollForTurn()
        {
            if (State.IsFinished) return;

            int roll = 0;
            if (Rules.maxRoll > 0)
                roll = _rng.Next(1, Rules.maxRoll + 1);
            _currentRoll = roll;
            State.SetCurrentRoll(roll);
        }

        private MoveRecord BuildRecord(
            PlayerId player,
            Move? move,
            int? fromCell,
            int? toCell,
            bool wasHit,
            ApplyResult result,
            MatchEndReason endReason)
        {
            return new MoveRecord(
                State.TurnIndex,
                player,
                move,
                fromCell,
                toCell,
                wasHit,
                State.CurrentRoll,
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

            var record = BuildRecord(
                State.CurrentPlayer,
                move,
                beforeMove.FromCell,
                beforeMove.ToCell,
                beforeMove.WasHit,
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

            State.AdvanceTurn();
            RollForTurn();
            return true;
        }

        private bool HandleNoMoves()
        {
            var winner = State.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A;
            State.Finish(winner);

            var buildRecord = BuildRecord(
                State.CurrentPlayer,
                null,
                null,
                null,
                false,
                ApplyResult.Illegal,
                MatchEndReason.NoMoves
            );
            Log.Add(buildRecord);
            OnMoveApplied?.Invoke(buildRecord);
            OnMatchEnded?.Invoke(State);
            return true;
        }

        private static PlayerId DecideWinnerOnTimeout(GameState s)
        {
            return PlayerId.A;
        }

        private (int? FromCell, int? ToCell, bool WasHit) DescribeMoveBeforeApply(Move move)
        {
            if (State == null) return (null, null, false);

            if (move.Kind == MoveKind.MoveOneStone)
            {
                int from = GameState.Mod(move.Value, State.Rules.ringSize);
                int to = GameState.Mod(from + _currentRoll, State.Rules.ringSize);
                bool wasHit = State.Rules.allowHitSingleStone
                    && State.GetStonesAt(State.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A, to) == 1;
                return (from, to, wasHit);
            }

            if (move.Kind == MoveKind.EnterFromHand)
            {
                int to = GameState.Mod(move.Value, State.Rules.ringSize);
                bool wasHit = State.Rules.allowHitSingleStone
                    && State.GetStonesAt(State.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A, to) == 1;
                return (null, to, wasHit);
            }

            return (null, null, false);
        }
    }
}
