using System;
using System.Collections.Generic;

namespace Diceforge.TokenPlacement
{
    public enum TokenLocation { Cell, Bar, BorneOff }

    public readonly struct TokenAssignment
    {
        public string Id { get; }
        public int Player { get; }
        public int StoneIndex { get; }
        public TokenLocation Location { get; }
        public int Cell { get; }

        public TokenAssignment(string id, int player, int stoneIndex, TokenLocation location, int cell = -1)
        {
            Id = id;
            Player = player;
            StoneIndex = stoneIndex;
            Location = location;
            Cell = location == TokenLocation.Cell ? cell : -1;
        }

        public TokenAssignment At(TokenLocation location, int cell = -1) =>
            new TokenAssignment(Id, Player, StoneIndex, location, cell);
    }

    // A snapshot owns its counts; callers cannot change them during resolution.
    public sealed class TokenCounts
    {
        private readonly int[][] _counts;
        public int BoardSize { get; }

        public TokenCounts(int[] cellsA, int[] cellsB, int barA, int barB, int offA, int offB)
        {
            if (cellsA == null || cellsB == null || cellsA.Length == 0 || cellsA.Length != cellsB.Length)
                throw new ArgumentException("Both players must have the same nonempty board.");
            BoardSize = cellsA.Length;
            _counts = new[] { new int[BoardSize + 2], new int[BoardSize + 2] };
            Array.Copy(cellsA, _counts[0], BoardSize);
            Array.Copy(cellsB, _counts[1], BoardSize);
            _counts[0][BoardSize] = barA;
            _counts[1][BoardSize] = barB;
            _counts[0][BoardSize + 1] = offA;
            _counts[1][BoardSize + 1] = offB;
            foreach (int[] counts in _counts)
                foreach (int count in counts)
                    if (count < 0) throw new ArgumentException("Token counts cannot be negative.");
        }

        public int Get(int player, TokenLocation location, int cell = -1) =>
            _counts[player][location == TokenLocation.Cell ? cell : BoardSize + (location == TokenLocation.Bar ? 0 : 1)];

        public int Total(int player)
        {
            int total = 0;
            foreach (int count in _counts[player]) total += count;
            return total;
        }
    }

    public readonly struct TokenMove
    {
        public int Player { get; }
        public TokenLocation From { get; }
        public int FromCell { get; }
        public TokenLocation To { get; }
        public int ToCell { get; }

        public TokenMove(int player, TokenLocation from, int fromCell, TokenLocation to, int toCell)
        {
            Player = player;
            From = from;
            FromCell = fromCell;
            To = to;
            ToCell = toCell;
        }
    }

    public sealed class PlacementResult
    {
        public bool Success => Error == null;
        public IReadOnlyList<TokenAssignment> Assignments { get; }
        public string MovedId { get; }
        public string Error { get; }

        internal PlacementResult(List<TokenAssignment> assignments, string movedId = null, string error = null)
        {
            Assignments = assignments?.AsReadOnly();
            MovedId = movedId;
            Error = error;
        }
    }

    public static class TokenPlacementResolver
    {
        public static PlacementResult Initialize(IReadOnlyList<TokenAssignment> identities, TokenCounts counts)
        {
            string error = ValidateIdentities(identities);
            if (error != null) return Failure(error);
            if (counts == null) return Failure("Missing authoritative counts.");
            var ordered = new List<TokenAssignment>(identities);
            ordered.Sort((a, b) => a.Player != b.Player ? a.Player.CompareTo(b.Player) : a.StoneIndex.CompareTo(b.StoneIndex));
            var result = new List<TokenAssignment>(ordered.Count);
            int cursor = 0;
            for (int player = 0; player < 2; player++)
            {
                for (int slot = 0; slot < counts.BoardSize + 2; slot++)
                {
                    TokenLocation location = slot < counts.BoardSize ? TokenLocation.Cell :
                        slot == counts.BoardSize ? TokenLocation.Bar : TokenLocation.BorneOff;
                    int count = counts.Get(player, location, slot);
                    for (int i = 0; i < count; i++)
                    {
                        if (cursor >= ordered.Count || ordered[cursor].Player != player)
                            return Failure("Not enough token identities for authoritative counts.");
                        result.Add(ordered[cursor++].At(location, slot));
                    }
                }
            }
            if (cursor != ordered.Count) return Failure("Too many token identities for authoritative counts.");
            return Checked(result, counts);
        }

