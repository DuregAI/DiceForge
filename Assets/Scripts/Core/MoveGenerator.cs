using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public enum MoveKind : byte
    {
        MoveOneStone = 0,
        EnterFromHand = 1
    }

    public readonly struct Move
    {
        public readonly MoveKind Kind;
        public readonly int Value;

        private Move(MoveKind kind, int value)
        {
            Kind = kind;
            Value = value;
        }

        public static Move MoveOneStone(int fromCell) => new Move(MoveKind.MoveOneStone, fromCell);
        public static Move EnterFromHand(int targetCell) => new Move(MoveKind.EnterFromHand, targetCell);

        public override string ToString()
        {
            return Kind == MoveKind.MoveOneStone ? $"Move({Value})" : $"Enter({Value})";
        }
    }

    public static class MoveGenerator
    {
        public static List<Move> GenerateLegalMoves(GameState s, int roll)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var rules = s.Rules;
            var moves = new List<Move>(32);

            if (s.IsFinished) return moves;

            if (roll <= 0)
                return moves;

            // 1) Move one stone from any occupied cell
            var current = s.CurrentPlayer;
            var opponent = current == PlayerId.A ? PlayerId.B : PlayerId.A;
            for (int cell = 0; cell < rules.ringSize; cell++)
            {
                if (s.GetStonesAt(current, cell) <= 0) continue;
                int to = GameState.Mod(cell + roll, rules.ringSize);
                if (!CanEnterCell(s, opponent, to))
                    continue;
                moves.Add(Move.MoveOneStone(cell));
            }

            // 2) Enter from hand (into start cell)
            if (s.GetStonesInHand(current) > 0)
            {
                int target = current == PlayerId.A ? rules.startCellA : rules.startCellB;
                target = GameState.Mod(target, rules.ringSize);
                if (CanEnterCell(s, opponent, target))
                    moves.Add(Move.EnterFromHand(target));
            }

            return moves;
        }

        public static ApplyResult ApplyMove(GameState s, Move m)
        {
            if (s.IsFinished) return ApplyResult.Illegal;

            switch (m.Kind)
            {
                case MoveKind.MoveOneStone:
                {
                    int roll = s.CurrentRoll;
                    if (roll <= 0 || roll > s.Rules.maxRoll) return ApplyResult.Illegal;

                    int from = GameState.Mod(m.Value, s.Rules.ringSize);
                    if (s.GetStonesAt(s.CurrentPlayer, from) <= 0) return ApplyResult.Illegal;

                    int to = GameState.Mod(from + roll, s.Rules.ringSize);
                    if (!CanEnterCell(s, s.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A, to))
                        return ApplyResult.Illegal;

                    if (!s.RemoveStoneFromCell(s.CurrentPlayer, from)) return ApplyResult.Illegal;
                    ResolveHitIfNeeded(s, to);
                    s.AddStoneToCell(s.CurrentPlayer, to);

                    return ApplyResult.Ok;
                }
                case MoveKind.EnterFromHand:
                {
                    if (s.GetStonesInHand(s.CurrentPlayer) <= 0) return ApplyResult.Illegal;

                    int target = s.CurrentPlayer == PlayerId.A ? s.Rules.startCellA : s.Rules.startCellB;
                    target = GameState.Mod(target, s.Rules.ringSize);
                    if (GameState.Mod(m.Value, s.Rules.ringSize) != target) return ApplyResult.Illegal;

                    if (!CanEnterCell(s, s.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A, target))
                        return ApplyResult.Illegal;

                    s.SpendStoneFromHand(s.CurrentPlayer);
                    ResolveHitIfNeeded(s, target);
                    s.AddStoneToCell(s.CurrentPlayer, target);
                    return ApplyResult.Ok;
                }
                default:
                    return ApplyResult.Illegal;
            }
        }

        private static bool CanEnterCell(GameState s, PlayerId opponent, int cell)
        {
            int opponentCount = s.GetStonesAt(opponent, cell);
            if (opponentCount <= 0) return true;
            if (opponentCount == 1 && s.Rules.allowHitSingleStone) return true;
            return false;
        }

        private static void ResolveHitIfNeeded(GameState s, int cell)
        {
            if (!s.Rules.allowHitSingleStone) return;

            PlayerId opponent = s.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A;
            if (s.GetStonesAt(opponent, cell) == 1)
            {
                s.RemoveStoneFromCell(opponent, cell);
                s.AddStoneToHand(opponent);
            }
        }
    }
}
