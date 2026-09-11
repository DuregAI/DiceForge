using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Diceforge.Progression
{
    public static class ProfileService
    {
        private const string FileName = "player_profile.json";
        private const string DefaultDisplayName = "Player";
        private const string ProfileVersion = "0.0.6";
        private static readonly Dictionary<string, int> CurrencyMap = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> InventoryMap = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> UpgradeMap = new(StringComparer.Ordinal);

        private static PlayerProfile _profile;
        private static AtomicProfileStore _store;
        private static string _lastSavedJson;
        public static string LoadError { get; private set; }
        private static AtomicProfileStore Store => _store ??= new AtomicProfileStore(GetPath());

        public static event Action ProfileChanged;
        public static event Action<string> OnPlayerNameChanged;

        public static PlayerProfile Current
        {
            get
            {
                if (!string.IsNullOrEmpty(LoadError)) throw new InvalidOperationException(LoadError);
                if (_profile == null) Load();
                return _profile;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RuntimeInit()
        {
            try { Load(); }
            catch (Exception exception)
            {
                Debug.LogError("[ProfileService] " + exception.Message);
                ProfileLoadErrorView.Show();
            }
        }

        public static void Load()
        {
            try
            {
                var loaded = Store.Load();
                LoadError = null;
                _profile = loaded ?? CreateDefault();
                RebuildCache();
                _lastSavedJson = JsonUtility.ToJson(_profile);
                var guidGenerated = EnsurePlayerGuid();
                var avatarNormalized = EnsureSelectedAvatarId();
                if (loaded == null || guidGenerated || avatarNormalized) Save();
            }
            catch (Exception exception)
            {
                LoadError = "Не удалось загрузить профиль. " + exception.Message;
                throw;
            }
            NotifyChanged();
            NotifyPlayerNameChanged();
        }

        public static void Save()
        {
            var candidate = Snapshot();
            if (!TryCommit(candidate, out string error, false))
            {
                if (_lastSavedJson != null)
                {
                    _profile = JsonUtility.FromJson<PlayerProfile>(_lastSavedJson);
                    RebuildCache();
                }
                throw new IOException(error);
            }
        }

        internal static PlayerProfile Snapshot()
        {
            var candidate = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(Current));
            candidate.version = ProfileVersion;
            candidate.currencies = ToList(CurrencyMap);
            candidate.inventory = ToList(InventoryMap);
            candidate.upgradeLevels = ToList(UpgradeMap);
            return candidate;
        }

        internal static bool TryCommit(PlayerProfile candidate, out string error, bool notify = true)
        {
            error = null;
            if (!string.IsNullOrEmpty(LoadError)) { error = LoadError; return false; }
            candidate.version = ProfileVersion;
            try { Store.Save(candidate); }
            catch (Exception exception)
            {
                // An exception can reach the caller after the replacement succeeded.
                try
                {
                    var saved = Store.Load();
                    if (saved == null || JsonUtility.ToJson(saved) != JsonUtility.ToJson(candidate))
                    { error = exception.Message; return false; }
                }
                catch { error = exception.Message; return false; }
            }
            _profile = candidate;
            RebuildCache();
            _lastSavedJson = JsonUtility.ToJson(_profile);
            if (notify) NotifyChanged();
            return true;
        }

        public static string GetDisplayName()
        {
            var playerName = Current.playerName;
            return string.IsNullOrWhiteSpace(playerName) ? DefaultDisplayName : playerName.Trim();
        }

        public static void SetPlayerName(string playerName)
        {
            var normalizedName = string.IsNullOrWhiteSpace(playerName) ? string.Empty : playerName.Trim();
            if (string.Equals(Current.playerName, normalizedName, StringComparison.Ordinal))
            {
                return;
            }

            Current.playerName = normalizedName;
            Save();
            NotifyChanged();
            NotifyPlayerNameChanged();
        }

        public static string GetSelectedAvatarId()
        {
            return string.IsNullOrWhiteSpace(Current.selectedAvatarId)
                ? string.Empty
                : Current.selectedAvatarId.Trim();
        }

        public static bool SetSelectedAvatarId(string avatarId)
        {
            string normalizedAvatarId = string.IsNullOrWhiteSpace(avatarId) ? string.Empty : avatarId.Trim();
            if (string.IsNullOrEmpty(normalizedAvatarId))
            {
                Debug.LogError("[ProfileService] Cannot assign an empty avatar id.");
                return false;
            }

            ItemDefinition definition = AvatarService.GetAvatarDefinition(normalizedAvatarId);
            if (definition == null)
            {
                Debug.LogError($"[ProfileService] Cannot assign avatar '{normalizedAvatarId}' because it is not configured.");
                return false;
            }

            if (!AvatarService.IsAvatarUnlocked(definition))
            {
                Debug.LogWarning($"[ProfileService] Cannot assign locked avatar '{normalizedAvatarId}'.");
                return false;
            }

            if (string.Equals(Current.selectedAvatarId, normalizedAvatarId, StringComparison.Ordinal))
            {
                return true;
            }

            Current.selectedAvatarId = normalizedAvatarId;
            SaveAndNotify();
            return true;
        }

        public static bool IsTutorialCompleted()
        {
            return Current.tutorialCompleted;
        }

        public static void SetTutorialCompleted(bool value)
        {
            if (Current.tutorialCompleted == value)
            {
                return;
            }

            Current.tutorialCompleted = value;
            SaveAndNotify();
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

        public static LevelUpPresentationData AddXp(int amount, string sourceContext = LevelUpSourceContexts.Progression)
        {
            if (amount <= 0)
                return null;

            int previousLevel = UiProgressionService.GetPlayerLevel();
            Current.hero.xp += amount;
            SaveAndNotify();
            return LevelUpProgressionService.Build(previousLevel, UiProgressionService.GetPlayerLevel(), sourceContext);
        }

        public static LevelUpPresentationData ApplyReward(RewardBundle bundle, string sourceContext = LevelUpSourceContexts.Progression)
        {
            return ApplyRewardDetailed(bundle, sourceContext).LevelUpData;
        }

        public static RewardApplicationResult ApplyRewardDetailed(RewardBundle bundle, string sourceContext = LevelUpSourceContexts.Progression)
        {
            int previousLevel = UiProgressionService.GetPlayerLevel();
            RewardBundle resolvedBundle = bundle ?? new RewardBundle();
            if (resolvedBundle.IsEmpty)
            {
                return new RewardApplicationResult(
                    resolvedBundle,
                    previousLevel,
                    previousLevel,
                    null,
                    sourceContext);
            }

            var candidate = Snapshot();
            ProgressionTransactionService.ApplyReward(candidate, resolvedBundle);
            if (!TryCommit(candidate, out string error)) throw new IOException(error);
            int newLevel = UiProgressionService.GetPlayerLevel();
            LevelUpPresentationData levelUpData = LevelUpProgressionService.Build(previousLevel, newLevel, sourceContext);
            return new RewardApplicationResult(
                resolvedBundle,
                previousLevel,
                newLevel,
                levelUpData,
                sourceContext);
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
            var old = Snapshot();
            _profile = CreateDefault();
            _profile.chapters = old.chapters;
            _profile.progressionReceipts = old.progressionReceipts;
            RebuildCache();
            EnsureSelectedAvatarId();
            SaveAndNotify();
            NotifyPlayerNameChanged();
        }

        public static void AddTestCurrency()
        {
            AddCurrency(ProgressionIds.SoftGold, 100);
            AddCurrency(ProgressionIds.Essence, 20);
            AddCurrency(ProgressionIds.Shards, 5);
        }

        private static bool EnsurePlayerGuid()
        {
            if (!string.IsNullOrWhiteSpace(Current.playerGuid))
            {
                Current.playerGuid = Current.playerGuid.Trim();
                return false;
            }

            Current.playerGuid = Guid.NewGuid().ToString();
            return true;
        }

        private static bool EnsureSelectedAvatarId()
        {
            string currentAvatarId = string.IsNullOrWhiteSpace(Current.selectedAvatarId)
                ? string.Empty
                : Current.selectedAvatarId.Trim();
            string defaultAvatarId = AvatarService.GetDefaultAvatarId();

            if (string.IsNullOrEmpty(defaultAvatarId))
            {
                Debug.LogError("[ProfileService] No default avatar is configured in the avatar catalog.");
                Current.selectedAvatarId = string.Empty;
                return false;
            }

            if (string.IsNullOrEmpty(currentAvatarId))
            {
                Current.selectedAvatarId = defaultAvatarId;
                return true;
            }

            ItemDefinition definition = AvatarService.GetAvatarDefinition(currentAvatarId);
            if (definition == null)
            {
                Debug.LogError($"[ProfileService] Selected avatar '{currentAvatarId}' is missing from the avatar catalog. Falling back to '{defaultAvatarId}'.");
                Current.selectedAvatarId = defaultAvatarId;
                return true;
            }

            if (!AvatarService.IsAvatarUnlocked(definition))
            {
                Debug.LogWarning($"[ProfileService] Selected avatar '{currentAvatarId}' is locked. Falling back to '{defaultAvatarId}'.");
                Current.selectedAvatarId = defaultAvatarId;
                return true;
            }

            Current.selectedAvatarId = currentAvatarId;
            return false;
        }

        private static void SaveAndNotify()
        {
            Save();
            NotifyChanged();
        }

        private static void NotifyChanged()
        {
            if (ProfileChanged == null) return;
            foreach (Action callback in ProfileChanged.GetInvocationList())
            {
                try { callback(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private static void NotifyPlayerNameChanged()
        {
            OnPlayerNameChanged?.Invoke(GetDisplayName());
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

            _profile.version = ProfileVersion;
            _profile.playerGuid = string.IsNullOrWhiteSpace(_profile.playerGuid) ? string.Empty : _profile.playerGuid.Trim();
            _profile.playerName = string.IsNullOrWhiteSpace(_profile.playerName) ? string.Empty : _profile.playerName.Trim();
            _profile.selectedAvatarId = string.IsNullOrWhiteSpace(_profile.selectedAvatarId) ? string.Empty : _profile.selectedAvatarId.Trim();
            _profile.currencies = ToList(CurrencyMap);
            _profile.inventory = ToList(InventoryMap);
            _profile.upgrades = new Dictionary<string, int>(UpgradeMap, StringComparer.Ordinal);
            _profile.upgradeLevels = ToList(UpgradeMap);
            _profile.chestQueue ??= new List<ChestInstance>();
            _profile.chapters ??= new List<ChapterProgress>();
            _profile.progressionReceipts ??= new List<ProgressionReceipt>();
        }

        private static PlayerProfile CreateDefault()
        {
            var profile = new PlayerProfile();
            profile.version = ProfileVersion;
            profile.playerGuid = Guid.NewGuid().ToString();
            profile.playerName = string.Empty;
            profile.selectedAvatarId = string.Empty;
            profile.tutorialCompleted = false;
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
