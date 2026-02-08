using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Chest Definition", fileName = "CHEST_New")]
    public sealed class ChestDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public List<FixedRewardEntry> fixedRewards = new();
    }
}
