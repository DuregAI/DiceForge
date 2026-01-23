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

        public GameState State { get; private set; }
        public MatchLog Log { get; } = new MatchLog();
        public RulesetConfig Rules { get; private set; }
        public int CurrentRoll => State?.CurrentRoll ?? 0;

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
            PlaceStartingChips();
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
            PlaceStartingChips();
            RollForTurn();

            OnMatchStarted?.Invoke(State);
        }

        public bool Tick()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            if (State.IsFinished)
                return false;

            int posABefore = State.PosA;
            int posBBefore = State.PosB;

            var legal = MoveGenerator.GenerateLegalMoves(State);
            if (legal.Count == 0)
                return HandleNoMoves(posABefore, posBBefore);

            var bot = State.CurrentPlayer == PlayerId.A ? _botA : _botB;
            var move = bot.ChooseMove(State, legal);

            return ApplyCurrentMove(move, posABefore, posBBefore);
        }

        public bool TryApplyHumanMove(Move move)
        {
            if (State == null || State.IsFinished)
                return false;

            int posABefore = State.PosA;
            int posBBefore = State.PosB;

            var legal = MoveGenerator.GenerateLegalMoves(State);
            if (legal.Count == 0)
                return HandleNoMoves(posABefore, posBBefore);

            if (!legal.Contains(move))
                return false;

            return ApplyCurrentMove(move, posABefore, posBBefore);
        }

        private void CreateBots()
        {
            _botA = new BotEasy(_seed + 100);
            _botB = new BotEasy(_seed + 200);
        }

        private void PlaceStartingChips()
        {
            int count = Math.Clamp(Rules.startChipsOnBoard, 0, Rules.ringSize - 2);
            if (count <= 0) return;

            int seed = _seed + Rules.startChipsSeedOffset;
            var rng = Rules.startChipsRandom ? new Random(seed) : null;

            int placed = 0;
            if (Rules.startChipsRandom)
            {
                int attempts = 0;
                while (placed < count && attempts < Rules.ringSize * 10)
                {
                    int cell = rng.Next(Rules.ringSize);
                    attempts++;

                    if (cell == State.PosA || cell == State.PosB) continue;
                    if (State.HasChip[cell]) continue;

                    State.HasChip[cell] = true;
                    placed++;
                }
            }
            else
            {
                for (int cell = 0; cell < Rules.ringSize && placed < count; cell++)
                {
                    if (cell == State.PosA || cell == State.PosB) continue;
                    if (State.HasChip[cell]) continue;

                    State.HasChip[cell] = true;
                    placed++;
                }
            }
        }

        private void RollForTurn()
        {
            if (State.IsFinished) return;

            int roll = 0;
            if (Rules.maxStep > 0)
                roll = _rng.Next(1, Rules.maxStep + 1);
            State.SetCurrentRoll(roll);
        }

        private MoveRecord BuildRecord(
            PlayerId player,
            Move? move,
            int posABefore,
            int posBBefore,
            int posAAfter,
            int posBAfter,
            ApplyResult result,
            MatchEndReason endReason)
        {
            int? chipCell = null;

            if (move.HasValue && move.Value.Kind == MoveKind.PlaceChip)
                chipCell = GameState.Mod(move.Value.Value, State.Rules.ringSize);

            return new MoveRecord(
                State.TurnIndex,
                player,
                move,
                posABefore,
                posBBefore,
                posAAfter,
                posBAfter,
                chipCell,
                State.CurrentRoll,
                result,
                endReason,
                State.Winner
            );
        }

        private bool ApplyCurrentMove(Move move, int posABefore, int posBBefore)
        {
            var result = MoveGenerator.ApplyMove(State, move);
            int posAAfter = State.PosA;
            int posBAfter = State.PosB;
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
                posABefore,
                posBBefore,
                posAAfter,
                posBAfter,
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

        private bool HandleNoMoves(int posABefore, int posBBefore)
        {
            var winner = State.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A;
            State.Finish(winner);

            var buildRecord = BuildRecord(
                State.CurrentPlayer,
                null,
                posABefore,
                posBBefore,
                State.PosA,
                State.PosB,
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
            int distA = ForwardDistance(s.PosA, s.PosB, s.Rules.ringSize);
            int distB = ForwardDistance(s.PosB, s.PosA, s.Rules.ringSize);

            if (distA < distB) return PlayerId.A;
            if (distB < distA) return PlayerId.B;

            return PlayerId.A;
        }

        private static int ForwardDistance(int from, int to, int ringSize)
        {
            int d = to - from;
            if (d < 0) d += ringSize;
            return d;
        }
    }
}
