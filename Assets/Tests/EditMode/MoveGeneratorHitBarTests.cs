using System.Collections.Generic;
using System.Linq;
using Diceforge.Core;
using NUnit.Framework;

namespace Diceforge.Tests.EditMode
{
    public class MoveGeneratorHitBarTests
    {
        [Test]
        public void NormalMove_HitsSingleOpponentStone_AndSendsToBar()
        {
            var rules = CreateRules();
            var state = new GameState(rules);
            ClearBoard(state);

            state.AddStoneToCell(PlayerId.A, 0);
            state.AddStoneToCell(PlayerId.B, 3);

            var legal = MoveGenerator.GenerateLegalMoves(state, dieValue: 3, headMovesUsed: 0, maxHeadMovesThisTurn: 1);
            Assert.That(legal.Any(m => m.Kind == MoveKind.MoveStone && m.FromCell == 0), Is.True);

            var result = MoveGenerator.ApplyMove(state, legal.First(m => m.Kind == MoveKind.MoveStone && m.FromCell == 0));
            Assert.That(result, Is.EqualTo(ApplyResult.Ok));
            Assert.That(state.GetStonesAt(PlayerId.A, 3), Is.EqualTo(1));
            Assert.That(state.GetStonesAt(PlayerId.B, 3), Is.EqualTo(0));
            Assert.That(state.GetBarCount(PlayerId.B), Is.EqualTo(1));
        }

        [Test]
        public void MoveToBlockedPoint_IsIllegal_WhenOpponentHasTwoOrMore()
        {
            var rules = CreateRules();
            var state = new GameState(rules);
            ClearBoard(state);

            state.AddStoneToCell(PlayerId.A, 0);
            state.AddStoneToCell(PlayerId.B, 3);
            state.AddStoneToCell(PlayerId.B, 3);

            var legal = MoveGenerator.GenerateLegalMoves(state, dieValue: 3, headMovesUsed: 0, maxHeadMovesThisTurn: 1);
            Assert.That(legal.Any(m => m.Kind == MoveKind.MoveStone && m.FromCell == 0), Is.False);
        }

        [Test]
        public void BarEntry_IsForced_AndUsesOpponentHomeMapping()
        {
            var rules = CreateRules();
            var state = new GameState(rules);
            ClearBoard(state);

            state.AddStoneToCell(PlayerId.A, 5);
            state.AddToBar(PlayerId.A);

            var die = 1;
            var legal = MoveGenerator.GenerateLegalMoves(state, dieValue: die, headMovesUsed: 0, maxHeadMovesThisTurn: 1);
            Assert.That(legal.Count, Is.EqualTo(1));
            Assert.That(legal[0].Kind, Is.EqualTo(MoveKind.EnterFromBar));

            var entryCells = MoveGenerator.GetEntryCellsForPlayer(rules, PlayerId.A);
            var opponentHome = BoardPathRules.GetHomeCells(rules, PlayerId.B);
            Assert.That(entryCells[0], Is.EqualTo(opponentHome[opponentHome.Count - 1]), "die=1 must map to farthest opponent-home entry.");
            Assert.That(entryCells[rules.homeSize - 1], Is.EqualTo(opponentHome[0]), "die=6 must map to nearest opponent-home entry.");

            var result = MoveGenerator.ApplyMove(state, legal[0]);
            Assert.That(result, Is.EqualTo(ApplyResult.Ok));
            Assert.That(state.GetBarCount(PlayerId.A), Is.EqualTo(0));
            Assert.That(state.GetStonesAt(PlayerId.A, entryCells[die - 1]), Is.EqualTo(1));
            Assert.That(state.GetStonesAt(PlayerId.A, 5), Is.EqualTo(1), "Normal stones must remain untouched while re-entering from bar.");
        }

