using UnityEngine;

namespace Diceforge.Map
{
    [CreateAssetMenu(menuName = "Diceforge/Map/Dev Mode Config", fileName = "DevModeConfig")]
    public sealed class DevModeConfigSO : ScriptableObject
    {
        public bool devModeEnabled;
    }
}
