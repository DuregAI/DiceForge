using UnityEngine;

namespace Diceforge.Progression
{
    public static class UiProgressionService
    {
        private const string DatabasePath = "Progression/ProgressionDatabase";
        public const int XpPerLevel = 100;
        private const int DefaultChestSectionUnlockLevel = 3;
        private const int DefaultUpgradesUnlockLevel = 4;

        private static ProgressionDatabase _database;

        public static int GetPlayerLevel()
        {
            int xp = Mathf.Max(0, ProfileService.Current.hero.xp);
            return (xp / XpPerLevel) + 1;
        }

        public static int GetChestSectionUnlockLevel()
        {
            var config = GetConfig();
            return Mathf.Max(1, config != null ? config.chestSectionUnlockLevel : DefaultChestSectionUnlockLevel);
        }

        public static int GetUpgradesUnlockLevel()
        {
            var config = GetConfig();
            return Mathf.Max(1, config != null ? config.upgradesUnlockLevel : DefaultUpgradesUnlockLevel);
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
