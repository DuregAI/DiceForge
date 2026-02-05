using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public enum MovePathClassification : byte
    {
        Invalid = 0,
        Normal = 1,
        ExactBearOff = 2,
        Overshoot = 3
    }

    public readonly struct PlayerPathInfo
    {
        public PlayerPathInfo(int boardSize, int startCell, int moveDir, int homeSize)
        {
            BoardSize = boardSize;
            StartCell = startCell;
            MoveDir = moveDir;
            HomeSize = homeSize;
        }

        public int BoardSize { get; }
        public int StartCell { get; }
        public int MoveDir { get; }
        public int HomeSize { get; }
        public int BearOffProgress => BoardSize;
        public int HomeStartProgress => BoardSize - HomeSize;
    }

    public static class BoardPathRules
    {
        public static PlayerPathInfo GetPathInfo(RulesetConfig rules, PlayerId player)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            int startCell = player == PlayerId.A ? rules.startCellA : rules.startCellB;
            int moveDir = player == PlayerId.A ? rules.moveDirA : rules.moveDirB;
            return new PlayerPathInfo(rules.boardSize, startCell, moveDir, rules.homeSize);
        }

        public static IReadOnlyList<int> GetHomeCells(RulesetConfig rules, PlayerId player)
        {
            var info = GetPathInfo(rules, player);
            if (info.BoardSize <= 0 || info.HomeSize <= 0)
                return Array.Empty<int>();

            var cells = new int[info.HomeSize];
            for (int i = 0; i < info.HomeSize; i++)
            {
                int progress = info.HomeStartProgress + i;
                cells[i] = ProgressToCell(info, progress);
            }

            return cells;
        }

        public static bool IsInHome(RulesetConfig rules, PlayerId player, int cell)
        {
            var info = GetPathInfo(rules, player);
            int progress = CellToProgress(info, cell);
            return progress >= info.HomeStartProgress && progress < info.BearOffProgress;
        }

        public static int PipsToBearOff(RulesetConfig rules, PlayerId player, int cell)
        {
            var info = GetPathInfo(rules, player);
            int progress = CellToProgress(info, cell);
            return info.BearOffProgress - progress;
        }

        public static MovePathClassification ClassifyMove(
            RulesetConfig rules,
            PlayerId player,
            int fromCell,
            int steps,
            out int rawToCell,
            out int toCell)
        {
            rawToCell = fromCell;
            toCell = fromCell;

            if (rules == null || steps <= 0)
                return MovePathClassification.Invalid;

            var info = GetPathInfo(rules, player);
            if (fromCell < 0 || fromCell >= info.BoardSize)
                return MovePathClassification.Invalid;

            rawToCell = fromCell + info.MoveDir * steps;

            int fromProgress = CellToProgress(info, fromCell);
            int targetProgress = fromProgress + steps;

            if (targetProgress < info.BearOffProgress)
            {
                toCell = ProgressToCell(info, targetProgress);
                return MovePathClassification.Normal;
            }

            if (targetProgress == info.BearOffProgress)
                return MovePathClassification.ExactBearOff;

            return MovePathClassification.Overshoot;
        }

        public static int CellToProgress(PlayerPathInfo info, int cell)
        {
            int signedDelta = (cell - info.StartCell) * info.MoveDir;
            return WrapIndex(signedDelta, info.BoardSize);
        }

        public static int ProgressToCell(PlayerPathInfo info, int progress)
        {
            int raw = info.StartCell + info.MoveDir * progress;
            return WrapIndex(raw, info.BoardSize);
        }

        private static int WrapIndex(int index, int boardSize)
        {
            int wrapped = index % boardSize;
            return wrapped < 0 ? wrapped + boardSize : wrapped;
        }
    }
}
