using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public enum MoveKind : byte
    {
        MoveStone = 0,
        BearOff = 1,
        EnterFromBar = 2
    }

    public readonly struct Move : IEquatable<Move>
    {
        public MoveKind Kind { get; }
        public int FromCell { get; }
        public int PipUsed { get; }

        private Move(MoveKind kind, int fromCell, int pipUsed)
        {
            Kind = kind;
            FromCell = fromCell;
            PipUsed = pipUsed;
        }

        public static Move MoveStone(int fromCell, int pipUsed) => new Move(MoveKind.MoveStone, fromCell, pipUsed);
        public static Move BearOff(int fromCell, int pipUsed) => new Move(MoveKind.BearOff, fromCell, pipUsed);
        public static Move EnterFromBar(int pipUsed) => new Move(MoveKind.EnterFromBar, -1, pipUsed);

        public override string ToString()
        {
            if (Kind == MoveKind.BearOff)
                return $"BearOff({FromCell}, {PipUsed})";
            if (Kind == MoveKind.EnterFromBar)
                return $"EnterFromBar({PipUsed})";
            return $"Move({FromCell}, {PipUsed})";
        }

        public bool Equals(Move other)
        {
            return Kind == other.Kind && FromCell == other.FromCell && PipUsed == other.PipUsed;
        }

        public override bool Equals(object obj)
        {
            return obj is Move other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Kind, FromCell, PipUsed);
        }
    }

    public static class MoveGenerator
    {
        public static List<Move> GenerateLegalMoves(
            GameState s,
            int dieValue,
            int headMovesUsed,
            int maxHeadMovesThisTurn)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var moves = new List<Move>(32);

            if (s.IsFinished) return moves;
            if (dieValue <= 0) return moves;

            var rules = s.Rules;
            var current = s.CurrentPlayer;
            var opponent = Opponent(current);
            int barCount = s.GetBarCount(current);
            if (barCount > 0)
            {
                int? entryCell = TryGetEntryCell(rules, current, dieValue);
                if (!entryCell.HasValue)
                    return moves;

                if (CanEnterCell(s, opponent, entryCell.Value))
                    moves.Add(Move.EnterFromBar(dieValue));

                return moves;
            }

            int headCell = GetHeadCell(s, current);
            bool allInHome = AllStonesInHome(s, current);
            int maxPipsInHome = allInHome ? GetMaxPipsInHome(s.Rules, s, current) : -1;

            for (int cell = 0; cell < rules.boardSize; cell++)
            {
                if (s.GetStonesAt(current, cell) <= 0) continue;
                if (cell == headCell && rules.headRules.restrictHeadMoves && headMovesUsed >= maxHeadMovesThisTurn)
                    continue;

                var classification = BoardPathRules.ClassifyMove(rules, current, cell, dieValue, out int rawToCell, out int toCell);
                LogMoveClassification(s, current, cell, dieValue, rawToCell, classification);

                if (classification == MovePathClassification.ExactBearOff || classification == MovePathClassification.Overshoot)
                {
                    if (allInHome && CanBearOff(s, current, cell, dieValue, maxPipsInHome))
                        moves.Add(Move.BearOff(cell, dieValue));
                    continue;
                }

                if (classification == MovePathClassification.Invalid)
                    continue;

                if (!CanEnterCell(s, opponent, toCell))
                    continue;
                moves.Add(Move.MoveStone(cell, dieValue));
            }

            return moves;
        }

        public static ApplyResult ApplyMove(GameState s, Move m)
        {
            if (s.IsFinished) return ApplyResult.Illegal;

            switch (m.Kind)
            {
                case MoveKind.MoveStone:
                {
                    int from = m.FromCell;
                    if (from < 0 || from >= s.Rules.boardSize)
                        return ApplyResult.Illegal;
                    if (s.GetStonesAt(s.CurrentPlayer, from) <= 0) return ApplyResult.Illegal;
                    if (s.GetBarCount(s.CurrentPlayer) > 0) return ApplyResult.Illegal;

                    var classification = BoardPathRules.ClassifyMove(s.Rules, s.CurrentPlayer, from, m.PipUsed, out int rawToCell, out int to);
                    LogMoveClassification(s, s.CurrentPlayer, from, m.PipUsed, rawToCell, classification);
                    if (classification != MovePathClassification.Normal)
                        return ApplyResult.Illegal;

                    if (!CanEnterCell(s, s.CurrentPlayer == PlayerId.A ? PlayerId.B : PlayerId.A, to))
                        return ApplyResult.Illegal;

                    if (!s.RemoveStoneFromCell(s.CurrentPlayer, from)) return ApplyResult.Illegal;
                    TryHitSingleStone(s, Opponent(s.CurrentPlayer), to);
                    s.AddStoneToCell(s.CurrentPlayer, to);
                    return ApplyResult.Ok;
                }
                case MoveKind.EnterFromBar:
                {
                    int? entryCell = TryGetEntryCell(s.Rules, s.CurrentPlayer, m.PipUsed);
                    if (!entryCell.HasValue)
                        return ApplyResult.Illegal;
                    if (s.GetBarCount(s.CurrentPlayer) <= 0)
                        return ApplyResult.Illegal;

                    var opponent = Opponent(s.CurrentPlayer);
                    int to = entryCell.Value;
                    if (!CanEnterCell(s, opponent, to))
                        return ApplyResult.Illegal;

                    if (!s.RemoveFromBar(s.CurrentPlayer, 1))
                        return ApplyResult.Illegal;

                    TryHitSingleStone(s, opponent, to);
                    s.AddStoneToCell(s.CurrentPlayer, to);
                    return ApplyResult.Ok;
                }
                case MoveKind.BearOff:
                {
                    int from = m.FromCell;
                    if (from < 0 || from >= s.Rules.boardSize)
                        return ApplyResult.Illegal;
                    if (s.GetStonesAt(s.CurrentPlayer, from) <= 0) return ApplyResult.Illegal;
                    if (s.GetBarCount(s.CurrentPlayer) > 0) return ApplyResult.Illegal;
                    if (!AllStonesInHome(s, s.CurrentPlayer)) return ApplyResult.Illegal;
                    if (!CanBearOff(s, s.CurrentPlayer, from, m.PipUsed)) return ApplyResult.Illegal;

                    var classification = BoardPathRules.ClassifyMove(s.Rules, s.CurrentPlayer, from, m.PipUsed, out int rawToCell, out _);
                    LogMoveClassification(s, s.CurrentPlayer, from, m.PipUsed, rawToCell, classification);
                    if (classification != MovePathClassification.ExactBearOff && classification != MovePathClassification.Overshoot)
                        return ApplyResult.Illegal;

                    if (!s.RemoveStoneFromCell(s.CurrentPlayer, from)) return ApplyResult.Illegal;
                    s.AddBorneOff(s.CurrentPlayer);

                    if (s.GetBorneOff(s.CurrentPlayer) >= s.Rules.totalStonesPerPlayer)
                    {
                        s.Finish(s.CurrentPlayer);
                        return ApplyResult.Finished;
                    }

                    return ApplyResult.Ok;
                }
                default:
                    return ApplyResult.Illegal;
            }
        }

        public static bool AllStonesInHome(GameState s, PlayerId player)
        {
            int total = 0;
            int homeCount = 0;
            for (int i = 0; i < s.Rules.boardSize; i++)
            {
                int count = s.GetStonesAt(player, i);
                if (count <= 0) continue;
                total += count;
                if (IsInHome(s, player, i))
                    homeCount += count;
            }

            return total == homeCount && total + s.GetBorneOff(player) == s.Rules.totalStonesPerPlayer;
        }

        public static bool CanBearOff(GameState s, PlayerId player, int fromCell, int pip)
        {
            if (!IsInHome(s, player, fromCell)) return false;

            int maxPipsInHome = GetMaxPipsInHome(s.Rules, s, player);
            return CanBearOff(s, player, fromCell, pip, maxPipsInHome);
        }

        public static int GetMaxPipsInHome(RulesetConfig rules, GameState s, PlayerId player)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (s == null) throw new ArgumentNullException(nameof(s));

            int maxPips = -1;
            var homeCells = BoardPathRules.GetHomeCells(rules, player);
            for (int i = 0; i < homeCells.Count; i++)
            {
                int cell = homeCells[i];
                if (s.GetStonesAt(player, cell) <= 0)
                    continue;

                int pips = BoardPathRules.PipsToBearOff(rules, player, cell);
                if (pips > maxPips)
                    maxPips = pips;
            }

            return maxPips;
        }

        private static bool IsInHome(GameState s, PlayerId player, int cell)
        {
            return BoardPathRules.IsInHome(s.Rules, player, cell);
        }

        private static bool CanBearOff(GameState s, PlayerId player, int fromCell, int pip, int maxPipsInHome)
        {
            int distance = BoardPathRules.PipsToBearOff(s.Rules, player, fromCell);

            if (pip == distance)
                return true;

            if (pip > distance)
                return maxPipsInHome >= 0 && distance == maxPipsInHome;

            return false;
        }

        private static int GetHeadCell(GameState s, PlayerId player)
        {
            return player == PlayerId.A ? s.Rules.startCellA : s.Rules.startCellB;
        }

        private static bool CanEnterCell(GameState s, PlayerId opponent, int cell)
        {
            int opponentCount = s.GetStonesAt(opponent, cell);
            if (opponentCount <= 0) return true;
            if (s.Rules.blockIfOpponentAnyStone) return false;
            return opponentCount == 1 && s.Rules.allowHitSingleStone;
        }

        public static IReadOnlyList<int> GetEntryCellsForPlayer(RulesetConfig rules, PlayerId player)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            var homeCells = BoardPathRules.GetHomeCells(rules, Opponent(player));
            var ordered = new int[homeCells.Count];
            for (int i = 0; i < homeCells.Count; i++)
                ordered[i] = homeCells[homeCells.Count - 1 - i];
            return ordered;
        }

        private static int? TryGetEntryCell(RulesetConfig rules, PlayerId player, int dieValue)
        {
            var entryCells = GetEntryCellsForPlayer(rules, player);
            if (dieValue <= 0 || dieValue > entryCells.Count)
                return null;

            return entryCells[dieValue - 1];
        }

        private static PlayerId Opponent(PlayerId player)
        {
            return player == PlayerId.A ? PlayerId.B : PlayerId.A;
        }

        private static void TryHitSingleStone(GameState s, PlayerId opponent, int toCell)
        {
            int opponentCount = s.GetStonesAt(opponent, toCell);
            if (opponentCount != 1)
                return;

            if (!s.RemoveStoneFromCell(opponent, toCell))
                return;

            s.AddToBar(opponent, 1);
        }

        private static void LogMoveClassification(
            GameState state,
            PlayerId player,
            int fromCell,
            int steps,
            int rawToCell,
            MovePathClassification classification)
        {
            if (!state.Rules.verboseLog)
                return;

            Console.WriteLine($"[MovePath] {player}: from={fromCell}, steps={steps}, rawTo={rawToCell}, class={classification}");
        }

    }
}
