using Diceforge.Core;
using UnityEngine;

namespace Diceforge.Progression
{
    public static class RewardService
    {
        private const float BaseChestChanceOnWin = 0.25f;

        private static readonly RewardBundle WinRewards = new()
        {
            xp = 10,
            currencies =
            {
                new ProfileAmount(ProgressionIds.SoftGold, 30),
                new ProfileAmount(ProgressionIds.Essence, 3)
            }
        };

        private static readonly RewardBundle LossRewards = new()
        {
            xp = 4,
            currencies =
            {
                new ProfileAmount(ProgressionIds.SoftGold, 10),
                new ProfileAmount(ProgressionIds.Essence, 1)
            }
        };

        public static RewardBundle CalculateMatchRewards(MatchResult matchResult, string mode)
        {
            bool won = matchResult.Winner == PlayerId.A;
            var bundle = CloneReward(won ? WinRewards : LossRewards);

            ApplyUpgradeBonuses(bundle, won, mode);
            TryAddWinChest(bundle, won);

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

            var chance = Mathf.Clamp(BaseChestChanceOnWin + GetUpgradeValue(ProgressionIds.UpgChestChance), 0f, 0.9f);
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

        private static RewardBundle CloneReward(RewardBundle source)
        {
            var clone = new RewardBundle
            {
                xp = source.xp
            };

            if (source.currencies != null)
            {
                foreach (var currency in source.currencies)
                {
                    clone.currencies.Add(new ProfileAmount(currency.id, currency.amount));
                }
            }

            if (source.items != null)
            {
                foreach (var item in source.items)
                {
                    clone.items.Add(new ProfileAmount(item.id, item.amount));
                }
            }

            return clone;
        }
    }
}
