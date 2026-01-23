using System;
using UnityEngine;

namespace Diceforge.Core
{
    public static class BattleRunner
    {
        // Чтобы "в консоли пошёл бой" сразу при Play:
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoRunOnPlay()
        {
            RunSingleMatch();
        }

        public static void RunSingleMatch()
        {
            var rules = new RulesetConfig
            {
                ringSize = 9,
                maxStep = 2,
                blocksPerPlayer = 3,
                allowBlockOnPlayers = false,
                maxTurns = 60,
                randomSeed = 12345,
                verboseLog = true
            };
            rules.Validate();

            var state = new GameState(rules);
            var botA = new BotEasy(rules.randomSeed + 100);
            var botB = new BotEasy(rules.randomSeed + 200);

            if (rules.verboseLog)
                Debug.Log("[Diceforge] Match start: " + state.DebugSnapshot());

            while (!state.IsFinished && state.TurnIndex < rules.maxTurns)
            {
                var legal = MoveGenerator.GenerateLegalMoves(state);
                if (legal.Count == 0)
                {
                    // нет ходов => поражение текущего
                    var winner = state.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A;
                    state.Finish(winner);
                    break;
                }

                var bot = state.CurrentPlayer == PlayerId.A ? botA : botB;
                var move = bot.ChooseMove(state, legal);

                MoveGenerator.ApplyMove(state, move);

                if (rules.verboseLog)
                    Debug.Log($"[Diceforge] {state.CurrentPlayer} -> {move}   {state.DebugSnapshot()}");

                if (state.IsFinished) break;

                state.AdvanceTurn();
            }

            if (!state.IsFinished)
            {
                // тайм-аут: победитель тот, кто ближе к сопернику по направлению
                var winner = DecideWinnerOnTimeout(state);
                state.Finish(winner);
            }

            Debug.Log($"[Diceforge] Match end. Winner: {state.Winner}  Turns: {state.TurnIndex}");
        }

        private static PlayerId DecideWinnerOnTimeout(GameState s)
        {
            // метрика: сколько шагов "вперёд" нужно, чтобы догнать соперника
            int distA = ForwardDistance(s.PosA, s.PosB, s.Rules.ringSize);
            int distB = ForwardDistance(s.PosB, s.PosA, s.Rules.ringSize);

            if (distA < distB) return PlayerId.A;
            if (distB < distA) return PlayerId.B;

            // ничья — пусть выигрывает тот, кто ходил первым (для детерминизма)
            return PlayerId.A;
        }

        private static int ForwardDistance(int from, int to, int ringSize)
        {
            // шаги по направлению SameDirectionLoop
            int d = to - from;
            if (d < 0) d += ringSize;
            return d;
        }
    }
}
