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
        public string rulesetId = "default";
        public string displayName = "Default Ruleset";
        public int maxRounds = 1;
        public int maxUnitsPerSide = 15;
        public bool allowReroll = false;
        public int actionsPerTurn = 2;

        public GameMode gameMode = GameMode.Long;

        public int boardSize = 24;
        public int homeSize = 6;
        public int totalStonesPerPlayer = 15;

        public int dieMin = 1;
        public int dieMax = 6;

        public DiceBagDefinition diceBagA;
        public DiceBagDefinition diceBagB;

        public int startCellA = 0;
        public int startCellB = 12;

        public bool allowHitSingleStone = false;
        public bool blockIfOpponentAnyStone = true;

        public int maxTurns = 120;
        public int randomSeed = 12345;
        public bool verboseLog = true;

        public HeadRuleConfig headRules = new HeadRuleConfig();

        public static RulesetConfig FromPreset(Presets.RulesetPreset preset)
        {
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));

            var config = new RulesetConfig
            {
                rulesetId = preset.rulesetId,
                displayName = preset.displayName,
                maxRounds = preset.maxRounds,
                maxUnitsPerSide = preset.maxUnitsPerSide,
                allowReroll = preset.allowReroll,
                actionsPerTurn = preset.actionsPerTurn,
                gameMode = preset.gameMode,
                boardSize = preset.boardSize,
                homeSize = preset.homeSize,
                totalStonesPerPlayer = preset.totalStonesPerPlayer,
                dieMin = preset.dieMin,
                dieMax = preset.dieMax,
                diceBagA = preset.diceBagA,
                diceBagB = preset.diceBagB,
                startCellA = preset.startCellA,
                startCellB = preset.startCellB,
                allowHitSingleStone = preset.allowHitSingleStone,
                blockIfOpponentAnyStone = preset.blockIfOpponentAnyStone,
                maxTurns = preset.maxTurns,
                randomSeed = preset.randomSeed,
                verboseLog = preset.verboseLog,
                headRules = CloneHeadRules(preset.headRules)
            };

            return config;
        }

        private static HeadRuleConfig CloneHeadRules(HeadRuleConfig source)
        {
            if (source == null)
                return new HeadRuleConfig();

            var clone = new HeadRuleConfig
            {
                restrictHeadMoves = source.restrictHeadMoves,
                maxHeadMovesPerTurn = source.maxHeadMovesPerTurn,
                firstTurnHeadAllowance = new List<HeadRuleEntry>()
            };

            if (source.firstTurnHeadAllowance != null)
            {
                foreach (var entry in source.firstTurnHeadAllowance)
                {
                    if (entry == null)
                        continue;

                    clone.firstTurnHeadAllowance.Add(new HeadRuleEntry(entry.dieA, entry.dieB, entry.maxHeadMoves));
                }
            }

            return clone;
        }

        public void Validate()
        {
            maxRounds = Math.Clamp(maxRounds, 1, 9999);
            maxUnitsPerSide = Math.Clamp(maxUnitsPerSide, 1, 999);
            actionsPerTurn = Math.Clamp(actionsPerTurn, 1, 10);
            boardSize = Math.Clamp(boardSize, 6, 48);
            homeSize = Math.Clamp(homeSize, 1, boardSize / 2);
            totalStonesPerPlayer = Math.Clamp(totalStonesPerPlayer, 1, 30);
            dieMin = Math.Clamp(dieMin, 1, 999);
            dieMax = Math.Clamp(dieMax, 1, 999);
            if (dieMax < dieMin)
                dieMax = dieMin;
            startCellA = Math.Clamp(startCellA, 0, boardSize - 1);
            startCellB = Math.Clamp(startCellB, 0, boardSize - 1);
            maxTurns = Math.Clamp(maxTurns, 1, 9999);
            headRules?.Validate();
        }
    }
}
