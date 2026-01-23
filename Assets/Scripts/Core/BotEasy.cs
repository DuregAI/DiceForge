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
            // 1) если можно выиграть шагом — делаем это
            int myPos = s.GetPos(s.CurrentPlayer);
            int oppPos = s.GetOpponentPos(s.CurrentPlayer);

            foreach (var m in legal)
            {
                if (m.Kind != MoveKind.Step) continue;
                int to = GameState.Mod(myPos + m.Value, s.Rules.ringSize);
                if (to == oppPos) return m;
            }

            // 2) иначе: попробуем поставить блок "перед" соперником (чуть-чуть мешаем)
            // (перед = oppPos+1 по SameDirection)
            int ahead = GameState.Mod(oppPos + 1, s.Rules.ringSize);
            foreach (var m in legal)
            {
                if (m.Kind == MoveKind.PlaceBlock && m.Value == ahead)
                    return m;
            }

            // 3) иначе: небольшой приоритет шагам (чтобы матч двигался)
            var stepMoves = new List<Move>();
            var blockMoves = new List<Move>();
            foreach (var m in legal)
            {
                if (m.Kind == MoveKind.Step) stepMoves.Add(m);
                else blockMoves.Add(m);
            }

            if (stepMoves.Count > 0 && _rng.NextDouble() < 0.75)
            {
                // предпочитаем "не нулевой" шаг, если есть
                for (int tries = 0; tries < 6; tries++)
                {
                    var pick = stepMoves[_rng.Next(stepMoves.Count)];
                    if (pick.Value > 0) return pick;
                }
                return stepMoves[_rng.Next(stepMoves.Count)];
            }

            // 4) fallback — случайный легальный
            return legal[_rng.Next(legal.Count)];
        }
    }
}
