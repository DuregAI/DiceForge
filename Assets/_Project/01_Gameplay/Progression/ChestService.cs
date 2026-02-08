using System;
using UnityEngine;

namespace Diceforge.Progression
{
    public static class ChestService
    {
        private const string DatabasePath = "Progression/ProgressionDatabase";
        private static ProgressionDatabase _database;

        public static ChestInstance CreateChestInstance(string chestTypeId)
        {
            if (string.IsNullOrEmpty(chestTypeId))
                return null;

            return new ChestInstance(Guid.NewGuid().ToString("N"), chestTypeId);
        }

        public static RewardBundle OpenChest(string chestInstanceId)
        {
            if (string.IsNullOrEmpty(chestInstanceId))
                return null;

            var queue = ProfileService.Current.chestQueue;
            var chest = queue.Find(x => x.instanceId == chestInstanceId);
            if (chest == null)
                return null;

            var chestDefinition = FindChestDefinition(chest.chestTypeId);
            var bundle = BuildRewards(chestDefinition);

            if (!ProfileService.RemoveChest(chestInstanceId))
                return null;

            ProfileService.ApplyReward(bundle);
            return bundle;
        }

        private static RewardBundle BuildRewards(ChestDefinition chestDefinition)
        {
            var bundle = new RewardBundle();
            if (chestDefinition == null || chestDefinition.fixedRewards == null)
                return bundle;

            foreach (var reward in chestDefinition.fixedRewards)
            {
                if (reward == null || reward.amount <= 0)
                    continue;

                if (reward.isItem)
                {
                    bundle.items.Add(new ProfileAmount(reward.id, reward.amount));
                }
                else
                {
                    bundle.currencies.Add(new ProfileAmount(reward.id, reward.amount));
                }
            }

            return bundle;
        }

        private static ChestDefinition FindChestDefinition(string chestTypeId)
        {
            if (string.IsNullOrEmpty(chestTypeId))
                return null;

            _database ??= Resources.Load<ProgressionDatabase>(DatabasePath);
            if (_database == null || _database.chestCatalog == null || _database.chestCatalog.chests == null)
                return null;

            return _database.chestCatalog.chests.Find(c => c != null && c.id == chestTypeId);
        }
    }
}
