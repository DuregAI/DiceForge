using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Database", fileName = "ProgressionDatabase")]
    public sealed class ProgressionDatabase : ScriptableObject
    {
        public CurrencyCatalog currencyCatalog;
        public ItemCatalog itemCatalog;
        public ChestCatalog chestCatalog;
        public UpgradeCatalog upgradeCatalog;
    }
}
