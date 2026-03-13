using System;
using System.Collections.Generic;
using Diceforge.Battle;
using Diceforge.MapSystem;
using Diceforge.Progression;
using UnityEngine;

namespace Diceforge.Map
{
    public static class MapFlowRuntime
    {
        public static bool IsMapBattleActive => !string.IsNullOrEmpty(SelectedNodeId);
        public static string ChapterId { get; private set; }
        public static string SelectedNodeId { get; private set; }
        public static bool HasPendingBattleResult { get; private set; }
        public static bool LastBattleWon { get; private set; }
        public static bool ReturnToMapRequested { get; private set; }

        public static void StartNodeBattle(string chapterId, string nodeId)
        {
            ChapterId = chapterId;
            SelectedNodeId = nodeId;
            HasPendingBattleResult = false;
        }

        public static void ReportBattleResult(bool won)
        {
            LastBattleWon = won;
            HasPendingBattleResult = true;
            ReturnToMapRequested = true;
        }

        public static void RequestReturnToMap()
        {
            ReturnToMapRequested = true;
        }

        public static bool ConsumeReturnToMapRequest()
        {
            if (!ReturnToMapRequested)
                return false;

            ReturnToMapRequested = false;
            return true;
        }

        public static void ClearPendingResult()
        {
            HasPendingBattleResult = false;
        }

        public static void ClearRunContext()
        {
            ChapterId = string.Empty;
            SelectedNodeId = string.Empty;
            HasPendingBattleResult = false;
            LastBattleWon = false;
            ReturnToMapRequested = false;
        }
    }

    public sealed class MapFlowOrchestrator : MonoBehaviour
    {
        [SerializeField] private MapController mapController;
        [SerializeField] private DevModeConfigSO devModeConfig;
        [SerializeField] private List<GameModePreset> battlePresets = new();
        [SerializeField] private List<BattleMapConfig> battleMaps = new();

        private MapDefinitionSO _map;
        private MapRunState _state;
        private LevelUpWindowPresenter _levelUpPresenter;

        public bool IsDevMode => devModeConfig != null ? devModeConfig.devModeEnabled : Debug.isDebugBuild;

        public void SetLevelUpPresenter(LevelUpWindowPresenter presenter)
        {
            _levelUpPresenter = presenter;
        }

        private void Awake()
        {
            if (mapController == null)
                mapController = GetComponent<MapController>();

            if (mapController != null)
            {
                mapController.ResetRunRequested -= ResetRun;
                mapController.ResetRunRequested += ResetRun;
                mapController.UnlockAllRequested -= UnlockAll;
                mapController.UnlockAllRequested += UnlockAll;
            }
        }

        public void StartChapter(string chapterId)
        {
            _map = MapDefinitionSO.LoadChapter(chapterId);
            _state = MapProgressService.Load(_map);
            RefreshMap();
            ProcessPendingBattleResultIfAny();
        }

        public void RefreshMap()
        {
            if (_map == null || mapController == null)
                return;

            mapController.Show(_map, _state, OnNodeSelected, IsDevMode);
        }

        public void OnNodeSelected(string nodeId)
        {
            var node = _map.GetNode(nodeId);
            if (node == null)
                return;

            _state.currentNodeId = nodeId;

            switch (node.type)
            {
                case MapNodeType.Battle:
                    LaunchBattle(node);
                    break;
                case MapNodeType.Chest:
                    CompleteNonBattleNode(node, HandleChestReward(node));
                    break;
                case MapNodeType.Shop:
                    CompleteNonBattleNode(node, BuildRewardBundle(node.reward, node.id));
                    break;
                case MapNodeType.Story:
                    CompleteNonBattleNode(node, BuildRewardBundle(node.reward, node.id));
                    break;
            }
        }

        public void ResetRun()
        {
            if (_map == null)
                return;

            MapProgressService.Reset(_map.chapterId);
            _state = MapProgressService.Load(_map);
            RefreshMap();
        }

        public void UnlockAll()
        {
            if (_map == null)
                return;

            foreach (var node in _map.nodes)
                _state.Unlock(node.id);

            MapProgressService.Save(_map.chapterId, _state);
            RefreshMap();
        }

        public void ProcessPendingBattleResultIfAny()
        {
            if (_map == null || !MapFlowRuntime.HasPendingBattleResult || !MapFlowRuntime.IsMapBattleActive)
                return;

            var node = _map.GetNode(MapFlowRuntime.SelectedNodeId);
            if (node == null)
            {
                MapFlowRuntime.ClearRunContext();
                return;
            }

            LevelUpPresentationData levelUpData = null;
            if (MapFlowRuntime.LastBattleWon)
            {
                var reward = BuildRewardBundle(node.reward, node.id);
                if (reward != null && !reward.IsEmpty)
                    levelUpData = ProfileService.ApplyReward(reward, LevelUpSourceContexts.Battle);

                CompleteNode(node);
            }
            else
            {
                _state.currentNodeId = node.id;
                if (!_state.IsUnlocked(node.id))
                    _state.Unlock(node.id);
            }

            MapProgressService.Save(_map.chapterId, _state);
            MapFlowRuntime.ClearPendingResult();
            MapFlowRuntime.ConsumeReturnToMapRequest();
            RefreshMap();
            ShowLevelUp(levelUpData);
        }

