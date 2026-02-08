using Diceforge.Core;
using UnityEngine;

namespace Diceforge.Progression
{
    public static class RewardService
    {
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

            if (won)
            {
                var chest = ChestService.CreateChestInstance(ProgressionIds.BasicChest);
                if (chest != null)
                    bundle.chests.Add(chest);
            }

            return bundle;
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
