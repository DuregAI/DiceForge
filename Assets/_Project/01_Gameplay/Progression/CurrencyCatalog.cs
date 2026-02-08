using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Currency Catalog", fileName = "CurrencyCatalog")]
    public sealed class CurrencyCatalog : ScriptableObject
    {
        public List<CurrencyDefinition> currencies = new();
    }
}
