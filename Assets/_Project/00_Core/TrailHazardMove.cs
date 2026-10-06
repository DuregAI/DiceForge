namespace Diceforge.Core
{
    public readonly struct TrailHazardMove
    {
        public int FromCell { get; }
        public int ToCell { get; }
        public bool Yielded { get; }
        public TrailHazardMove(int fromCell, int toCell, bool yielded)
        {
            FromCell = fromCell;
            ToCell = toCell;
            Yielded = yielded;
        }
    }
}
