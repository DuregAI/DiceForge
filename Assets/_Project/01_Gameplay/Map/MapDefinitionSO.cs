using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Map
{
    [CreateAssetMenu(menuName = "Diceforge/Map/Map Definition", fileName = "MapDefinition")]
    public sealed class MapDefinitionSO : ScriptableObject
    {
        public string chapterId = "Chapter1";
        public string startNodeId = "C1_01";
        public List<MapNodeDefinition> nodes = new();

        public MapNodeDefinition GetNode(string nodeId)
        {
            return nodes.Find(x => x != null && x.id == nodeId);
        }

        public static MapDefinitionSO LoadChapter(string chapterId)
        {
            var loaded = Resources.Load<MapDefinitionSO>($"Map/{chapterId}_MapDefinition");
            if (loaded != null)
                return loaded;

            if (chapterId == "Chapter1")
                return BuildChapter1RuntimeFallback();

            return null;
        }

        private static MapDefinitionSO BuildChapter1RuntimeFallback()
        {
            var map = CreateInstance<MapDefinitionSO>();
            map.chapterId = "Chapter1";
            map.startNodeId = "C1_01";
            map.nodes = new List<MapNodeDefinition>
            {
                CreateNode("C1_01", MapNodeType.Battle, new Vector2(0.08f, 0.20f), "short", "battle_small", "", "C1_02"),
                CreateNode("C1_02", MapNodeType.Battle, new Vector2(0.18f, 0.32f), "long", "battle_small", "", "C1_03"),
                CreateNode("C1_03", MapNodeType.Chest, new Vector2(0.28f, 0.22f), "", "chest_basic", "CHEST_BASIC", "C1_04"),
                CreateNode("C1_04", MapNodeType.Battle, new Vector2(0.38f, 0.36f), "short", "battle_medium", "", "C1_05"),
                CreateNode("C1_05", MapNodeType.Shop, new Vector2(0.49f, 0.24f), "", "shop_visit", "", "C1_06"),
                CreateNode("C1_06", MapNodeType.Battle, new Vector2(0.60f, 0.38f), "long", "battle_medium", "", "C1_07"),
                CreateNode("C1_07", MapNodeType.Battle, new Vector2(0.70f, 0.25f), "short", "battle_medium", "", "C1_08"),
                CreateNode("C1_08", MapNodeType.Chest, new Vector2(0.78f, 0.42f), "", "chest_basic", "CHEST_BASIC", "C1_09"),
                CreateNode("C1_09", MapNodeType.Battle, new Vector2(0.86f, 0.28f), "long", "battle_large", "", "C1_10"),
                CreateNode("C1_10", MapNodeType.Battle, new Vector2(0.94f, 0.40f), "long", "battle_boss", "", "")
            };
            return map;
        }

        private static MapNodeDefinition CreateNode(string id, MapNodeType type, Vector2 pos, string battlePresetId, string rewardPresetId, string chestId, string nextId)
        {
            var node = new MapNodeDefinition
            {
                id = id,
                type = type,
                positionNormalized = pos,
                battlePresetId = battlePresetId,
                rewardPresetId = rewardPresetId,
                chestId = chestId,
                nextIds = new List<string>()
            };

            if (!string.IsNullOrWhiteSpace(nextId))
                node.nextIds.Add(nextId);

            return node;
        }
    }
}
