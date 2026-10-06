using System;
using System.Collections.Generic;
using Diceforge.Core;
using UnityEngine;

namespace Diceforge.GameModes
{
    [CreateAssetMenu(menuName = "Diceforge/Demo Level", fileName = "DemoLevel")]
    public sealed class DemoLevelDefinition : ScriptableObject
    {
        public string sourceVersion = "0.1";
        public string sourceId;
        public string levelId;
        public string title;
        public string[] heroIds = Array.Empty<string>();
        public GameObject bumPrefab;
        public GameObject hazardPrefab;

        public void Validate(RulesetConfig rules)
        {
            if (rules == null || rules.gameMode != GameMode.SoloTrail || rules.boardSize != 8)
                throw new InvalidOperationException($"Demo level '{levelId}' requires the eight-cell SoloTrail.");
            if (heroIds == null || heroIds.Length < 1 || heroIds.Length > 3 ||
                heroIds.Length != rules.totalStonesPerPlayer)
                throw new InvalidOperationException($"Demo level '{levelId}' hero count does not match its rules.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in heroIds)
                if (!ids.Add(id ?? string.Empty) || (id != "tish" && id != "luma" && id != "bum"))
                    throw new InvalidOperationException($"Demo level '{levelId}' contains an unknown or repeated hero '{id}'.");
            if (rules.startCellA != 0 || rules.moveDirA != 1 || rules.allowHitSingleStone || !rules.blockIfOpponentAnyStone || rules.allowReroll)
                throw new InvalidOperationException($"Demo level '{levelId}' requires forward movement, blocking and no hits or rerolls.");
            int expectedActions = rules.soloTrailStepOfferMode == SoloTrailStepOfferMode.Sequential ? 2 : 1;
            if (rules.actionsPerTurn != expectedActions)
                throw new InvalidOperationException($"Demo level '{levelId}' action count does not match its step offer.");
        }

        public static string HeroName(string id) => id switch
        {
            "tish" => "Tish",
            "luma" => "Luma",
            "bum" => "Bum",
            _ => string.Empty
        };
    }
}
