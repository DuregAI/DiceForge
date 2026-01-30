using System;
using Diceforge.Core;
using UnityEngine;

namespace Diceforge.Presets
{
    [CreateAssetMenu(fileName = "RulesetPreset", menuName = "Diceforge/Ruleset Preset")]
    public sealed class RulesetPreset : ScriptableObject
    {
        [Header("Metadata")]
        public string rulesetId = "default";
        public string displayName = "Default Ruleset";

        [Header("Match Limits")]
        public int maxRounds = 1;
        public int maxUnitsPerSide = 15;
        public bool allowReroll = false;
        public int actionsPerTurn = 2;

        [Header("Board")]
        public GameMode gameMode = GameMode.Long;
        public int boardSize = 24;
        public int homeSize = 6;
        public int totalStonesPerPlayer = 15;

        [Header("Dice")]
        public int dieMin = 1;
        public int dieMax = 6;
        //public DiceBagDefinition diceBagA;
        //public DiceBagDefinition diceBagB;

        [Header("Start Cells")]
        public int startCellA = 0;
        public int startCellB = 12;
        public int moveDirA = 1;
        public int moveDirB = -1;

        [Header("Collision Rules")]
        public bool allowHitSingleStone = false;
        public bool blockIfOpponentAnyStone = true;

        [Header("Match Settings")]
        public int maxTurns = 120;
        public int randomSeed = 12345;
        public bool verboseLog = true;

        [Header("Head Rules")]
        public HeadRuleConfig headRules = new HeadRuleConfig();
    }
}
