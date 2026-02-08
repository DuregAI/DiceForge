using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Item Definition", fileName = "ITEM_New")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public ItemType itemType = ItemType.Unknown;
    }
}
