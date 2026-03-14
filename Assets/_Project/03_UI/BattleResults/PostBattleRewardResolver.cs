using System;
using Diceforge.Core;
using Diceforge.Map;
using Diceforge.Progression;

namespace Diceforge.View
{
    internal sealed class PostBattleRewardOutcome
    {
        public PostBattleRewardOutcome(bool won, bool isMapBattle, RewardBundle rewardBundle, RewardApplicationResult applicationResult)
        {
            Won = won;
            IsMapBattle = isMapBattle;
            RewardBundle = rewardBundle ?? new RewardBundle();
            ApplicationResult = applicationResult ?? new RewardApplicationResult(RewardBundle, UiProgressionService.GetPlayerLevel(), UiProgressionService.GetPlayerLevel(), null, LevelUpSourceContexts.Battle);
        }

        public bool Won { get; }
        public bool IsMapBattle { get; }
        public RewardBundle RewardBundle { get; }
        public RewardApplicationResult ApplicationResult { get; }
        public bool HasRewardSummary => HasCurrenciesOrItems(RewardBundle);

        private static bool HasCurrenciesOrItems(RewardBundle bundle)
        {
            if (bundle == null)
                return false;

            bool hasCurrencies = bundle.currencies != null && bundle.currencies.Exists(entry => entry != null && entry.amount > 0);
            bool hasItems = bundle.items != null && bundle.items.Exists(entry => entry != null && entry.amount > 0);
            return hasCurrencies || hasItems;
        }
    }

    internal static class PostBattleRewardResolver
    {
        public static PostBattleRewardOutcome Resolve(MatchResult result, bool won)
        {
            bool isMapBattle = MapFlowRuntime.IsMapBattleActive;
            RewardBundle rewardBundle = ResolveRewardBundle(result, won, isMapBattle);
            RewardApplicationResult applicationResult = ProfileService.ApplyRewardDetailed(rewardBundle, LevelUpSourceContexts.Battle);

            if (isMapBattle && won)
                MapFlowRuntime.MarkRewardsHandledInBattleFlow();

            return new PostBattleRewardOutcome(won, isMapBattle, rewardBundle, applicationResult);
        }

        private static RewardBundle ResolveRewardBundle(MatchResult result, bool won, bool isMapBattle)
        {
            if (isMapBattle)
            {
                if (!won)
                    return new RewardBundle();

                if (string.IsNullOrWhiteSpace(MapFlowRuntime.ChapterId) || string.IsNullOrWhiteSpace(MapFlowRuntime.SelectedNodeId))
                {
                    throw new InvalidOperationException("[PostBattleRewardResolver] Map battle reward resolution failed: map runtime context is incomplete.");
                }

                MapDefinitionSO map = MapDefinitionSO.LoadChapter(MapFlowRuntime.ChapterId);
                MapNodeDefinition node = map.GetNode(MapFlowRuntime.SelectedNodeId);
                if (node == null)
                {
                    throw new InvalidOperationException($"[PostBattleRewardResolver] Map battle reward resolution failed: node '{MapFlowRuntime.SelectedNodeId}' was not found in chapter '{MapFlowRuntime.ChapterId}'.");
                }

                return MapFlowOrchestrator.BuildRewardBundle(node.reward, node.id);
            }

            string modeId = MatchService.ActivePreset != null ? MatchService.ActivePreset.modeId : string.Empty;
            return RewardService.CalculateMatchRewards(result, modeId);
        }
    }
}
