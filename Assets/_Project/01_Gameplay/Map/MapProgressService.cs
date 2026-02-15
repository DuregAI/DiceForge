using UnityEngine;

namespace Diceforge.Map
{
    public static class MapProgressService
    {
        private const string KeyPrefix = "map_state_";

        public static MapRunState Load(MapDefinitionSO map)
        {
            var key = KeyPrefix + map.chapterId;
            if (!PlayerPrefs.HasKey(key))
            {
                var fresh = CreateNewRun(map);
                Save(map.chapterId, fresh);
                return fresh;
            }

            var json = PlayerPrefs.GetString(key, string.Empty);
            var loaded = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<MapRunState>(json);
            if (loaded == null)
            {
                loaded = CreateNewRun(map);
            }

            loaded.unlockedNodeIds ??= new();
            loaded.completedNodeIds ??= new();
            if (string.IsNullOrEmpty(loaded.currentNodeId))
                loaded.currentNodeId = map.startNodeId;
            loaded.Unlock(map.startNodeId);
            return loaded;
        }

        public static void Save(string chapterId, MapRunState state)
        {
            var key = KeyPrefix + chapterId;
            PlayerPrefs.SetString(key, JsonUtility.ToJson(state));
            PlayerPrefs.Save();
        }

        public static void Reset(string chapterId)
        {
            PlayerPrefs.DeleteKey(KeyPrefix + chapterId);
            PlayerPrefs.Save();
        }

        private static MapRunState CreateNewRun(MapDefinitionSO map)
        {
            var state = new MapRunState
            {
                currentNodeId = map.startNodeId
            };
            state.Unlock(map.startNodeId);
            return state;
        }
    }
}
