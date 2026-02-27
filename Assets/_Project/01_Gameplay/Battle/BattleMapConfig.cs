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
        public GameModePreset gameModePreset;
        public BoardLayout boardLayout;

        [Header("Visual")]
        public MapTheme mapTheme;
        public BoardVisualMode visualMode;

        [Header("Meta")]
        public bool deprecated;

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

            if (gameModePreset == null)
            {
                error = "GameModePreset is missing.";
                return false;
            }

            if (gameModePreset.rulesetPreset == null)
            {
                error = "GameModePreset has no RulesetPreset.";
                return false;
            }

            if (mapTheme == null)
            {
                error = "MapTheme is missing.";
                return false;
            }

            RulesetConfig rules = RulesetConfig.FromPreset(gameModePreset.rulesetPreset);
            int boardSize = GetBoardSize();

            if (rules.startCellA < 0 || rules.startCellA >= boardSize)
            {
                error = $"startCellA={rules.startCellA} is outside [0..{boardSize - 1}].";
                return false;
            }

            if (rules.startCellB < 0 || rules.startCellB >= boardSize)
            {
                error = $"startCellB={rules.startCellB} is outside [0..{boardSize - 1}].";
                return false;
            }

            return true;
        }

        public int GetBoardSize()
        {
            return boardLayout != null && boardLayout.cells != null
                ? boardLayout.cells.Count
                : 0;
        }
    }
}
