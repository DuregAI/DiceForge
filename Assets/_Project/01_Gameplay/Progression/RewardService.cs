using Diceforge.Core;
using UnityEngine;

namespace Diceforge.Progression
{
    public static class RewardService
    {
        public static RewardBundle CalculateMatchRewards(MatchResult matchResult, string mode)
        {
            bool won = matchResult.Winner == PlayerId.A;
            RewardBundle bundle = BuildBaseReward(won);

            ApplyUpgradeBonuses(bundle, won, mode);
            TryAddWinChest(bundle, won);

            return bundle;
        }

        private static RewardBundle BuildBaseReward(bool won)
        {
            var bundle = new RewardBundle
            {
                xp = won ? UiProgressionService.GetMatchWinXp() : UiProgressionService.GetMatchLossXp()
            };

            bundle.currencies.Add(new ProfileAmount(
                ProgressionIds.SoftGold,
                won ? UiProgressionService.GetMatchWinSoftGold() : UiProgressionService.GetMatchLossSoftGold()));
            bundle.currencies.Add(new ProfileAmount(
                ProgressionIds.Essence,
                won ? UiProgressionService.GetMatchWinEssence() : UiProgressionService.GetMatchLossEssence()));

            return bundle;
        }

        private static void ApplyUpgradeBonuses(RewardBundle bundle, bool won, string mode)
        {
            if (bundle == null)
                return;

            if (won)
            {
                AddCurrency(bundle, ProgressionIds.SoftGold, Mathf.RoundToInt(GetUpgradeValue(ProgressionIds.UpgWinGold)));
                AddCurrency(bundle, ProgressionIds.Shards, Mathf.RoundToInt(GetUpgradeValue(ProgressionIds.UpgShardsOnWin)));
            }
            else
            {
                AddCurrency(bundle, ProgressionIds.SoftGold, Mathf.RoundToInt(GetUpgradeValue(ProgressionIds.UpgLossGold)));
            }

            AddCurrency(bundle, ProgressionIds.Essence, Mathf.RoundToInt(GetUpgradeValue(ProgressionIds.UpgEssencePerMatch)));

            float goldMultiplier = 1f;
            if (string.Equals(mode, "long", System.StringComparison.OrdinalIgnoreCase))
                goldMultiplier += GetUpgradeValue(ProgressionIds.UpgLongModeBonus);
            else if (string.Equals(mode, "short", System.StringComparison.OrdinalIgnoreCase))
                goldMultiplier += GetUpgradeValue(ProgressionIds.UpgShortModeBonus);

            MultiplyCurrency(bundle, ProgressionIds.SoftGold, goldMultiplier);

            float xpMultiplier = 1f + GetUpgradeValue(ProgressionIds.UpgXpBonus);
            bundle.xp = Mathf.RoundToInt(bundle.xp * xpMultiplier);
        }

        private static void TryAddWinChest(RewardBundle bundle, bool won)
        {
            if (!won || bundle == null)
                return;

            float chance = Mathf.Clamp(UiProgressionService.GetBaseChestChanceOnWin() + GetUpgradeValue(ProgressionIds.UpgChestChance), 0f, 0.9f);
            if (Random.value > chance)
                return;

            var chest = ChestService.CreateChestInstance(ProgressionIds.BasicChest);
            if (chest != null)
                bundle.chests.Add(chest);
        }

        private static float GetUpgradeValue(string upgradeId)
        {
            var catalog = UpgradeService.GetCatalog();
            var definition = catalog != null ? catalog.ResolveById(upgradeId) : null;
            if (definition == null)
                return 0f;

            var level = UpgradeService.GetLevel(upgradeId);
            return definition.GetValueForLevel(level);
        }

        private static void AddCurrency(RewardBundle bundle, string currencyId, int amount)
        {
            if (amount <= 0)
                return;

            var entry = bundle.currencies.Find(x => x != null && x.id == currencyId);
            if (entry == null)
            {
                bundle.currencies.Add(new ProfileAmount(currencyId, amount));
                return;
            }

            entry.amount += amount;
        }

        private static void MultiplyCurrency(RewardBundle bundle, string currencyId, float multiplier)
        {
            if (multiplier <= 0f)
                return;

            var entry = bundle.currencies.Find(x => x != null && x.id == currencyId);
            if (entry == null)
                return;

            entry.amount = Mathf.RoundToInt(entry.amount * multiplier);
        }
    }
}
