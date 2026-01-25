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
            if (legal == null || legal.Count == 0)
                return default;

            foreach (var m in legal)
            {
                if (m.Kind == MoveKind.BearOff)
                    return m;
            }

            return legal[_rng.Next(legal.Count)];
        }
    }
}
