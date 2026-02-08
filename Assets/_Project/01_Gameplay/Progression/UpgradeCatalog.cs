using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        public List<UpgradeDefinition> upgrades = new();

        public UpgradeDefinition ResolveById(string upgradeId)
        {
            if (string.IsNullOrEmpty(upgradeId) || upgrades == null)
                return null;

            for (int i = 0; i < upgrades.Count; i++)
            {
                var entry = upgrades[i];
                if (entry != null && entry.upgradeId == upgradeId)
                    return entry;
            }

            return null;
        }
    }
}