        // A null move synchronizes an unchanged snapshot without reallocating identities.
        public static PlacementResult Apply(IReadOnlyList<TokenAssignment> previous, TokenMove? move,
            string preferredId, TokenCounts counts)
        {
            string error = ValidateIdentities(previous);
            if (error != null) return Failure(error);
            if (counts == null) return Failure("Missing authoritative counts.");
            foreach (TokenAssignment token in previous)
                if (!ValidPosition(token.Location, token.Cell, counts.BoardSize))
                    return Failure($"Token '{token.Id}' has an invalid position.");

            var result = new List<TokenAssignment>(previous);
            if (ValidateCounts(result, counts) == null) return new PlacementResult(result);
            if (!move.HasValue) return Failure("Counts changed without a move.");
            TokenMove action = move.Value;
            if (action.Player < 0 || action.Player > 1 ||
                !ValidPosition(action.From, action.FromCell, counts.BoardSize) ||
                !ValidPosition(action.To, action.ToCell, counts.BoardSize) ||
                action.From == TokenLocation.BorneOff || action.To == TokenLocation.Bar ||
                (action.From == TokenLocation.Bar && action.To != TokenLocation.Cell))
                return Failure("Invalid move description.");

            int moved = -1;
            for (int i = 0; i < previous.Count; i++)
            {
                TokenAssignment token = previous[i];
                if (token.Player != action.Player || token.Location != action.From ||
                    (action.From == TokenLocation.Cell && token.Cell != action.FromCell)) continue;
                if (token.Id == preferredId) { moved = i; break; }
                if (moved < 0 || token.StoneIndex > previous[moved].StoneIndex) moved = i;
            }
            if (moved < 0) return Failure("No token belongs to the moving player at the source.");
            result[moved] = previous[moved].At(action.To, action.ToCell);

            if (action.To == TokenLocation.Cell)
            {
                int victim = -1;
                int occupants = 0;
                for (int i = 0; i < previous.Count; i++)
                {
                    TokenAssignment token = previous[i];
                    if (token.Player != action.Player && token.Location == TokenLocation.Cell && token.Cell == action.ToCell)
                    { victim = i; occupants++; }
                }
                int remaining = counts.Get(1 - action.Player, TokenLocation.Cell, action.ToCell);
                if (occupants == 1 && remaining == 0)
                    result[victim] = previous[victim].At(TokenLocation.Bar);
            }
            return Checked(result, counts, previous[moved].Id);
        }

        private static PlacementResult Checked(List<TokenAssignment> result, TokenCounts counts, string movedId = null)
        {
            string error = ValidateCounts(result, counts);
            return error == null ? new PlacementResult(result, movedId) : Failure(error);
        }

        private static string ValidateIdentities(IReadOnlyList<TokenAssignment> tokens)
        {
            if (tokens == null) return "Missing previous assignments.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var indices = new HashSet<(int, int)>();
            foreach (TokenAssignment token in tokens)
            {
                if (string.IsNullOrEmpty(token.Id) || !ids.Add(token.Id)) return "Missing or duplicate token ID.";
                if (token.Player < 0 || token.Player > 1 || token.StoneIndex < 0 || !indices.Add((token.Player, token.StoneIndex)))
                    return $"Token '{token.Id}' has an invalid or duplicate player/index.";
            }
            return null;
        }

        private static bool ValidPosition(TokenLocation location, int cell, int size) =>
            location == TokenLocation.Cell ? cell >= 0 && cell < size :
            (location == TokenLocation.Bar || location == TokenLocation.BorneOff) && cell == -1;

        private static string ValidateCounts(IReadOnlyList<TokenAssignment> tokens, TokenCounts counts)
        {
            var actual = new int[2, counts.BoardSize + 2];
            foreach (TokenAssignment token in tokens)
            {
                if (!ValidPosition(token.Location, token.Cell, counts.BoardSize)) return $"Token '{token.Id}' has an invalid position.";
                int slot = token.Location == TokenLocation.Cell ? token.Cell : counts.BoardSize + (token.Location == TokenLocation.Bar ? 0 : 1);
                actual[token.Player, slot]++;
            }
            for (int player = 0; player < 2; player++)
                for (int slot = 0; slot < counts.BoardSize + 2; slot++)
                {
                    TokenLocation location = slot < counts.BoardSize ? TokenLocation.Cell :
                        slot == counts.BoardSize ? TokenLocation.Bar : TokenLocation.BorneOff;
                    int expected = counts.Get(player, location, slot);
                    if (actual[player, slot] != expected)
                        return $"Player {player}, {location} {slot}: assigned {actual[player, slot]}, expected {expected}.";
                }
            return null;
        }

        private static PlacementResult Failure(string error) => new PlacementResult(null, error: error);
    }
}
