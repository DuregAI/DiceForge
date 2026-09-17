using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Map
{
    [CreateAssetMenu(menuName = "Diceforge/Map/Map Definition", fileName = "MapDefinition")]
    public sealed class MapDefinitionSO : ScriptableObject
    {
        public string chapterId = "Chapter1";
        public string startNodeId = "C1_01";
        public bool useWoodlandLayout;
        public List<MapNodeDefinition> nodes = new();

        public MapNodeDefinition GetNode(string nodeId)
        {
            return nodes.Find(x => x != null && x.id == nodeId);
        }

        public static MapDefinitionSO LoadChapter(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                throw new InvalidOperationException("[MapDefinitionSO] LoadChapter failed: requested chapterId is null or empty. Authored map data is required.");

            string normalizedChapterId = chapterId.Trim();
            string resourcesPath = $"Map/{normalizedChapterId}_MapDefinition";
            MapDefinitionSO loaded = Resources.Load<MapDefinitionSO>(resourcesPath);
            if (loaded == null)
            {
                throw new InvalidOperationException(
                    $"[MapDefinitionSO] LoadChapter failed: authored map asset is missing for chapterId='{normalizedChapterId}' at Resources path '{resourcesPath}'. Runtime fallback is disabled; authored map data is required.");
            }

            loaded.ValidateLoadedChapter(normalizedChapterId);
            return loaded;
        }

        private void ValidateLoadedChapter(string requestedChapterId)
        {
            string assetName = string.IsNullOrWhiteSpace(name) ? "<unnamed>" : name;

            if (string.IsNullOrWhiteSpace(chapterId))
                throw BuildValidationException(assetName, "chapterId is null or empty.");

            if (string.IsNullOrWhiteSpace(startNodeId))
                throw BuildValidationException(assetName, "startNodeId is null or empty.");

            if (nodes == null || nodes.Count == 0)
                throw BuildValidationException(assetName, "nodes list is null or empty.");

            if (!string.Equals(chapterId, requestedChapterId, StringComparison.Ordinal))
            {
                throw BuildValidationException(
                    assetName,
                    $"asset chapterId='{chapterId}' does not match requested chapterId='{requestedChapterId}'.");
            }

            if (GetNode(startNodeId) == null)
                throw BuildValidationException(assetName, $"startNodeId='{startNodeId}' does not resolve to a node in the authored map definition.");

            if (useWoodlandLayout)
            {
                if (nodes.Count != 6 || startNodeId != nodes[0]?.id)
                    throw BuildValidationException(assetName, "Woodland layout requires six ordered levels, starting at the first node.");
                var ids = new HashSet<string>();
                for (int i = 0; i < nodes.Count; i++)
                {
                    var node = nodes[i];
                    if (node == null || string.IsNullOrWhiteSpace(node.id) || !ids.Add(node.id) ||
                        node.type != MapNodeType.Battle || string.IsNullOrWhiteSpace(node.battlePresetId))
                        throw BuildValidationException(assetName, "Woodland nodes must be unique battle levels with configured presets.");
                    int expectedNextCount = i == nodes.Count - 1 ? 0 : 1;
                    if (node.nextIds == null || node.nextIds.Count != expectedNextCount ||
                        (expectedNextCount == 1 && node.nextIds[0] != nodes[i + 1]?.id))
                        throw BuildValidationException(assetName, "Woodland levels must form one ordered chain with no branches.");
                }
            }
        }

        private static InvalidOperationException BuildValidationException(string assetName, string reason)
        {
            return new InvalidOperationException($"[MapDefinitionSO] LoadChapter failed: asset '{assetName}' is invalid: {reason}");
        }
    }
}
