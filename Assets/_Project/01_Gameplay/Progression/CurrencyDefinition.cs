using UnityEngine;

namespace Diceforge.Progression
{
    [CreateAssetMenu(menuName = "Diceforge/Progression/Currency Definition", fileName = "CUR_New")]
    public sealed class CurrencyDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
    }
}
