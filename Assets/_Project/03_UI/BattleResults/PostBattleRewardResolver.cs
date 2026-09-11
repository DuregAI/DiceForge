using System;
using Diceforge.Core;
using Diceforge.Map;
using Diceforge.Progression;

namespace Diceforge.View
{
    internal sealed class PostBattleRewardOutcome
    {
        public PostBattleRewardOutcome(bool won, bool isMapBattle, RewardBundle rewardBundle, RewardApplicationResult applicationResult, bool isDraw = false)
        {
            Won = won;
            IsMapBattle = isMapBattle;
            IsDraw = isDraw;
            RewardBundle = rewardBundle ?? new RewardBundle();
            ApplicationResult = applicationResult ?? new RewardApplicationResult(RewardBundle, UiProgressionService.GetPlayerLevel(), UiProgressionService.GetPlayerLevel(), null, LevelUpSourceContexts.Battle);
        }

        public bool Won { get; }
        public bool IsDraw { get; }
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
        private static RewardBundle ResolveRewardBundle(MatchResult result, bool won, bool isMapBattle)
        {
            if (result.IsDraw)
                return new RewardBundle();

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

    internal sealed class BattleRewardSession
    {
        private readonly string _id = Guid.NewGuid().ToString("N");
        private readonly string _chapterId;
        private readonly string _runId;
        private readonly string _nodeId;
        private readonly string _modeId;
        private readonly MapNodeDefinition _node;
        private ProgressionOperation _operation;
        private MatchResult? _matchResult;
        private bool _won;
        internal ProgressionCommitResult CommitResult { get; private set; }
        internal PostBattleRewardOutcome Outcome { get; private set; }
        internal bool HasPendingSave => _matchResult != null && (CommitResult == null || !CommitResult.Succeeded);
        internal bool PresentationStarted { get; set; }

        internal BattleRewardSession(string modeId)
        {
            _modeId = modeId;
            if (!MapFlowRuntime.IsMapBattleActive) return;
            _chapterId = MapFlowRuntime.ChapterId;
            _nodeId = MapFlowRuntime.SelectedNodeId;
            _runId = MapProgressService.GetRunId(_chapterId);
            _node = MapDefinitionSO.LoadChapter(_chapterId).GetNode(_nodeId)
                ?? throw new InvalidOperationException("Battle node is missing: " + _nodeId);
        }

        internal void Complete(MatchResult result, bool won)
        {
            if (_matchResult == null) { _matchResult = result; _won = won && !result.IsDraw; }
            Retry();
        }

        internal void Retry()
        {
            if (_matchResult == null || (CommitResult != null && CommitResult.Succeeded)) return;
            try
            {
                if (_operation == null)
                {
                    RewardBundle reward;
                    if (_matchResult.Value.IsDraw || (_node != null && (!_won || ProfileService.Current.chapters.Find(x => x.chapterId == _chapterId).state.IsCompleted(_nodeId))))
                        reward = new RewardBundle();
                    else reward = _node != null ? MapFlowOrchestrator.BuildRewardBundle(_node.reward, _nodeId)
                        : RewardService.CalculateMatchRewards(_matchResult.Value, _modeId);
                    _operation = ProgressionTransactionService.Prepare(_id, _chapterId, _runId, _nodeId,
                        _node?.nextIds, _won, reward, LevelUpSourceContexts.Battle);
                }
                CommitResult = ProgressionTransactionService.Commit(_operation);
                if (!CommitResult.Succeeded) return;
                Outcome = new PostBattleRewardOutcome(_won, _node != null, CommitResult.Application.RewardBundle,
                    CommitResult.Application, _matchResult.Value.IsDraw);
                if (_node != null) MapFlowRuntime.ReportBattleResult(_won);
            }
            catch (Exception exception)
            {
                CommitResult = new ProgressionCommitResult(ProgressionCommitStatus.SaveFailed, error: exception.Message);
            }
        }
    }
}
