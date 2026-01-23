using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public enum MoveKind : byte
    {
        Step = 0,
        PlaceChip = 1
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

        public static Move Step(int steps) => new Move(MoveKind.Step, steps);
        public static Move PlaceChip(int cellIndex) => new Move(MoveKind.PlaceChip, cellIndex);

        public override string ToString()
        {
            return Kind == MoveKind.Step ? $"Step({Value})" : $"Chip({Value})";
        }
    }

    public static class MoveGenerator
    {
        public static List<Move> GenerateLegalMoves(GameState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var rules = s.Rules;
            var moves = new List<Move>(32);

            if (s.IsFinished) return moves;

            // 1) Step move: только по броску кубика
            int steps = s.CurrentRoll;
            if (steps == 0 && !rules.allowZeroStep)
            {
                // no-op
            }
            else
            {
                int from = s.GetPos(s.CurrentPlayer);
                int to = GameState.Mod(from + steps, rules.ringSize);

                // нельзя в фишку
                if (!s.HasChip[to])
                {
                    // нельзя на соперника (для MVP сделаем столкновение = победа, поэтому разрешаем как "атаку")
                    // но чтобы был осмысленный win-condition, разрешаем ход на соперника как завершающий
                    moves.Add(Move.Step(steps));
                }
            }

            // 2) Place chip moves (если есть фишки)
            if (s.GetChipsInHand(s.CurrentPlayer) > 0)
            {
                for (int i = 0; i < rules.ringSize; i++)
                {
                    if (s.HasChip[i]) continue;

                    if (!rules.allowChipOnPlayers)
                    {
                        if (i == s.PosA || i == s.PosB) continue;
                    }

                    // не ставим блок "под себя" (чтобы не было тупняка)
                    if (i == s.GetPos(s.CurrentPlayer)) continue;

                    moves.Add(Move.PlaceChip(i));
                }
            }

            return moves;
        }

        public static ApplyResult ApplyMove(GameState s, Move m)
        {
            if (s.IsFinished) return ApplyResult.Illegal;

            switch (m.Kind)
            {
                case MoveKind.Step:
                {
                    int from = s.GetPos(s.CurrentPlayer);
                    int to = GameState.Mod(from + m.Value, s.Rules.ringSize);

                    // защита от неверных ходов (на всякий случай)
                    if (m.Value != s.CurrentRoll) return ApplyResult.Illegal;
                    if (m.Value < 0 || m.Value > s.Rules.maxStep) return ApplyResult.Illegal;
                    if (!s.Rules.allowZeroStep && m.Value == 0) return ApplyResult.Illegal;
                    if (s.HasChip[to]) return ApplyResult.Illegal;

                    s.SetPos(s.CurrentPlayer, to);

                    // win-condition MVP:
                    // если встали на клетку соперника => победа
                    if (to == s.GetOpponentPos(s.CurrentPlayer))
                    {
                        s.Finish(s.CurrentPlayer);
                        return ApplyResult.Finished;
                    }

                    return ApplyResult.Ok;
                }
                case MoveKind.PlaceChip:
                {
                    if (s.GetChipsInHand(s.CurrentPlayer) <= 0) return ApplyResult.Illegal;

                    int cell = GameState.Mod(m.Value, s.Rules.ringSize);
                    if (s.HasChip[cell]) return ApplyResult.Illegal;

                    if (!s.Rules.allowChipOnPlayers && (cell == s.PosA || cell == s.PosB))
                        return ApplyResult.Illegal;
                    if (cell == s.GetPos(s.CurrentPlayer)) return ApplyResult.Illegal;

                    s.HasChip[cell] = true;
                    s.SpendChip(s.CurrentPlayer);
                    return ApplyResult.Ok;
                }
                default:
                    return ApplyResult.Illegal;
            }
        }
    }
}
