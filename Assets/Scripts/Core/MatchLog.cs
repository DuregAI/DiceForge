using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public enum MatchEndReason : byte
    {
        None = 0,
        Win = 1,
        Timeout = 2,
        NoMoves = 3
    }

    public enum ApplyResult : byte
    {
        Ok = 0,
        Illegal = 1,
        Finished = 2
    }

    public readonly struct MoveRecord
    {
        public int TurnIndex { get; }
        public PlayerId PlayerId { get; }
        public Move? Move { get; }
        public int PosABefore { get; }
        public int PosBBefore { get; }
        public int PosAAfter { get; }
        public int PosBAfter { get; }
        public int? BlockCell { get; }
        public ApplyResult ApplyResult { get; }
        public MatchEndReason EndReason { get; }
        public PlayerId? Winner { get; }

        public MoveRecord(
            int turnIndex,
            PlayerId playerId,
            Move? move,
            int posABefore,
            int posBBefore,
            int posAAfter,
            int posBAfter,
            int? blockCell,
            ApplyResult applyResult,
            MatchEndReason endReason,
            PlayerId? winner)
        {
            TurnIndex = turnIndex;
            PlayerId = playerId;
            Move = move;
            PosABefore = posABefore;
            PosBBefore = posBBefore;
            PosAAfter = posAAfter;
            PosBAfter = posBAfter;
            BlockCell = blockCell;
            ApplyResult = applyResult;
            EndReason = endReason;
            Winner = winner;
        }
    }

    public sealed class MatchLog
    {
        private readonly List<MoveRecord> _records = new List<MoveRecord>();

        public IReadOnlyList<MoveRecord> Records => _records;
        public int Count => _records.Count;

        public MoveRecord? Last => _records.Count == 0 ? null : _records[^1];

        public void Add(MoveRecord record)
        {
            _records.Add(record);
        }

        public MoveRecord? GetRecordAt(int index)
        {
            if (index < 0 || index >= _records.Count)
                return null;
            return _records[index];
        }

        public void Clear()
        {
            _records.Clear();
        }
    }
}
