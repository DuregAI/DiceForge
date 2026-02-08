using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Chest Catalog", fileName = "ChestCatalog")]
    public sealed class ChestCatalog : ScriptableObject
    {
        public List<ChestDefinition> chests = new();
    }
}
