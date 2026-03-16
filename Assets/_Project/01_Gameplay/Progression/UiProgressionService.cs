using UnityEngine;

namespace Diceforge.Progression
{
    public static class UiProgressionService
    {
        private const string DatabasePath = "Progression/ProgressionDatabase";
        private const int DefaultXpPerLevel = 100;
        private const int DefaultXpPerLevelIncrement = 0;
        private const int DefaultChestSectionUnlockLevel = 3;
        private const int DefaultUpgradesUnlockLevel = 4;
        private const int DefaultWinXp = 10;
        private const int DefaultLossXp = 4;
        private const int DefaultWinSoftGold = 30;
        private const int DefaultLossSoftGold = 10;
        private const int DefaultWinEssence = 3;
        private const int DefaultLossEssence = 1;
        private const float DefaultBaseChestChanceOnWin = 0.25f;

        private static ProgressionDatabase _database;

        public static int XpPerLevel => GetXpRequiredForLevel(GetPlayerLevel());

        public static int GetPlayerLevel()
        {
            return GetLevelForXp(ProfileService.Current.hero.xp);
        }

        public static int GetLevelForXp(int xp)
        {
            int clampedXp = Mathf.Max(0, xp);
            int level = 1;

            while (clampedXp >= GetLevelFloorXp(level + 1))
                level++;

            return level;
        }

        public static int GetLevelFloorXp(int level)
        {
            int clampedLevel = Mathf.Max(1, level);
            int completedLevels = clampedLevel - 1;
            if (completedLevels <= 0)
                return 0;

            long baseXp = GetBaseXpPerLevel();
            long increment = GetXpPerLevelIncrement();
            long totalXp = (completedLevels * (2L * baseXp + ((completedLevels - 1L) * increment))) / 2L;
            return totalXp > int.MaxValue ? int.MaxValue : (int)totalXp;
        }

        public static int GetXpIntoCurrentLevel(int xp)
        {
            int level = GetLevelForXp(xp);
            return Mathf.Max(0, xp - GetLevelFloorXp(level));
        }

        public static int GetXpPerLevel()
        {
            return GetBaseXpPerLevel();
        }

        public static int GetBaseXpPerLevel()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(1, config != null ? config.xpPerLevel : DefaultXpPerLevel);
        }

        public static int GetXpPerLevelIncrement()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(0, config != null ? config.xpPerLevelIncrement : DefaultXpPerLevelIncrement);
        }

        public static int GetXpRequiredForLevel(int level)
        {
            int clampedLevel = Mathf.Max(1, level);
            int xpRequired = GetBaseXpPerLevel() + ((clampedLevel - 1) * GetXpPerLevelIncrement());
            return Mathf.Max(1, xpRequired);
        }

        public static int GetChestSectionUnlockLevel()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(1, config != null ? config.chestSectionUnlockLevel : DefaultChestSectionUnlockLevel);
        }

        public static int GetUpgradesUnlockLevel()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(1, config != null ? config.upgradesUnlockLevel : DefaultUpgradesUnlockLevel);
        }

        public static int GetMatchWinXp()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(0, config != null ? config.winXp : DefaultWinXp);
        }

        public static int GetMatchLossXp()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(0, config != null ? config.lossXp : DefaultLossXp);
        }

        public static int GetMatchWinSoftGold()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(0, config != null ? config.winSoftGold : DefaultWinSoftGold);
        }

        public static int GetMatchLossSoftGold()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(0, config != null ? config.lossSoftGold : DefaultLossSoftGold);
        }

        public static int GetMatchWinEssence()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(0, config != null ? config.winEssence : DefaultWinEssence);
        }

        public static int GetMatchLossEssence()
        {
            UiProgressionConfig config = GetConfig();
            return Mathf.Max(0, config != null ? config.lossEssence : DefaultLossEssence);
        }

        public static float GetBaseChestChanceOnWin()
        {
            UiProgressionConfig config = GetConfig();
            float configuredValue = config != null ? config.baseChestChanceOnWin : DefaultBaseChestChanceOnWin;
            return Mathf.Clamp(configuredValue, 0f, 0.9f);
        }

        public static bool IsChestSectionUnlocked()
        {
            return GetPlayerLevel() >= GetChestSectionUnlockLevel();
        }

        public static bool IsUpgradesUnlocked()
        {
            return GetPlayerLevel() >= GetUpgradesUnlockLevel();
        }

        private static UiProgressionConfig GetConfig()
        {
            _database ??= Resources.Load<ProgressionDatabase>(DatabasePath);
            return _database != null ? _database.uiProgressionConfig : null;
        }
    }
}
