using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Core
{
    [CreateAssetMenu(fileName = "SetupPreset", menuName = "Diceforge/Setup Preset")]
    public sealed class SetupPreset : ScriptableObject
    {
        public string setupId = "default";
        public string displayName = "Default Setup";
        public int boardSize = 24;
        public List<UnitPlacement> unitPlacements = new List<UnitPlacement>();
    }

    [Serializable]
    public struct UnitPlacement
    {
        public PlayerId player;
        public int cellIndex;
        public int count;
    }
}
