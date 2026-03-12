using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Progression
{
    public static class AvatarService
    {
        private const string DatabasePath = "Progression/ProgressionDatabase";

        private static ProgressionDatabase _database;
        private static List<ItemDefinition> _avatarDefinitions;
        private static string _defaultAvatarId;
        private static bool _missingCatalogLogged;

        public static string GetDefaultAvatarId()
        {
            EnsureAvatarCatalog();
            return _defaultAvatarId ?? string.Empty;
        }

        public static IReadOnlyList<ItemDefinition> GetAvatarDefinitions()
        {
            EnsureAvatarCatalog();
            return _avatarDefinitions;
        }

        public static ItemDefinition GetAvatarDefinition(string avatarId)
        {
            if (string.IsNullOrWhiteSpace(avatarId))
                return null;

            EnsureAvatarCatalog();

            for (int i = 0; i < _avatarDefinitions.Count; i++)
            {
                ItemDefinition definition = _avatarDefinitions[i];
                if (definition != null && definition.id == avatarId)
                    return definition;
            }

            return null;
        }

        public static ItemDefinition GetSelectedAvatarDefinition()
        {
            ItemDefinition selected = GetAvatarDefinition(ProfileService.GetSelectedAvatarId());
            if (selected != null)
                return selected;

            return GetAvatarDefinition(GetDefaultAvatarId());
        }

        public static Sprite GetSelectedAvatarSprite()
        {
            ItemDefinition definition = GetSelectedAvatarDefinition();
            return definition != null ? definition.icon : null;
        }

        public static bool IsAvatarUnlocked(string avatarId)
        {
            return IsAvatarUnlocked(GetAvatarDefinition(avatarId));
        }

        public static bool IsAvatarUnlocked(ItemDefinition definition)
        {
            if (definition == null)
                return false;

            if (definition.cosmeticSourceType == CosmeticSourceType.Default)
                return true;

            return ProfileService.GetItemCount(definition.id) > 0;
        }

        public static bool TrySelectAvatar(string avatarId)
        {
            return ProfileService.SetSelectedAvatarId(avatarId);
        }

        private static void EnsureAvatarCatalog()
        {
            if (_avatarDefinitions != null)
                return;

            _avatarDefinitions = new List<ItemDefinition>();
            _defaultAvatarId = string.Empty;

            ProgressionDatabase database = GetDatabase();
            if (database == null || database.itemCatalog == null || database.itemCatalog.items == null)
            {
                if (!_missingCatalogLogged)
                {
                    Debug.LogError("[AvatarService] ProgressionDatabase item catalog is missing.");
                    _missingCatalogLogged = true;
                }

                return;
            }

            for (int i = 0; i < database.itemCatalog.items.Count; i++)
            {
                ItemDefinition definition = database.itemCatalog.items[i];
                if (definition == null)
                    continue;

                if (definition.itemType != ItemType.Cosmetic || definition.cosmeticCategory != CosmeticCategory.Avatar)
                    continue;

                _avatarDefinitions.Add(definition);
            }

            _avatarDefinitions.Sort(CompareAvatarDefinitions);

            for (int i = 0; i < _avatarDefinitions.Count; i++)
            {
                ItemDefinition definition = _avatarDefinitions[i];
                if (definition != null && definition.cosmeticSourceType == CosmeticSourceType.Default)
                {
                    _defaultAvatarId = definition.id;
                    break;
                }
            }

            if (string.IsNullOrEmpty(_defaultAvatarId))
                Debug.LogError("[AvatarService] No default avatar is configured in the item catalog.");
        }

        private static int CompareAvatarDefinitions(ItemDefinition left, ItemDefinition right)
        {
            int leftSource = left != null && left.cosmeticSourceType == CosmeticSourceType.Default ? 0 : 1;
            int rightSource = right != null && right.cosmeticSourceType == CosmeticSourceType.Default ? 0 : 1;
            int sourceComparison = leftSource.CompareTo(rightSource);
            if (sourceComparison != 0)
                return sourceComparison;

            int sortComparison = (left != null ? left.sortOrder : 0).CompareTo(right != null ? right.sortOrder : 0);
            if (sortComparison != 0)
                return sortComparison;

            string leftName = left != null ? left.displayName : string.Empty;
            string rightName = right != null ? right.displayName : string.Empty;
            return string.CompareOrdinal(leftName, rightName);
        }

        private static ProgressionDatabase GetDatabase()
        {
            _database ??= Resources.Load<ProgressionDatabase>(DatabasePath);
            return _database;
        }
    }
}
