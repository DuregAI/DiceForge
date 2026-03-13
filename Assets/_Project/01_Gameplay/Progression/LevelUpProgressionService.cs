using System.Collections.Generic;

namespace Diceforge.Progression
{
    public static class LevelUpProgressionService
    {
        public const string DefaultEffectPresetId = "level_up_arcane";
        private const string ChestShopUnlockId = "chest_shop";
        private const string UpgradesUnlockId = "upgrades";

        public static LevelUpPresentationData Build(int previousLevel, int newLevel, string sourceContext, string flavorText = null, string effectPresetId = DefaultEffectPresetId)
        {
            if (newLevel <= previousLevel)
                return null;

            var unlocks = BuildUnlockList(previousLevel, newLevel);
            string resolvedFlavorText = string.IsNullOrWhiteSpace(flavorText)
                ? BuildFlavorText(newLevel, unlocks.Count)
                : flavorText;

            return new LevelUpPresentationData(previousLevel, newLevel, unlocks, resolvedFlavorText, effectPresetId, sourceContext);
        }

        public static LevelUpPresentationData CreateDebugSample(int previousLevel, int newLevel, IReadOnlyList<LevelUpUnlockInfo> unlocks, string flavorText = null, string effectPresetId = DefaultEffectPresetId)
        {
            return new LevelUpPresentationData(
                previousLevel,
                newLevel,
                unlocks ?? new List<LevelUpUnlockInfo>(0),
                string.IsNullOrWhiteSpace(flavorText) ? BuildFlavorText(newLevel, unlocks != null ? unlocks.Count : 0) : flavorText,
                effectPresetId,
                LevelUpSourceContexts.Debug);
        }

        private static List<LevelUpUnlockInfo> BuildUnlockList(int previousLevel, int newLevel)
        {
            var unlocks = new List<LevelUpUnlockInfo>(2);
            int chestUnlockLevel = UiProgressionService.GetChestSectionUnlockLevel();
            int upgradesUnlockLevel = UiProgressionService.GetUpgradesUnlockLevel();

            if (previousLevel < chestUnlockLevel && newLevel >= chestUnlockLevel)
                unlocks.Add(new LevelUpUnlockInfo(ChestShopUnlockId, "Chest Shop"));

            if (previousLevel < upgradesUnlockLevel && newLevel >= upgradesUnlockLevel)
                unlocks.Add(new LevelUpUnlockInfo(UpgradesUnlockId, "Upgrades"));

            return unlocks;
        }

        private static string BuildFlavorText(int newLevel, int unlockCount)
        {
            if (unlockCount > 0)
                return "New paths have opened in your forge.";

            if (newLevel % 5 == 0)
                return "A milestone worth savoring.";

            return "Your forge burns brighter.";
        }
    }
}
