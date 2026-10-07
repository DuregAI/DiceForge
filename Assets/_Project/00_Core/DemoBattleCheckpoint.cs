using System;

namespace Diceforge.Core
{
    // Board simulation only. Campaign identity and named hero assignments belong to the caller.
    [Serializable]
    public sealed class DemoBattleCheckpoint
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string rulesetId;
        public int boardSize;
        public int[] cellsA, cellsB;
        public int borneOffA, borneOffB, barA, barB;
        public int turnIndex, turnsTakenA, turnsTakenB, currentPlayer;
        public string outcomeLabel;
        public int[] outcomeDice, remainingDice, usedDice;
        public int selectedDieIndex = -1;
        public int headMovesUsed, headMovesLimit;
        public int trailHazardCell = -1;
        public bool trailHazardYielded;
        public bool finished;
        public int winner = -1;
        public MatchEndReason endReason;
        public int orderedBagCursor;
    }
}
