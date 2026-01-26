using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Core
{
    public enum DiceBagDrawMode : byte
    {
        Sequential = 0,
        Shuffled = 1
    }

    [CreateAssetMenu(fileName = "DiceBagDefinition", menuName = "Diceforge/Dice Bag Definition")]
    public sealed class DiceBagDefinition : ScriptableObject
    {
        public DiceBagDrawMode drawMode = DiceBagDrawMode.Sequential;
        public List<DiceOutcomeDefinition> outcomes = new List<DiceOutcomeDefinition>();
    }

    [Serializable]
    public sealed class DiceOutcomeDefinition
    {
        public string label;
        public int weight = 1;
        public int[] dice = Array.Empty<int>();
    }
}
