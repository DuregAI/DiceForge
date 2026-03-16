using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Chest Definition", fileName = "CHEST_New")]
    public sealed class ChestDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite icon;
        public string purchaseCurrencyId = ProgressionIds.SoftGold;
        [Min(0)] public int purchasePrice;
        public List<FixedRewardEntry> fixedRewards = new();
    }
}
