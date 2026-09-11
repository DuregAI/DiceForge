using System.Collections.Generic;
using System.Linq;
using Diceforge.TokenPlacement;
using NUnit.Framework;

namespace Diceforge.Tests.TokenPlacement
{
    public sealed class PlacementScenario
    {
        public string Name;
        public TokenAssignment[] Before;
        public TokenAssignment[] Expected;
        public TokenMove Move;
        public string Preferred;
        public string Moved;
        public int Pip;
        public override string ToString() => Name;
    }

    public class TokenPlacementResolverTests
    {
        internal static TokenAssignment Cell(int player, int index, int cell) =>
            new TokenAssignment($"{(player == 0 ? "A" : "B")}-{index}", player, index, TokenLocation.Cell, cell);

        public static IEnumerable<PlacementScenario> Scenarios()
        {
            yield return Scenario("Regression_5_to_0", new[] { Cell(0, 0, 1), Cell(0, 1, 5) }, 0, 5, 0, "A-1");
            yield return Scenario("Forward_across_friendly_cells", new[] { Cell(0, 0, 0), Cell(0, 1, 1), Cell(0, 2, 3) }, 0, 0, 5, "A-0");
            yield return Scenario("Backward_across_friendly_cells", new[] { Cell(1, 0, 1), Cell(1, 1, 3), Cell(1, 2, 5) }, 1, 5, 0, "B-2");
            yield return Scenario("Forward_wrap", new[] { Cell(0, 0, 5), Cell(0, 1, 1) }, 0, 5, 0, "A-0");
            yield return Scenario("Backward_wrap", new[] { Cell(1, 0, 0), Cell(1, 1, 1) }, 1, 0, 5, "B-0");
            foreach (string selection in new[] { "A-0", null, "missing", "B-0", "A-2" })
            {
                var before = new[] { Cell(0, 0, 3), Cell(0, 1, 3), Cell(0, 2, 1), Cell(1, 0, 3) };
                yield return Scenario("Selection_" + (selection ?? "none"), before, 0, 3, 4,
                    selection == "A-0" ? "A-0" : "A-1", selection);
            }
            yield return Scenario("Hit", new[] { Cell(0, 0, 1), Cell(1, 0, 3), Cell(1, 1, 4) }, 0, 1, 3, "A-0", hit: true);
            foreach (bool hit in new[] { false, true })
            {
                var before = new[] { Cell(0, 0, 1).At(TokenLocation.Bar), Cell(0, 1, 2).At(TokenLocation.Bar), Cell(1, 0, hit ? 3 : 4) };
                yield return Scenario("Bar_entry_" + hit, before, 0, -1, 3, "A-0", "A-0", hit);
            }
            yield return Scenario("Selected_bear_off", new[] { Cell(0, 0, 5), Cell(0, 1, 5) }, 0, 5, -1, "A-0", "A-0");
        }

        private static PlacementScenario Scenario(string name, TokenAssignment[] before, int player, int from, int to,
            string moved, string preferred = null, bool hit = false)
        {
            var action = new TokenMove(player, from < 0 ? TokenLocation.Bar : TokenLocation.Cell, from,
                to < 0 ? TokenLocation.BorneOff : TokenLocation.Cell, to);
            return new PlacementScenario
            {
                Name = name, Before = before, Move = action, Preferred = preferred, Moved = moved,
                Pip = name.Contains("wrap") || from < 0 || to < 0 ? 1 : System.Math.Abs(to - from),
                Expected = before.Select(t => t.Id == moved ? t.At(action.To, to) :
                    hit && t.Player != player && t.Location == TokenLocation.Cell && t.Cell == to ? t.At(TokenLocation.Bar) : t).ToArray()
            };
        }

        internal static TokenCounts Counts(IEnumerable<TokenAssignment> tokens)
        {
            var cells = new[] { new int[6], new int[6] };
            var bar = new int[2];
            var off = new int[2];
            foreach (TokenAssignment t in tokens)
                if (t.Location == TokenLocation.Cell) cells[t.Player][t.Cell]++;
                else if (t.Location == TokenLocation.Bar) bar[t.Player]++;
                else off[t.Player]++;
            return new TokenCounts(cells[0], cells[1], bar[0], bar[1], off[0], off[1]);
        }