        private void LaunchBattle(MapNodeDefinition node)
        {
            var preset = battlePresets.Find(x => x != null && x.modeId == node.battlePresetId);
            if (preset == null)
                throw new InvalidOperationException($"[MapFlow] LaunchBattle failed: preset not configured for node '{node.id}' battlePresetId='{node.battlePresetId}'.");

            var map = preset.mapConfig;
            if (!map.TryValidate(out string validationError))
                throw new InvalidOperationException($"[MapFlow] LaunchBattle failed: map '{map.name}' mapId='{map.mapId}' invalid: {validationError}.");

            MapFlowRuntime.StartNodeBattle(_map.chapterId, node.id);
            BattleLauncher.Start(new BattleStartRequest(preset, map));
        }

        private RewardBundle HandleChestReward(MapNodeDefinition node)
        {
            return BuildRewardBundle(node.reward, node.id);
        }

        private void CompleteNonBattleNode(MapNodeDefinition node, RewardBundle reward)
        {
            LevelUpPresentationData levelUpData = null;
            if (reward != null && !reward.IsEmpty)
                levelUpData = ProfileService.ApplyReward(reward, LevelUpSourceContexts.Progression);

            CompleteNode(node);
            MapProgressService.Save(_map.chapterId, _state);
            RefreshMap();
            ShowLevelUp(levelUpData);
        }

        private void ShowLevelUp(LevelUpPresentationData data)
        {
            if (data == null)
                return;

            _levelUpPresenter?.Show(data);
        }

        private void CompleteNode(MapNodeDefinition node)
        {
            _state.MarkCompleted(node.id);
            foreach (var nextId in node.nextIds)
                _state.Unlock(nextId);

            if (node.nextIds.Count == 1)
                _state.currentNodeId = node.nextIds[0];
            else if (node.nextIds.Count > 1)
                _state.currentNodeId = node.nextIds[0];
        }

        private static RewardBundle BuildRewardBundle(MapNodeRewardDefinition rewardDefinition, string nodeId)
        {
            var bundle = new RewardBundle();
            if (rewardDefinition == null)
                return bundle;

            if (rewardDefinition.currencies != null)
            {
                foreach (var currency in rewardDefinition.currencies)
                {
                    ValidateProfileAmount(currency, nodeId, "currency");
                    bundle.currencies.Add(new ProfileAmount(currency.id, currency.amount));
                }
            }

            if (rewardDefinition.items != null)
            {
                foreach (var item in rewardDefinition.items)
                {
                    ValidateProfileAmount(item, nodeId, "item");
                    bundle.items.Add(new ProfileAmount(item.id, item.amount));
                }
            }

            if (rewardDefinition.xp < 0)
                throw new InvalidOperationException($"[MapFlow] Reward config invalid for node '{nodeId}': xp cannot be negative.");

            bundle.xp = rewardDefinition.xp;

            if (rewardDefinition.chests != null)
            {
                foreach (var chestReward in rewardDefinition.chests)
                {
                    ValidateChestReward(chestReward, nodeId);
                    for (int i = 0; i < chestReward.amount; i++)
                    {
                        ChestInstance chest = ChestService.CreateChestInstance(chestReward.chestId);
                        if (chest == null)
                            throw new InvalidOperationException($"[MapFlow] Reward config invalid for node '{nodeId}': chest '{chestReward.chestId}' could not be created.");

                        bundle.chests.Add(chest);
                    }
                }
            }

            return bundle;
        }

        private static void ValidateProfileAmount(ProfileAmount amount, string nodeId, string rewardType)
        {
            if (amount == null)
                throw new InvalidOperationException($"[MapFlow] Reward config invalid for node '{nodeId}': {rewardType} entry is null.");

            if (string.IsNullOrWhiteSpace(amount.id))
                throw new InvalidOperationException($"[MapFlow] Reward config invalid for node '{nodeId}': {rewardType} id is empty.");

            if (amount.amount <= 0)
                throw new InvalidOperationException($"[MapFlow] Reward config invalid for node '{nodeId}': {rewardType} '{amount.id}' amount must be > 0.");
        }

        private static void ValidateChestReward(MapChestRewardEntry chestReward, string nodeId)
        {
            if (chestReward == null)
                throw new InvalidOperationException($"[MapFlow] Reward config invalid for node '{nodeId}': chest reward entry is null.");

            if (string.IsNullOrWhiteSpace(chestReward.chestId))
                throw new InvalidOperationException($"[MapFlow] Reward config invalid for node '{nodeId}': chest reward id is empty.");

            if (chestReward.amount <= 0)
                throw new InvalidOperationException($"[MapFlow] Reward config invalid for node '{nodeId}': chest '{chestReward.chestId}' amount must be > 0.");
        }
    }
}
