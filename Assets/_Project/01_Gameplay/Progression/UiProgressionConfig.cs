using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/UI Progression Config", fileName = "UiProgressionConfig")]
    public sealed class UiProgressionConfig : ScriptableObject
    {
        [Min(1)] public int chestSectionUnlockLevel = 3;
        [Min(1)] public int upgradesUnlockLevel = 4;
    }
}
