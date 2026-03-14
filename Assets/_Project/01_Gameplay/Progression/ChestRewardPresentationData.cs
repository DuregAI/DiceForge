using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Progression
{
    public readonly struct ChestRewardPresentationEntry
    {
        public ChestRewardPresentationEntry(string chestId, string displayName, Sprite icon, string rarityLabel, int count)
        {
            ChestId = string.IsNullOrWhiteSpace(chestId) ? string.Empty : chestId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ChestId : displayName.Trim();
            Icon = icon;
            RarityLabel = string.IsNullOrWhiteSpace(rarityLabel) ? string.Empty : rarityLabel.Trim();
            Count = count < 1 ? 1 : count;
        }

        public string ChestId { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public string RarityLabel { get; }
        public int Count { get; }
        public bool HasRarityLabel => !string.IsNullOrWhiteSpace(RarityLabel);
    }

    public sealed class ChestRewardPresentationData
    {
        private ChestRewardPresentationData(
            IReadOnlyList<ChestRewardPresentationEntry> entries,
            string flavorText,
            string effectPresetId,
            string sourceContext,
            int playerLevel,
            bool afterLevelUp)
        {
            Entries = entries ?? new List<ChestRewardPresentationEntry>(0);
            FlavorText = string.IsNullOrWhiteSpace(flavorText) ? string.Empty : flavorText.Trim();
            EffectPresetId = string.IsNullOrWhiteSpace(effectPresetId) ? "level_up_arcane" : effectPresetId.Trim();
            SourceContext = string.IsNullOrWhiteSpace(sourceContext) ? string.Empty : sourceContext.Trim();
            PlayerLevel = playerLevel;
            AfterLevelUp = afterLevelUp;
        }

        public IReadOnlyList<ChestRewardPresentationEntry> Entries { get; }
        public string FlavorText { get; }
        public string EffectPresetId { get; }
        public string SourceContext { get; }
        public int PlayerLevel { get; }
        public bool AfterLevelUp { get; }
        public bool HasEntries => Entries != null && Entries.Count > 0;
        public bool HasAdditionalEntries => Entries != null && Entries.Count > 1;
        public int TotalChestCount
        {
            get
            {
                if (Entries == null || Entries.Count == 0)
                    return 0;

                int totalCount = 0;
                for (int i = 0; i < Entries.Count; i++)
                    totalCount += Entries[i].Count;

                return totalCount;
            }
        }

        public ChestRewardPresentationEntry PrimaryEntry => HasEntries ? Entries[0] : default;

        public static ChestRewardPresentationData CreateFromGrantedChests(
            ChestCatalog chestCatalog,
            IReadOnlyList<ChestInstance> grantedChests,
            string sourceContext,
            int playerLevel,
            bool afterLevelUp,
            string effectPresetId = "level_up_arcane")
        {
            if (grantedChests == null || grantedChests.Count == 0)
                return null;

            var countsByType = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var orderedTypeIds = new List<string>(grantedChests.Count);
            for (int i = 0; i < grantedChests.Count; i++)
            {
                ChestInstance chest = grantedChests[i];
                if (chest == null || string.IsNullOrWhiteSpace(chest.chestTypeId))
                    continue;

                string chestTypeId = chest.chestTypeId.Trim();
                if (!countsByType.ContainsKey(chestTypeId))
                {
                    countsByType[chestTypeId] = 0;
                    orderedTypeIds.Add(chestTypeId);
                }

                countsByType[chestTypeId]++;
            }

            if (orderedTypeIds.Count == 0)
                return null;

            var entries = new List<ChestRewardPresentationEntry>(orderedTypeIds.Count);
            for (int i = 0; i < orderedTypeIds.Count; i++)
            {
                string chestTypeId = orderedTypeIds[i];
                ChestDefinition definition = FindChestDefinition(chestCatalog, chestTypeId);
                string displayName = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName
                    : chestTypeId;

                entries.Add(new ChestRewardPresentationEntry(
                    chestTypeId,
                    displayName,
                    definition != null ? definition.icon : null,
                    string.Empty,
                    countsByType[chestTypeId]));
            }

            int totalCount = 0;
            for (int i = 0; i < entries.Count; i++)
                totalCount += entries[i].Count;

            string flavorText = totalCount == 1
                ? "A battle-earned chest has been added to your stash."
                : $"{totalCount} battle-earned chests have been added to your stash.";

            return new ChestRewardPresentationData(entries, flavorText, effectPresetId, sourceContext, playerLevel, afterLevelUp);
        }

        private static ChestDefinition FindChestDefinition(ChestCatalog chestCatalog, string chestTypeId)
        {
            if (chestCatalog == null || chestCatalog.chests == null || string.IsNullOrWhiteSpace(chestTypeId))
                return null;

            for (int i = 0; i < chestCatalog.chests.Count; i++)
            {
                ChestDefinition definition = chestCatalog.chests[i];
                if (definition != null && string.Equals(definition.id, chestTypeId, System.StringComparison.Ordinal))
                    return definition;
            }

            return null;
        }
    }
}