        [TestCaseSource(nameof(Scenarios))]
        public void MovePreservesIdentity(PlacementScenario scenario)
        {
            var original = scenario.Before.ToArray();
            PlacementResult result = TokenPlacementResolver.Apply(scenario.Before, scenario.Move, scenario.Preferred, Counts(scenario.Expected));
            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(result.MovedId, Is.EqualTo(scenario.Moved));
            CollectionAssert.AreEqual(scenario.Expected, result.Assignments);
            CollectionAssert.AreEqual(original, scenario.Before, "Resolution must not mutate its input.");
            PlacementResult repeat = TokenPlacementResolver.Apply(result.Assignments, scenario.Move, scenario.Preferred, Counts(scenario.Expected));
            Assert.That(repeat.Success, Is.True, repeat.Error);
            Assert.That(repeat.MovedId, Is.Null);
            CollectionAssert.AreEqual(result.Assignments, repeat.Assignments);
        }

        [Test]
        public void InitializeAndRestartOrderByCellThenBarThenOffAndStoneIndex()
        {
            var expected = new[] { Cell(0, 0, 1), Cell(0, 1, 5), Cell(0, 2, 0).At(TokenLocation.Bar),
                Cell(0, 3, 0).At(TokenLocation.BorneOff), Cell(1, 0, 2) };
            var identities = expected.Reverse().Select(t => t.At(TokenLocation.BorneOff)).ToArray();
            var result = TokenPlacementResolver.Initialize(identities, Counts(expected));
            Assert.That(result.Success, Is.True, result.Error);
            CollectionAssert.AreEqual(expected, result.Assignments);
            var restarted = TokenPlacementResolver.Initialize(result.Assignments.Reverse().ToArray(), Counts(expected));
            CollectionAssert.AreEqual(expected, restarted.Assignments);
        }

        [Test]
        public void MismatchIsAtomicAndCanBeRecoveredWithDifferentTokenCount()
        {
            var before = new[] { Cell(0, 0, 1), Cell(0, 1, 5) };
            var expected = new[] { Cell(0, 0, 0), Cell(0, 1, 1), Cell(0, 2, 2) };
            var failure = TokenPlacementResolver.Apply(before, new TokenMove(0, TokenLocation.Cell, 5, TokenLocation.Cell, 0), "A-1", Counts(expected));
            Assert.That(failure.Success, Is.False);
            Assert.That(failure.Assignments, Is.Null);
            Assert.That(failure.MovedId, Is.Null);
            var recovered = TokenPlacementResolver.Initialize(expected.Reverse().ToArray(), Counts(expected));
            CollectionAssert.AreEqual(expected, recovered.Assignments);
            var reduced = TokenPlacementResolver.Initialize(new[] { expected[0] }, Counts(new[] { expected[0] }));
            Assert.That(reduced.Success, Is.True, reduced.Error);
            Assert.That(reduced.Assignments.Count, Is.EqualTo(1));
        }

        [Test]
        public void MissingSourceDoesNotMoveAnArbitraryActiveToken()
        {
            var before = new[] { Cell(0, 0, 1) };
            var result = TokenPlacementResolver.Apply(before, new TokenMove(0, TokenLocation.Cell, 5, TokenLocation.Cell, 0), "A-0", Counts(new[] { Cell(0, 0, 0) }));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("source"));
        }

        [Test]
        public void RejectsDuplicateIdsAndDuplicateIndicesAndInvalidCells()
        {
            var token = Cell(0, 0, 1);
            Assert.That(TokenPlacementResolver.Apply(new[] { token, token }, null, null, Counts(new[] { token, token })).Success, Is.False);
            var duplicateIndex = new TokenAssignment("other", 0, 0, TokenLocation.Cell, 1);
            Assert.That(TokenPlacementResolver.Initialize(new[] { token, duplicateIndex }, Counts(new[] { token, token })).Success, Is.False);
            Assert.That(TokenPlacementResolver.Apply(new[] { Cell(0, 0, 99) }, null, null, Counts(new[] { token })).Success, Is.False);
        }

        [Test]
        public void UnchangedSyncPreservesNoncanonicalAssignments()
        {
            var previous = new[] { Cell(0, 0, 5), Cell(0, 1, 1) };
            var result = TokenPlacementResolver.Apply(previous, null, null, Counts(previous));
            Assert.That(result.Success, Is.True, result.Error);
            CollectionAssert.AreEqual(previous, result.Assignments);
        }
    }
}
