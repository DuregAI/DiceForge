using UnityEngine;

namespace Diceforge.Progression
{
    public enum BuyResult
    {
        Success = 0,
        UnknownUpgrade = 1,
        AlreadyMaxLevel = 2,
        NotEnoughGold = 3
    }

    public static class UpgradeService
    {
        private const string DatabasePath = "Progression/ProgressionDatabase";
        private static ProgressionDatabase _database;

        public static int GetLevel(string upgradeId)
        {
            return ProfileService.GetUpgradeLevel(upgradeId);
        }

        public static bool CanBuy(string upgradeId)
        {
            var definition = GetDefinition(upgradeId);
            if (definition == null)
                return false;

            var level = GetLevel(upgradeId);
            if (level >= definition.maxLevel)
                return false;

            var nextPrice = definition.GetPriceForLevel(level + 1);
            return ProfileService.GetCurrency(ProgressionIds.SoftGold) >= nextPrice;
        }

        public static int GetNextPrice(string upgradeId)
        {
            var definition = GetDefinition(upgradeId);
            if (definition == null)
                return 0;

            var level = GetLevel(upgradeId);
            if (level >= definition.maxLevel)
                return 0;

            return definition.GetPriceForLevel(level + 1);
        }

        public static BuyResult Buy(string upgradeId)
        {
            var definition = GetDefinition(upgradeId);
            if (definition == null)
                return BuyResult.UnknownUpgrade;

            var level = GetLevel(upgradeId);
            if (level >= definition.maxLevel)
                return BuyResult.AlreadyMaxLevel;

            var nextPrice = definition.GetPriceForLevel(level + 1);
            if (!ProfileService.SpendCurrency(ProgressionIds.SoftGold, nextPrice))
                return BuyResult.NotEnoughGold;

            ProfileService.SetUpgradeLevel(upgradeId, level + 1);
            return BuyResult.Success;
        }

        public static UpgradeCatalog GetCatalog()
        {
            return GetDatabase()?.upgradeCatalog;
        }

        private static UpgradeDefinition GetDefinition(string upgradeId)
        {
            return GetCatalog()?.ResolveById(upgradeId);
        }

        private static ProgressionDatabase GetDatabase()
        {
            _database ??= Resources.Load<ProgressionDatabase>(DatabasePath);
            return _database;
        }
    }
}
