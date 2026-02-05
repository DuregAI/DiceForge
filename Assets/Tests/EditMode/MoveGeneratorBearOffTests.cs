using System.Linq;
using Diceforge.Core;
using NUnit.Framework;

namespace Diceforge.Tests.EditMode
{
    public class MoveGeneratorBearOffTests
    {
        [Test]
        public void OversizedBearOff_IsBlocked_WhenFartherStoneExists()
        {
            var rules = CreateRules(totalStones: 2);
            var state = new GameState(rules);
            ClearPlayerStones(state, PlayerId.A);

            int pips6Cell = GetHomeCellByPips(rules, PlayerId.A, 6);
            int pips5Cell = GetHomeCellByPips(rules, PlayerId.A, 5);
            state.AddStoneToCell(PlayerId.A, pips6Cell);
            state.AddStoneToCell(PlayerId.A, pips5Cell);

            var legal = MoveGenerator.GenerateLegalMoves(state, dieValue: 6, headMovesUsed: 0, maxHeadMovesThisTurn: 1);

            Assert.That(legal.Any(m => m.Kind == MoveKind.BearOff && m.FromCell == pips6Cell), Is.True,
                "Exact bear-off from pips=6 should be legal.");
            Assert.That(legal.Any(m => m.Kind == MoveKind.BearOff && m.FromCell == pips5Cell), Is.False,
                "Oversized bear-off from pips=5 should be blocked while pips=6 is occupied.");
        }

        [Test]
        public void OversizedBearOff_IsAllowed_WhenNoFartherStoneExists()
        {
            var rules = CreateRules(totalStones: 1);
            var state = new GameState(rules);
            ClearPlayerStones(state, PlayerId.A);

            int pips5Cell = GetHomeCellByPips(rules, PlayerId.A, 5);
            state.AddStoneToCell(PlayerId.A, pips5Cell);

            var legal = MoveGenerator.GenerateLegalMoves(state, dieValue: 6, headMovesUsed: 0, maxHeadMovesThisTurn: 1);

            Assert.That(legal.Any(m => m.Kind == MoveKind.BearOff && m.FromCell == pips5Cell), Is.True,
                "Oversized bear-off from pips=5 should be legal when it is the farthest occupied home point.");
        }

        [Test]
        public void BearOff_IsNotAllowed_FromOutsideHomeZone()
        {
            var rules = CreateRules(totalStones: 1);
            var state = new GameState(rules);
            ClearPlayerStones(state, PlayerId.A);

            int outsideHomeCell = 0;
            Assert.That(BoardPathRules.IsInHome(rules, PlayerId.A, outsideHomeCell), Is.False,
                "Sanity check: selected cell must be outside home zone.");

            state.AddStoneToCell(PlayerId.A, outsideHomeCell);

            var legal = MoveGenerator.GenerateLegalMoves(state, dieValue: 6, headMovesUsed: 0, maxHeadMovesThisTurn: 1);

            Assert.That(legal.Any(m => m.Kind == MoveKind.BearOff), Is.False,
                "Bear-off must not be generated for stones outside home.");
        }

        private static RulesetConfig CreateRules(int totalStones)
        {
            var rules = new RulesetConfig
            {
                boardSize = 24,
                homeSize = 6,
                totalStonesPerPlayer = totalStones,
                startCellA = 0,
                startCellB = 12,
                moveDirA = 1,
                moveDirB = -1,
                verboseLog = false,
                blockIfOpponentAnyStone = true,
                allowHitSingleStone = false
            };

            rules.Validate();
            return rules;
        }

        private static int GetHomeCellByPips(RulesetConfig rules, PlayerId player, int pips)
        {
            var homeCells = BoardPathRules.GetHomeCells(rules, player);
            return homeCells.First(cell => BoardPathRules.PipsToBearOff(rules, player, cell) == pips);
        }

        private static void ClearPlayerStones(GameState state, PlayerId player)
        {
            for (int cell = 0; cell < state.Rules.boardSize; cell++)
            {
                while (state.GetStonesAt(player, cell) > 0)
                    state.RemoveStoneFromCell(player, cell);
            }
        }
    }
}
