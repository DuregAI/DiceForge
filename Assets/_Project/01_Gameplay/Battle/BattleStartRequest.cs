using System;
using Diceforge.MapSystem;
using UnityEngine;

namespace Diceforge.Battle
{
    /// <summary>
    /// Data contract for entering the battle scene through the new launcher pipeline.
    /// </summary>
    public sealed class BattleStartRequest
    {
        public BattleStartRequest(GameModePreset preset, BattleMapConfig mapConfig)
        {
            presetOverride = preset ?? throw new ArgumentNullException(nameof(preset), "[BattleStartRequest] Preset is required for strict new-start pipeline.");
            mapConfigOverride = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig), "[BattleStartRequest] BattleMapConfig is required for strict new-start pipeline.");
        }

        /// <summary>
        /// Existing game mode preset to run (Tutorial / Short / Long / Experimental).
        /// </summary>
        public GameModePreset presetOverride { get; }

        /// <summary>
        /// Required map override for strict pipeline.
        /// </summary>
        public BattleMapConfig mapConfigOverride { get; }

        public string DebugSummary()
        {
            string presetName = presetOverride != null ? presetOverride.name : "<none>";
            string mapName = mapConfigOverride != null ? mapConfigOverride.name : "<none>";
            return $"preset={presetName}, map={mapName}";
        }
    }
}