        [Test]
        public void BarEntry_HitsSingleOpponentStone()
        {
            var rules = CreateRules();
            var state = new GameState(rules);
            ClearBoard(state);

            state.AddToBar(PlayerId.A);
            var entryCells = MoveGenerator.GetEntryCellsForPlayer(rules, PlayerId.A);
            int target = entryCells[2]; // die=3
            state.AddStoneToCell(PlayerId.B, target);

            var legal = MoveGenerator.GenerateLegalMoves(state, dieValue: 3, headMovesUsed: 0, maxHeadMovesThisTurn: 1);
            Assert.That(legal.Count, Is.EqualTo(1));
            Assert.That(legal[0].Kind, Is.EqualTo(MoveKind.EnterFromBar));

            var result = MoveGenerator.ApplyMove(state, legal[0]);
            Assert.That(result, Is.EqualTo(ApplyResult.Ok));
            Assert.That(state.GetBarCount(PlayerId.A), Is.EqualTo(0));
            Assert.That(state.GetBarCount(PlayerId.B), Is.EqualTo(1));
            Assert.That(state.GetStonesAt(PlayerId.A, target), Is.EqualTo(1));
            Assert.That(state.GetStonesAt(PlayerId.B, target), Is.EqualTo(0));
        }

        [Test]
        public void BarEntry_NoLegalMove_WhenAllEntryDoorsBlocked()
        {
            var rules = CreateRules();
            var state = new GameState(rules);
            ClearBoard(state);

            state.AddToBar(PlayerId.A);
            var entryCells = MoveGenerator.GetEntryCellsForPlayer(rules, PlayerId.A);
            foreach (int cell in entryCells)
            {
                state.AddStoneToCell(PlayerId.B, cell);
                state.AddStoneToCell(PlayerId.B, cell);
            }

            for (int die = 1; die <= rules.homeSize; die++)
            {
                var legal = MoveGenerator.GenerateLegalMoves(state, dieValue: die, headMovesUsed: 0, maxHeadMovesThisTurn: 1);
                Assert.That(legal, Is.Empty, $"die {die} should have no entry when all doors are blocked.");
            }
        }

        [Test]
        public void Runner_ProgressesWithoutSoftLock_WithHitBarRules()
        {
            var rules = CreateRules();
            rules.maxTurns = 400;

            var outcomes = new List<DiceOutcomeData>
            {
                new DiceOutcomeData("d1", 1, new[] { 1 }),
                new DiceOutcomeData("d2", 1, new[] { 2 }),
                new DiceOutcomeData("d3", 1, new[] { 3 }),
                new DiceOutcomeData("d4", 1, new[] { 4 }),
                new DiceOutcomeData("d5", 1, new[] { 5 }),
                new DiceOutcomeData("d6", 1, new[] { 6 })
            };
            var bag = new DiceBagConfigData(DiceBagDrawMode.Shuffled, outcomes);

            for (int i = 0; i < 10; i++)
            {
                var runner = new BattleRunner();
                runner.Init(rules, bag, bag, seed: 100 + i);

                int safety = 0;
                while (!runner.State.IsFinished && safety++ < 5000)
                    runner.Tick();

                Assert.That(runner.State.IsFinished, Is.True, $"match {i} should finish and not soft-lock.");
            }
        }

        private static RulesetConfig CreateRules()
        {
            var rules = new RulesetConfig
            {
                boardSize = 24,
                homeSize = 6,
                totalStonesPerPlayer = 15,
                startCellA = 0,
                startCellB = 12,
                moveDirA = 1,
                moveDirB = -1,
                verboseLog = false,
                blockIfOpponentAnyStone = false,
                allowHitSingleStone = true,
                maxTurns = 300
            };

            rules.Validate();
            return rules;
        }

        private static void ClearBoard(GameState state)
        {
            for (int cell = 0; cell < state.Rules.boardSize; cell++)
            {
                while (state.GetStonesAt(PlayerId.A, cell) > 0)
                    state.RemoveStoneFromCell(PlayerId.A, cell);
                while (state.GetStonesAt(PlayerId.B, cell) > 0)
                    state.RemoveStoneFromCell(PlayerId.B, cell);
            }
        }
    }
}
