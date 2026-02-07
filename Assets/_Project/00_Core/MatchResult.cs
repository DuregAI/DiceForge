namespace Diceforge.Core
{
    public readonly struct MatchResult
    {
        public MatchResult(PlayerId winner, MatchEndReason reason)
        {
            Winner = winner;
            Reason = reason;
        }

        public PlayerId Winner { get; }
        public MatchEndReason Reason { get; }
    }
}
