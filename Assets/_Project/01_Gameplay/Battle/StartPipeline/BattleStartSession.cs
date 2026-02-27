namespace Diceforge.BattleStart
{
    /// <summary>
    /// Runtime handoff storage for battle start requests between scenes.
    /// </summary>
    public static class BattleStartSession
    {
        public static BattleStartRequest CurrentRequest { get; private set; }

        public static void Set(BattleStartRequest request)
        {
            CurrentRequest = request;
        }

        public static BattleStartRequest Consume()
        {
            BattleStartRequest request = CurrentRequest;
            CurrentRequest = null;
            return request;
        }

        public static bool HasRequest => CurrentRequest != null;
    }
}
