using System;
using System.Collections.Generic;
using Diceforge.Progression;
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
    public sealed class MapChestRewardEntry
    {
        public string chestId;
        public int amount = 1;
    }

    [Serializable]
    public sealed class MapNodeRewardDefinition
    {
        public List<ProfileAmount> currencies = new();
        public List<ProfileAmount> items = new();
        public int xp;
        public List<MapChestRewardEntry> chests = new();
    }

    [Serializable]
    public sealed class MapNodeDefinition
    {
        public string id;
        public MapNodeType type;
        public Vector2 positionNormalized = new(0.5f, 0.5f);
        public List<string> nextIds = new();
        public string battlePresetId;
        public MapNodeRewardDefinition reward = new();
        public string storyLineId;
    }
}
