using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public sealed class BattleRunner
    {
        private BotEasy _botA;
        private BotEasy _botB;
        private int _seed;

        public GameState State { get; private set; }
        public MatchLog Log { get; } = new MatchLog();
        public RulesetConfig Rules { get; private set; }

        public event Action<GameState> OnMatchStarted;
        public event Action<MoveRecord> OnMoveApplied;
        public event Action<GameState> OnMatchEnded;

        public void Init(RulesetConfig rules, int seed)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();
            _seed = seed;

            State = new GameState(Rules);
            CreateBots();
            Log.Clear();

            OnMatchStarted?.Invoke(State);
        }

        public void Reset()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            State.Reset();
            CreateBots();
            Log.Clear();

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
            {
                var winner = State.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A;
                State.Finish(winner);

                var record = BuildRecord(
                    State.CurrentPlayer,
                    null,
                    posABefore,
                    posBBefore,
                    State.PosA,
                    State.PosB,
                    ApplyResult.Illegal,
                    MatchEndReason.NoMoves
                );
                Log.Add(record);
                OnMoveApplied?.Invoke(record);
                OnMatchEnded?.Invoke(State);
                return true;
            }

            var bot = State.CurrentPlayer == PlayerId.A ? _botA : _botB;
            var move = bot.ChooseMove(State, legal);

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
            return true;
        }

        private void CreateBots()
        {
            _botA = new BotEasy(_seed + 100);
            _botB = new BotEasy(_seed + 200);
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
            int? blockCell = null;

            if (move.HasValue && move.Value.Kind == MoveKind.PlaceBlock)
                blockCell = GameState.Mod(move.Value.Value, State.Rules.ringSize);

            return new MoveRecord(
                State.TurnIndex,
                player,
                move,
                posABefore,
                posBBefore,
                posAAfter,
                posBAfter,
                blockCell,
                result,
                endReason,
                State.Winner
            );
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
