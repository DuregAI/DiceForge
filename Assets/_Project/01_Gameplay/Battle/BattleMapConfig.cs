using Diceforge.Core;
using Diceforge.Map;
using UnityEngine;

namespace Diceforge.MapSystem
{
    [CreateAssetMenu(menuName = "Diceforge/Battle Map Config", fileName = "BattleMapConfig")]
    public sealed class BattleMapConfig : ScriptableObject
    {
        [Header("Identity")]
        public string mapId;
        public string displayName;

        [Header("Gameplay")]
        public BoardLayout boardLayout;

        [Header("Visual")]
        public MapTheme mapTheme;
        public BoardVisualMode visualMode;

        public bool TryValidate(out string error)
        {
            error = null;

            if (boardLayout == null)
            {
                error = "BoardLayout is missing.";
                return false;
            }

            if (boardLayout.cells == null || boardLayout.cells.Count == 0)
            {
                error = "BoardLayout has no cells.";
                return false;
            }

            if (mapTheme == null)
            {
                error = "MapTheme is missing.";
                return false;
            }
            return true;
        }
    }
}
