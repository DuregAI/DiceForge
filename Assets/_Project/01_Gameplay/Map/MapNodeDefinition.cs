using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Map
{
    public enum MapNodeType
    {
        Battle,
        Chest,
        Shop,
        Story
    }

    [Serializable]
    public sealed class MapNodeDefinition
    {
        public string id;
        public MapNodeType type;
        public Vector2 positionNormalized = new(0.5f, 0.5f);
        public List<string> nextIds = new();
        public string battlePresetId;
        public string rewardPresetId;
        public string chestId;
        public string storyLineId;
    }
}
