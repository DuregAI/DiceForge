using System;
using System.Collections.Generic;

namespace Diceforge.Progression
{
    public sealed class RewardApplicationResult
    {
        private static readonly IReadOnlyList<ChestInstance> EmptyChests = Array.Empty<ChestInstance>();

        public RewardApplicationResult(RewardBundle rewardBundle, int previousLevel, int newLevel, LevelUpPresentationData levelUpData, string sourceContext)
        {
            RewardBundle = rewardBundle ?? new RewardBundle();
            PreviousLevel = previousLevel;
            NewLevel = newLevel;
            LevelUpData = levelUpData;
            SourceContext = string.IsNullOrWhiteSpace(sourceContext) ? LevelUpSourceContexts.Progression : sourceContext.Trim();
        }

        public RewardBundle RewardBundle { get; }
        public int PreviousLevel { get; }
        public int NewLevel { get; }
        public LevelUpPresentationData LevelUpData { get; }
        public string SourceContext { get; }
        public int XpGained => RewardBundle != null ? Math.Max(0, RewardBundle.xp) : 0;
        public bool DidLevelUp => LevelUpData != null;
        public bool HasChestRewards => GrantedChests.Count > 0;
        public IReadOnlyList<ChestInstance> GrantedChests => RewardBundle != null && RewardBundle.chests != null
            ? RewardBundle.chests
            : EmptyChests;
    }
}
