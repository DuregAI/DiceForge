namespace Diceforge.Core
{
    public readonly struct DiceRoll
    {
        public int DieA { get; }
        public int DieB { get; }
        public bool IsDouble => DieA == DieB;

        public DiceRoll(int dieA, int dieB)
        {
            DieA = dieA;
            DieB = dieB;
        }

        public override string ToString()
        {
            return IsDouble ? $"{DieA},{DieB} (DOUBLE)" : $"{DieA},{DieB}";
        }
    }
}
