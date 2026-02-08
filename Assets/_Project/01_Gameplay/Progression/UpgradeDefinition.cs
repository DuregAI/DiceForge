using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Upgrade Definition", fileName = "UPG_")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string upgradeId;
        public string displayName;
        [TextArea] public string description;
        public int maxLevel = 1;
        public UpgradeEffectType effectType;
        public int[] priceByLevel;
        public float[] valueByLevel;
        public bool requiresModeInfo;

        public int GetPriceForLevel(int level)
        {
            if (priceByLevel == null || level <= 0 || level > priceByLevel.Length)
                return 0;
            return Mathf.Max(0, priceByLevel[level - 1]);
        }

        public float GetValueForLevel(int level)
        {
            if (valueByLevel == null || level <= 0 || level > valueByLevel.Length)
                return 0f;
            return valueByLevel[level - 1];
        }
    }
}
