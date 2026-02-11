using System;
using System.Collections.Generic;

namespace Diceforge.Progression
{
    [Serializable]
    public sealed class PlayerProfile
    {
        public string version = "0.0.4";
        public string playerGuid = string.Empty;
        public string playerName = string.Empty;
        public bool tutorialCompleted;
        public HeroProgress hero = new();
        public List<ProfileAmount> currencies = new();
        public List<ProfileAmount> inventory = new();
        public List<ChestInstance> chestQueue = new();
        public Dictionary<string, int> upgrades = new();
        public List<ProfileAmount> upgradeLevels = new();

        [Serializable]
        public sealed class HeroProgress
        {
            public int xp;
        }
    }

    [Serializable]
    public sealed class ProfileAmount
    {
        public string id;
        public int amount;

        public ProfileAmount() { }

        public ProfileAmount(string id, int amount)
        {
            this.id = id;
            this.amount = amount;
        }
    }

    [Serializable]
    public sealed class ChestInstance
    {
        public string instanceId;
        public string chestTypeId;

        public ChestInstance() { }

        public ChestInstance(string instanceId, string chestTypeId)
        {
            this.instanceId = instanceId;
            this.chestTypeId = chestTypeId;
        }
    }

    [Serializable]
    public sealed class RewardBundle
    {
        public List<ProfileAmount> currencies = new();
        public List<ProfileAmount> items = new();
        public int xp;
        public List<ChestInstance> chests = new();

        public bool IsEmpty => (currencies == null || currencies.Count == 0)
                               && (items == null || items.Count == 0)
                               && xp <= 0
                               && (chests == null || chests.Count == 0);
    }
}
