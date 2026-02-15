using System;
using System.Collections.Generic;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        private MapDefinitionSO _map;
        private MapRunState _state;

        public bool IsDevMode => devModeConfig != null ? devModeConfig.devModeEnabled : Debug.isDebugBuild;

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
            if (_map == null)
            {
                Debug.LogError($"[MapFlow] Map chapter not found: {chapterId}");
                return;
            }

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
                    CompleteNonBattleNode(node, BuildRewardBundle(node.rewardPresetId));
                    break;
                case MapNodeType.Story:
                    CompleteNonBattleNode(node, BuildRewardBundle(node.rewardPresetId));
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

            if (MapFlowRuntime.LastBattleWon)
            {
                var reward = BuildRewardBundle(node.rewardPresetId);
                if (reward != null && !reward.IsEmpty)
                    ProfileService.ApplyReward(reward);

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
        }

        private void LaunchBattle(MapNodeDefinition node)
        {
            var preset = battlePresets.Find(x => x != null && x.modeId == node.battlePresetId);
            if (preset == null && battlePresets.Count > 0)
                preset = battlePresets[0];

            if (preset == null)
            {
                Debug.LogError("[MapFlow] No battle preset configured for map battle node.");
                return;
            }

            GameModeSelection.SetSelected(preset);
            MapFlowRuntime.StartNodeBattle(_map.chapterId, node.id);
            SceneManager.LoadScene("Battle");
        }

        private RewardBundle HandleChestReward(MapNodeDefinition node)
        {
            if (!string.IsNullOrWhiteSpace(node.chestId))
            {
                var chest = ChestService.CreateChestInstance(node.chestId);
                if (chest != null)
                {
                    return new RewardBundle { chests = new List<ChestInstance> { chest } };
                }
            }

            return BuildRewardBundle(node.rewardPresetId);
        }

        private void CompleteNonBattleNode(MapNodeDefinition node, RewardBundle reward)
        {
            if (reward != null && !reward.IsEmpty)
                ProfileService.ApplyReward(reward);

            CompleteNode(node);
            MapProgressService.Save(_map.chapterId, _state);
            RefreshMap();
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

        private static RewardBundle BuildRewardBundle(string rewardPresetId)
        {
            var bundle = new RewardBundle();
            switch (rewardPresetId)
            {
                case "battle_small":
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.SoftGold, 25));
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.Essence, 2));
                    break;
                case "battle_medium":
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.SoftGold, 45));
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.Essence, 4));
                    break;
                case "battle_large":
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.SoftGold, 70));
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.Essence, 7));
                    break;
                case "battle_boss":
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.SoftGold, 120));
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.Essence, 10));
                    bundle.items.Add(new ProfileAmount(ProgressionIds.Shards, 10));
                    break;
                case "chest_basic":
                    bundle.chests.Add(ChestService.CreateChestInstance(ProgressionIds.BasicChest));
                    break;
                case "shop_visit":
                    bundle.currencies.Add(new ProfileAmount(ProgressionIds.SoftGold, 30));
                    break;
            }

            return bundle;
        }
    }
}
