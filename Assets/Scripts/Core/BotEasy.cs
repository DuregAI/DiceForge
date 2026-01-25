using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public sealed class BotEasy
    {
        private readonly Random _rng;

        public BotEasy(int seed)
        {
            _rng = new Random(seed);
        }

        public Move ChooseMove(GameState s, List<Move> legal)
        {
            var opponent = s.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A;

            // 1) если можно сделать hit — делаем это
            foreach (var m in legal)
            {
                int to = GetTargetCell(s, m);
                if (to < 0) continue;
                if (s.GetStonesAt(opponent, to) == 1)
                    return m;
            }

            // 2) иначе — случайный легальный
            return legal[_rng.Next(legal.Count)];
        }

        private static int GetTargetCell(GameState s, Move move)
        {
            if (move.Kind == MoveKind.MoveOneStone)
            {
                int from = GameState.Mod(move.Value, s.Rules.ringSize);
                return GameState.Mod(from + s.CurrentRoll, s.Rules.ringSize);
            }

            if (move.Kind == MoveKind.EnterFromHand)
                return GameState.Mod(move.Value, s.Rules.ringSize);

            return -1;
        }
    }
}
