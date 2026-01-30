using System.Text;

namespace Diceforge.Core
{
    public static class GameStateDebug
    {
        public static string Snapshot(GameState state)
        {
            var sb = new StringBuilder();
            sb.Append($"T{state.TurnIndex} P:{state.CurrentPlayer}  ");
            sb.Append($"Off A:{state.BorneOffA}  Off B:{state.BorneOffB}  ");
            sb.Append("A cells:");
            ReadOnlySpan<int> stonesAByCell = state.StonesAByCell;
            for (int i = 0; i < stonesAByCell.Length; i++)
                if (stonesAByCell[i] > 0) sb.Append($"{i}({stonesAByCell[i]}) ");
            sb.Append(" B cells:");
            ReadOnlySpan<int> stonesBByCell = state.StonesBByCell;
            for (int i = 0; i < stonesBByCell.Length; i++)
                if (stonesBByCell[i] > 0) sb.Append($"{i}({stonesBByCell[i]}) ");
            return sb.ToString();
        }
    }
}
