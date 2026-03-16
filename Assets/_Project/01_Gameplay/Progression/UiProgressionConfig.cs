using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/UI Progression Config", fileName = "UiProgressionConfig")]
    public sealed class UiProgressionConfig : ScriptableObject
    {
        [Header("Unlocks")]
        [Min(1)] public int chestSectionUnlockLevel = 3;
        [Min(1)] public int upgradesUnlockLevel = 4;

        [Header("Level Pacing")]
        [Min(1)] public int xpPerLevel = 100;

        [Header("Match Rewards")]
        [Min(0)] public int winXp = 10;
        [Min(0)] public int lossXp = 4;
        [Min(0)] public int winSoftGold = 30;
        [Min(0)] public int lossSoftGold = 10;
        [Min(0)] public int winEssence = 3;
        [Min(0)] public int lossEssence = 1;
        [Range(0f, 0.9f)] public float baseChestChanceOnWin = 0.25f;
    }
}
