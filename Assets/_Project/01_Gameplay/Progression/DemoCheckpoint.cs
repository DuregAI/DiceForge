using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Diceforge.Core;
using UnityEngine;

namespace Diceforge.Progression
{
    [Serializable]
    public sealed class DemoCompletionProgress
    {
        public DemoLearningState learning;
        public string[] storySeen;
    }

    [Serializable]
    public sealed class DemoHeroCheckpoint
    {
        public string id;
        public int cell;
        public bool exited;
    }

    [Serializable]
    public sealed class DemoCheckpoint
    {
        public int schemaVersion = 1;
        public string scenarioSignature, levelId, chapterId, runId, nodeId, operationId, selectedHeroId;
        public DemoBattleCheckpoint battle;
        public DemoHeroCheckpoint[] heroes;
        public DemoLearningState learning;
        public bool completionLinesSeen;

        public static string Signature(GameModePreset preset, RulesetConfig rules)
        {
            // Serialize authored primitive data, never transient Unity instance IDs.
            string source = preset.demoLevel.sourceVersion + "|" + preset.modeId + "|" +
                string.Join(",", preset.demoLevel.heroIds) + "|" + JsonUtility.ToJson(rules) + "|" +
                JsonUtility.ToJson(preset.setupPreset) + "|" + JsonUtility.ToJson(preset.diceBagA);
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(source)));
        }

        public bool Matches(GameModePreset preset, RulesetConfig rules, string chapter, string run, string node)
        {
            if (schemaVersion != 1 || levelId != preset.demoLevel.levelId ||
                scenarioSignature != Signature(preset, rules) || string.IsNullOrWhiteSpace(operationId) ||
                (chapterId ?? "") != (chapter ?? "") || (runId ?? "") != (run ?? "") || (nodeId ?? "") != (node ?? "") || battle == null || heroes == null ||
                heroes.Length != preset.demoLevel.heroIds.Length || learning == null || learning.levelId != levelId ||
                string.IsNullOrEmpty(learning.attemptId) || learning.moves < 0 || learning.boardRevision != learning.moves ||
                learning.unaidedMoves < 0 || learning.unaidedMoves > learning.moves ||
                learning.errorCounts?.Any(e => e == null || e.count < 0 || e.count > 2 || (int)e.reason < 1 || (int)e.reason > 8) == true)
                return false;
            var counts = new int[rules.boardSize];
            int exited = 0;
            foreach (string id in preset.demoLevel.heroIds)
            {
                var matches = heroes.Where(h => h != null && h.id == id).ToArray();
                if (matches.Length != 1) return false;
                var hero = matches[0];
                if (hero.exited) { if (hero.cell != -1) return false; exited++; }
                else { if (hero.cell < 0 || hero.cell >= counts.Length) return false; counts[hero.cell]++; }
            }
            return battle.cellsA != null && counts.SequenceEqual(battle.cellsA) && exited == battle.borneOffA &&
                (string.IsNullOrEmpty(selectedHeroId) || heroes.Any(h => h.id == selectedHeroId));
        }
    }
}
