using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public enum GameMode : byte
    {
        Long = 0,
        Short = 1
    }

    [Serializable]
    public sealed class HeadRuleEntry
    {
        public int dieA;
        public int dieB;
        public int maxHeadMoves;

        public HeadRuleEntry(int dieA, int dieB, int maxHeadMoves)
        {
            this.dieA = dieA;
            this.dieB = dieB;
            this.maxHeadMoves = maxHeadMoves;
        }

        public bool Matches(int a, int b)
        {
            return (dieA == a && dieB == b) || (dieA == b && dieB == a);
        }
    }

    [Serializable]
    public sealed class HeadRuleConfig
    {
        public bool restrictHeadMoves = true;
        public int maxHeadMovesPerTurn = 1;
        public List<HeadRuleEntry> firstTurnHeadAllowance = new List<HeadRuleEntry>
        {
            new HeadRuleEntry(6, 6, 2),
            new HeadRuleEntry(4, 4, 2),
            new HeadRuleEntry(3, 3, 2)
        };

        public int? GetFirstTurnAllowance(int dieA, int dieB)
        {
            foreach (var entry in firstTurnHeadAllowance)
            {
                if (entry != null && entry.Matches(dieA, dieB))
                    return entry.maxHeadMoves;
            }

            return null;
        }

        public void Validate()
        {
            maxHeadMovesPerTurn = Math.Clamp(maxHeadMovesPerTurn, 0, 4);
            foreach (var entry in firstTurnHeadAllowance)
            {
                if (entry == null) continue;
                entry.dieA = Math.Clamp(entry.dieA, 1, 6);
                entry.dieB = Math.Clamp(entry.dieB, 1, 6);
                entry.maxHeadMoves = Math.Clamp(entry.maxHeadMoves, 0, 4);
            }
        }
    }

    [Serializable]
    public sealed class RulesetConfig
    {
        public GameMode gameMode = GameMode.Long;

        public int boardSize = 24;
        public int homeSize = 6;
        public int totalStonesPerPlayer = 15;

        public int startCellA = 0;
        public int startCellB = 12;

        public bool allowHitSingleStone = false;
        public bool blockIfOpponentAnyStone = true;

        public int maxTurns = 120;
        public int randomSeed = 12345;
        public bool verboseLog = true;

        public HeadRuleConfig headRules = new HeadRuleConfig();

        public void Validate()
        {
            boardSize = Math.Clamp(boardSize, 6, 48);
            homeSize = Math.Clamp(homeSize, 1, boardSize / 2);
            totalStonesPerPlayer = Math.Clamp(totalStonesPerPlayer, 1, 30);
            startCellA = Math.Clamp(startCellA, 0, boardSize - 1);
            startCellB = Math.Clamp(startCellB, 0, boardSize - 1);
            maxTurns = Math.Clamp(maxTurns, 1, 9999);
            headRules?.Validate();
        }
    }
}
