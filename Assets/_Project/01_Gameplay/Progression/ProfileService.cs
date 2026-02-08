using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Diceforge.Progression
{
    public static class ProfileService
    {
        private const string FileName = "player_profile.json";
        private static readonly Dictionary<string, int> CurrencyMap = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> InventoryMap = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> UpgradeMap = new(StringComparer.Ordinal);

        private static PlayerProfile _profile;

        public static event Action ProfileChanged;

        public static PlayerProfile Current => _profile ??= CreateDefault();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            Load();
        }

        public static void Load()
        {
            var path = GetPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _profile = JsonUtility.FromJson<PlayerProfile>(json) ?? CreateDefault();
            }
            else
            {
                _profile = CreateDefault();
                Save();
            }

            RebuildCache();
            NotifyChanged();
        }

        public static void Save()
        {
            if (_profile == null)
            {
                _profile = CreateDefault();
            }

            _profile.currencies = ToList(CurrencyMap);
            _profile.inventory = ToList(InventoryMap);
            _profile.upgrades = new Dictionary<string, int>(UpgradeMap, StringComparer.Ordinal);
            _profile.upgradeLevels = ToList(UpgradeMap);

            var json = JsonUtility.ToJson(_profile, true);
            File.WriteAllText(GetPath(), json);
        }

        public static int GetCurrency(string id) => GetAmount(CurrencyMap, id);

        public static void AddCurrency(string id, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(id))
                return;

            AddAmount(CurrencyMap, id, amount);
            SaveAndNotify();
        }

        public static bool SpendCurrency(string id, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(id))
                return false;

            var current = GetAmount(CurrencyMap, id);
            if (current < amount)
                return false;

            CurrencyMap[id] = current - amount;
            SaveAndNotify();
            return true;
        }

        public static int GetItemCount(string id) => GetAmount(InventoryMap, id);

        public static void AddItem(string id, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(id))
                return;

            AddAmount(InventoryMap, id, amount);
            SaveAndNotify();
        }

        public static bool RemoveItem(string id, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(id))
                return false;

            var current = GetAmount(InventoryMap, id);
            if (current < amount)
                return false;

            InventoryMap[id] = current - amount;
            SaveAndNotify();
            return true;
        }

        public static void AddXp(int amount)
        {
            if (amount <= 0)
                return;

            Current.hero.xp += amount;
            SaveAndNotify();
        }

        public static void ApplyReward(RewardBundle bundle)
        {
            if (bundle == null || bundle.IsEmpty)
                return;

            if (bundle.currencies != null)
            {
                foreach (var currency in bundle.currencies)
                {
                    if (currency == null)
                        continue;
                    AddAmount(CurrencyMap, currency.id, currency.amount);
                }
            }

            if (bundle.items != null)
            {
                foreach (var item in bundle.items)
                {
                    if (item == null)
                        continue;
                    AddAmount(InventoryMap, item.id, item.amount);
                }
            }

            if (bundle.xp > 0)
                Current.hero.xp += bundle.xp;

            if (bundle.chests != null)
            {
                foreach (var chest in bundle.chests)
                {
                    if (chest == null)
                        continue;
                    Current.chestQueue.Add(chest);
                }
            }

            SaveAndNotify();
        }

        public static void AddChest(ChestInstance chest)
        {
            if (chest == null)
                return;

            Current.chestQueue.Add(chest);
            SaveAndNotify();
        }

        public static bool RemoveChest(string chestInstanceId)
        {
            if (string.IsNullOrEmpty(chestInstanceId))
                return false;

            var index = Current.chestQueue.FindIndex(x => x.instanceId == chestInstanceId);
            if (index < 0)
                return false;

            Current.chestQueue.RemoveAt(index);
            SaveAndNotify();
            return true;
        }


        public static int GetUpgradeLevel(string id) => GetAmount(UpgradeMap, id);

        internal static void SetUpgradeLevel(string id, int level)
        {
            if (string.IsNullOrEmpty(id))
                return;

            UpgradeMap[id] = Mathf.Max(0, level);
            SaveAndNotify();
        }

        public static int IncrementUpgradeLevel(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 0;

            var nextLevel = GetUpgradeLevel(id) + 1;
            UpgradeMap[id] = nextLevel;
            SaveAndNotify();
            return nextLevel;
        }

        public static void ResetProfile()
        {
            _profile = CreateDefault();
            RebuildCache();
            SaveAndNotify();
        }

        public static void AddTestCurrency()
        {
            AddCurrency(ProgressionIds.SoftGold, 100);
            AddCurrency(ProgressionIds.Essence, 20);
            AddCurrency(ProgressionIds.Shards, 5);
        }

        private static void SaveAndNotify()
        {
            Save();
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            ProfileChanged?.Invoke();
        }

        private static void RebuildCache()
        {
            CurrencyMap.Clear();
            InventoryMap.Clear();
            UpgradeMap.Clear();

            if (_profile == null)
                _profile = CreateDefault();

            if (_profile.hero == null)
                _profile.hero = new PlayerProfile.HeroProgress();

            if (_profile.currencies != null)
            {
                foreach (var currency in _profile.currencies)
                {
                    if (currency == null || string.IsNullOrEmpty(currency.id))
                        continue;

                    CurrencyMap[currency.id] = Mathf.Max(0, currency.amount);
                }
            }

            if (_profile.inventory != null)
            {
                foreach (var item in _profile.inventory)
                {
                    if (item == null || string.IsNullOrEmpty(item.id))
                        continue;

                    InventoryMap[item.id] = Mathf.Max(0, item.amount);
                }
            }

            if (_profile.upgrades != null)
            {
                foreach (var pair in _profile.upgrades)
                {
                    if (string.IsNullOrEmpty(pair.Key))
                        continue;

                    UpgradeMap[pair.Key] = Mathf.Max(0, pair.Value);
                }
            }

            if (_profile.upgradeLevels != null)
            {
                foreach (var level in _profile.upgradeLevels)
                {
                    if (level == null || string.IsNullOrEmpty(level.id))
                        continue;

                    UpgradeMap[level.id] = Mathf.Max(0, level.amount);
                }
            }

            _profile.currencies = ToList(CurrencyMap);
            _profile.inventory = ToList(InventoryMap);
            _profile.upgrades = new Dictionary<string, int>(UpgradeMap, StringComparer.Ordinal);
            _profile.upgradeLevels = ToList(UpgradeMap);
            _profile.chestQueue ??= new List<ChestInstance>();
        }

        private static PlayerProfile CreateDefault()
        {
            var profile = new PlayerProfile();
            profile.currencies.Add(new ProfileAmount(ProgressionIds.SoftGold, 0));
            profile.currencies.Add(new ProfileAmount(ProgressionIds.Essence, 0));
            profile.currencies.Add(new ProfileAmount(ProgressionIds.Shards, 0));
            return profile;
        }

        private static int GetAmount(Dictionary<string, int> map, string id)
        {
            if (string.IsNullOrEmpty(id))
                return 0;

            return map.TryGetValue(id, out var amount) ? amount : 0;
        }

        private static void AddAmount(Dictionary<string, int> map, string id, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(id))
                return;

            var current = GetAmount(map, id);
            map[id] = current + amount;
        }

        private static List<ProfileAmount> ToList(Dictionary<string, int> map)
        {
            var list = new List<ProfileAmount>(map.Count);
            foreach (var pair in map)
            {
                list.Add(new ProfileAmount(pair.Key, pair.Value));
            }

            return list;
        }

        private static string GetPath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }
    }
}
