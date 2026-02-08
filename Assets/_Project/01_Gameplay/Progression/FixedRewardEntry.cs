using System;

namespace Diceforge.Progression
{
    [Serializable]
    public sealed class FixedRewardEntry
    {
        public string id;
        public int amount;
        public bool isItem;
    }
}
