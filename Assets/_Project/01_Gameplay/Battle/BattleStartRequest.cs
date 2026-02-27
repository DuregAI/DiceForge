using Diceforge.MapSystem;
using UnityEngine;

namespace Diceforge.Battle
{
    /// <summary>
    /// Data contract for entering the battle scene through the new launcher pipeline.
    /// </summary>
    public sealed class BattleStartRequest
    {
        public BattleStartRequest(GameModePreset preset, BattleMapConfig mapConfig = null)
        {
            presetOverride = preset;
            mapConfigOverride = mapConfig;
        }

        /// <summary>
        /// Existing game mode preset to run (Tutorial / Short / Long / Experimental).
        /// </summary>
        public GameModePreset presetOverride { get; }

        /// <summary>
        /// Optional map override. If null, the battle scene default map is used.
        /// </summary>
        public BattleMapConfig mapConfigOverride { get; }

        public string DebugSummary()
        {
            string presetName = presetOverride != null ? presetOverride.name : "<none>";
            string mapName = mapConfigOverride != null ? mapConfigOverride.name : "<default>";
            return $"preset={presetName}, map={mapName}";
        }
    }
}
