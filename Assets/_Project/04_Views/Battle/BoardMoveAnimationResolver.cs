namespace Diceforge.View
{
    internal static class BoardMoveAnimationResolver
    {
        public static bool TryResolveSignedSteps(int boardSize, int fromCell, int toCell, int pipUsed, out int steps)
        {
            steps = 0;

            if (boardSize <= 0 || pipUsed <= 0)
                return false;

            int forward = (toCell - fromCell + boardSize) % boardSize;
            int backward = -((fromCell - toCell + boardSize) % boardSize);

            if (forward == pipUsed)
            {
                steps = pipUsed;
                return true;
            }

            if (-backward == pipUsed)
            {
                steps = backward;
                return true;
            }

            return false;
        }
    }
}
