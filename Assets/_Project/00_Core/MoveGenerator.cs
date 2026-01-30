using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public enum MoveKind : byte
    {
        MoveStone = 0,
        BearOff = 1
    }

    public readonly struct Move : IEquatable<Move>
    {
        public MoveKind Kind { get; }
        public int FromCell { get; }
        public int PipUsed { get; }

        private Move(MoveKind kind, int fromCell, int pipUsed)
        {
            Kind = kind;
            FromCell = fromCell;
            PipUsed = pipUsed;
        }

        public static Move MoveStone(int fromCell, int pipUsed) => new Move(MoveKind.MoveStone, fromCell, pipUsed);
        public static Move BearOff(int fromCell, int pipUsed) => new Move(MoveKind.BearOff, fromCell, pipUsed);

        public override string ToString()
        {
            return Kind == MoveKind.BearOff
                ? $"BearOff({FromCell}, {PipUsed})"
                : $"Move({FromCell}, {PipUsed})";
        }

        public bool Equals(Move other)
        {
            return Kind == other.Kind && FromCell == other.FromCell && PipUsed == other.PipUsed;
        }

        public override bool Equals(object obj)
        {
            return obj is Move other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, FromCell, PipUsed);
        }
    }

    public static class MoveGenerator
    {
        public static List<Move> GenerateLegalMoves(
            GameState s,
            int dieValue,
            int headMovesUsed,
            int maxHeadMovesThisTurn)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var moves = new List<Move>(32);

            if (s.IsFinished) return moves;
            if (dieValue <= 0) return moves;

            var rules = s.Rules;
            var current = s.CurrentPlayer;
            var opponent = current == PlayerId.A ? PlayerId.B : PlayerId.A;
            int dir = GetMoveDir(s, current);
            int headCell = GetHeadCell(s, current);
            bool allInHome = AllStonesInHome(s, current);

            for (int cell = 0; cell < rules.boardSize; cell++)
            {
                if (s.GetStonesAt(current, cell) <= 0) continue;
                if (cell == headCell && rules.headRules.restrictHeadMoves && headMovesUsed >= maxHeadMovesThisTurn)
                    continue;

                if (allInHome && CanBearOff(s, current, cell, dieValue))
                {
                    moves.Add(Move.BearOff(cell, dieValue));
                    continue;
                }

                if (IsOvershootWithoutBearOff(s, current, cell, dieValue))
                    continue;

                int to = GameState.Mod(cell + dieValue * dir, rules.boardSize);
                if (!CanEnterCell(s, opponent, to))
                    continue;
                moves.Add(Move.MoveStone(cell, dieValue));
            }

            return moves;
        }

        public static ApplyResult ApplyMove(GameState s, Move m)
        {
            if (s.IsFinished) return ApplyResult.Illegal;

            switch (m.Kind)
            {
                case MoveKind.MoveStone:
                {
                    int from = GameState.Mod(m.FromCell, s.Rules.boardSize);
                    if (s.GetStonesAt(s.CurrentPlayer, from) <= 0) return ApplyResult.Illegal;

                    int dir = GetMoveDir(s, s.CurrentPlayer);
                    int to = GameState.Mod(from + m.PipUsed * dir, s.Rules.boardSize);
                    if (!CanEnterCell(s, s.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A, to))
                        return ApplyResult.Illegal;

                    if (!s.RemoveStoneFromCell(s.CurrentPlayer, from)) return ApplyResult.Illegal;
                    s.AddStoneToCell(s.CurrentPlayer, to);
                    return ApplyResult.Ok;
                }
                case MoveKind.BearOff:
                {
                    int from = GameState.Mod(m.FromCell, s.Rules.boardSize);
                    if (s.GetStonesAt(s.CurrentPlayer, from) <= 0) return ApplyResult.Illegal;
                    if (!AllStonesInHome(s, s.CurrentPlayer)) return ApplyResult.Illegal;
                    if (!CanBearOff(s, s.CurrentPlayer, from, m.PipUsed)) return ApplyResult.Illegal;

                    if (!s.RemoveStoneFromCell(s.CurrentPlayer, from)) return ApplyResult.Illegal;
                    s.AddBorneOff(s.CurrentPlayer);

                    if (s.GetBorneOff(s.CurrentPlayer) >= s.Rules.totalStonesPerPlayer)
                    {
                        s.Finish(s.CurrentPlayer);
                        return ApplyResult.Finished;
                    }

                    return ApplyResult.Ok;
                }
                default:
                    return ApplyResult.Illegal;
            }
        }

        public static bool AllStonesInHome(GameState s, PlayerId player)
        {
            int total = 0;
            int homeCount = 0;
            for (int i = 0; i < s.Rules.boardSize; i++)
            {
                int count = s.GetStonesAt(player, i);
                if (count <= 0) continue;
                total += count;
                if (IsInHome(s, player, i))
                    homeCount += count;
            }

            return total == homeCount && total + s.GetBorneOff(player) == s.Rules.totalStonesPerPlayer;
        }

        public static bool CanBearOff(GameState s, PlayerId player, int fromCell, int pip)
        {
            if (!IsInHome(s, player, fromCell)) return false;

            int distance = DistanceToStart(s, player, fromCell);
            if (pip == distance)
                return true;

            if (pip > distance)
                return !HasStoneFurtherFromStart(s, player, distance);

            return false;
        }

        private static bool IsOvershootWithoutBearOff(GameState s, PlayerId player, int fromCell, int pip)
        {
            if (!IsInHome(s, player, fromCell)) return false;

            int distance = DistanceToStart(s, player, fromCell);
            return pip >= distance;
        }

        private static bool IsInHome(GameState s, PlayerId player, int cell)
        {
            int distance = DistanceToStart(s, player, cell);
            return distance >= 1 && distance <= s.Rules.homeSize;
        }

        private static int DistanceToStart(GameState s, PlayerId player, int cell)
        {
            int startCell = GetHeadCell(s, player);
            int dir = GetMoveDir(s, player);
            int delta = GameState.Mod(cell - startCell, s.Rules.boardSize);
            return (delta * dir + s.Rules.boardSize) % s.Rules.boardSize;
        }

        private static bool HasStoneFurtherFromStart(GameState s, PlayerId player, int distance)
        {
            for (int i = 0; i < s.Rules.boardSize; i++)
            {
                if (!IsInHome(s, player, i))
                    continue;

                int d = DistanceToStart(s, player, i);
                if (d > distance && s.GetStonesAt(player, i) > 0)
                    return true;
            }

            return false;
        }

        private static int GetHeadCell(GameState s, PlayerId player)
        {
            return player == PlayerId.A ? s.Rules.startCellA : s.Rules.startCellB;
        }

        private static int GetMoveDir(GameState s, PlayerId player)
        {
            return player == PlayerId.A ? s.Rules.moveDirA : s.Rules.moveDirB;
        }

        private static bool CanEnterCell(GameState s, PlayerId opponent, int cell)
        {
            int opponentCount = s.GetStonesAt(opponent, cell);
            if (opponentCount <= 0) return true;
            if (s.Rules.blockIfOpponentAnyStone) return false;
            return opponentCount == 1 && s.Rules.allowHitSingleStone;
        }

    }
}
