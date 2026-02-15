using System;
using System.Collections.Generic;

namespace Diceforge.Map
{
    [Serializable]
    public sealed class MapRunState
    {
        public string currentNodeId;
        public List<string> completedNodeIds = new();
        public List<string> unlockedNodeIds = new();

        public bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && completedNodeIds.Contains(id);
        public bool IsUnlocked(string id) => !string.IsNullOrEmpty(id) && unlockedNodeIds.Contains(id);

        public void MarkCompleted(string id)
        {
            if (string.IsNullOrEmpty(id) || completedNodeIds.Contains(id))
                return;

            completedNodeIds.Add(id);
        }

        public void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id) || unlockedNodeIds.Contains(id))
                return;

            unlockedNodeIds.Add(id);
        }
    }
}
