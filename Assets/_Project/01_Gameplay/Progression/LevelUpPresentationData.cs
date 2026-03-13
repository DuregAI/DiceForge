using System.Collections.Generic;

namespace Diceforge.Progression
{
    public readonly struct LevelUpUnlockInfo
    {
        public LevelUpUnlockInfo(string id, string displayName)
        {
            Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
        }

        public string Id { get; }
        public string DisplayName { get; }
    }

    public static class LevelUpSourceContexts
    {
        public const string Battle = "battle";
        public const string Debug = "debug";
        public const string Progression = "progression";
    }

    public sealed class LevelUpPresentationData
    {
        public LevelUpPresentationData(int previousLevel, int newLevel, IReadOnlyList<LevelUpUnlockInfo> unlocks, string flavorText, string effectPresetId, string sourceContext)
        {
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
            Unlocks = unlocks ?? new List<LevelUpUnlockInfo>(0);
            FlavorText = string.IsNullOrWhiteSpace(flavorText) ? string.Empty : flavorText.Trim();
            EffectPresetId = string.IsNullOrWhiteSpace(effectPresetId) ? string.Empty : effectPresetId.Trim();
            SourceContext = string.IsNullOrWhiteSpace(sourceContext) ? LevelUpSourceContexts.Progression : sourceContext.Trim();
        }

        public int PreviousLevel { get; }
        public int NewLevel { get; }
        public IReadOnlyList<LevelUpUnlockInfo> Unlocks { get; }
        public string FlavorText { get; }
        public string EffectPresetId { get; }
        public string SourceContext { get; }
        public bool HasUnlocks => Unlocks != null && Unlocks.Count > 0;
    }
}
