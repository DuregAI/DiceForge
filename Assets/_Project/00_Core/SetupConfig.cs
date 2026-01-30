using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public sealed class SetupConfig
    {
        public string SetupId { get; }
        public string DisplayName { get; }
        public int BoardSize { get; }
        public IReadOnlyList<UnitPlacement> UnitPlacements { get; }

        public SetupConfig(string setupId, string displayName, int boardSize, IReadOnlyList<UnitPlacement> unitPlacements)
        {
            SetupId = setupId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            BoardSize = boardSize;
            UnitPlacements = unitPlacements ?? Array.Empty<UnitPlacement>();
        }

        public static SetupConfig FromPreset(SetupPreset preset)
        {
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));

            var placements = preset.unitPlacements != null
                ? new List<UnitPlacement>(preset.unitPlacements)
                : new List<UnitPlacement>();

            return new SetupConfig(preset.setupId, preset.displayName, preset.boardSize, placements);
        }
    }
}
