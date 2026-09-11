using System;
using System.Collections.Generic;
using Diceforge.Map;
using UnityEngine;

namespace Diceforge.Progression
{
    [Serializable]
    public sealed class ChapterProgress
    {
        public string chapterId;
        public string runId;
        public MapRunState state = new();
    }

    [Serializable]
    public sealed class ProgressionReceipt
    {
        public string operationId;
        public string chapterId;
        public string runId;
        public string nodeId;
        public RewardBundle reward = new();
        public int previousLevel;
        public int newLevel;
        public string sourceContext;
    }

    public enum ProgressionCommitStatus { Applied, AlreadyApplied, SaveFailed }

    public sealed class ProgressionCommitResult
    {
        public ProgressionCommitStatus Status { get; }
        public RewardApplicationResult Application { get; }
        public string Error { get; }
        public bool Succeeded => Status != ProgressionCommitStatus.SaveFailed;
        internal ProgressionCommitResult(ProgressionCommitStatus status, ProgressionReceipt receipt = null, string error = null)
        {
            Status = status;
            Error = error;
            if (receipt != null)
                Application = new RewardApplicationResult(receipt.reward, receipt.previousLevel, receipt.newLevel,
                    status == ProgressionCommitStatus.Applied
                        ? LevelUpProgressionService.Build(receipt.previousLevel, receipt.newLevel, receipt.sourceContext) : null,
                    receipt.sourceContext);
        }
    }

    // A prepared operation owns its snapshot. Retrying never rerolls rewards or chest IDs.
    public sealed class ProgressionOperation
    {
        internal string Id;
        internal string ChapterId;
        internal string RunId;
        internal string NodeId;
        internal string[] NextIds;
        internal bool Won;
        internal RewardBundle Reward;
        internal string Source;
    }

    public static class ProgressionTransactionService
    {
        public static ProgressionOperation Prepare(string operationId, string chapterId, string runId,
            string nodeId, IEnumerable<string> nextIds, bool won, RewardBundle reward, string source)
        {
            if (string.IsNullOrWhiteSpace(operationId)) throw new ArgumentException("Operation ID is required.");
            return new ProgressionOperation
            {
                Id = operationId, ChapterId = chapterId, RunId = runId, NodeId = nodeId, Won = won,
                NextIds = nextIds == null ? Array.Empty<string>() : new List<string>(nextIds).ToArray(),
                Reward = JsonUtility.FromJson<RewardBundle>(JsonUtility.ToJson(reward ?? new RewardBundle())), Source = source
            };
        }

        public static ProgressionCommitResult Commit(ProgressionOperation operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            var candidate = ProfileService.Snapshot();
            ChapterProgress chapter = null;
            if (!string.IsNullOrEmpty(operation.ChapterId))
            {
                chapter = candidate.chapters.Find(x => x.chapterId == operation.ChapterId);
                if (chapter == null || chapter.runId != operation.RunId)
                    return new ProgressionCommitResult(ProgressionCommitStatus.SaveFailed, error: "Результат относится к другому прохождению кампании.");
            }
            var existing = candidate.progressionReceipts.Find(x => x.operationId == operation.Id);
            if (existing != null)
                return new ProgressionCommitResult(ProgressionCommitStatus.AlreadyApplied, existing);

            var reward = operation.Reward;
            if (chapter != null)
            {
                if (!operation.Won || chapter.state.IsCompleted(operation.NodeId)) reward = new RewardBundle();
                if (operation.Won && !chapter.state.IsCompleted(operation.NodeId))
                {
                    chapter.state.MarkCompleted(operation.NodeId);
                    foreach (var next in operation.NextIds) chapter.state.Unlock(next);
                    if (operation.NextIds.Length > 0) chapter.state.currentNodeId = operation.NextIds[0];
                }
                else if (!operation.Won)
                {
                    chapter.state.currentNodeId = operation.NodeId;
                    chapter.state.Unlock(operation.NodeId);
                }
            }
            int previousLevel = UiProgressionService.GetLevelForXp(candidate.hero.xp);
            ApplyReward(candidate, reward);
            var receipt = new ProgressionReceipt
            {
                operationId = operation.Id, chapterId = operation.ChapterId, runId = operation.RunId, nodeId = operation.NodeId,
                reward = reward, previousLevel = previousLevel, newLevel = UiProgressionService.GetLevelForXp(candidate.hero.xp), sourceContext = operation.Source
            };
            candidate.progressionReceipts.Add(receipt);
            if (!ProfileService.TryCommit(candidate, out string error))
                return new ProgressionCommitResult(ProgressionCommitStatus.SaveFailed, error: error);
            return new ProgressionCommitResult(ProgressionCommitStatus.Applied, receipt);
        }

        internal static void ApplyReward(PlayerProfile profile, RewardBundle reward)
        {
            AddAmounts(profile.currencies, reward.currencies);
            AddAmounts(profile.inventory, reward.items);
            profile.hero.xp = checked(profile.hero.xp + Math.Max(0, reward.xp));
            profile.chestQueue ??= new();
            if (reward.chests != null)
                foreach (var chest in reward.chests)
                    if (chest != null && !profile.chestQueue.Exists(x => x.instanceId == chest.instanceId))
                        profile.chestQueue.Add(new ChestInstance(chest.instanceId, chest.chestTypeId));
        }

        private static void AddAmounts(List<ProfileAmount> target, List<ProfileAmount> additions)
        {
            if (additions == null) return;
            foreach (var amount in additions)
            {
                if (amount == null || string.IsNullOrEmpty(amount.id) || amount.amount <= 0) continue;
                var entry = target.Find(x => x.id == amount.id);
                if (entry == null) target.Add(new ProfileAmount(amount.id, amount.amount));
                else entry.amount = checked(entry.amount + amount.amount);
            }
        }
    }
}
