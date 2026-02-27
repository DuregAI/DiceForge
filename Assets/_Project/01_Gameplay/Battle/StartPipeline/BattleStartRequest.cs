using Diceforge.MapSystem;
namespace Diceforge.BattleStart
{
    /// <summary>
    /// Request payload for the new battle start pipeline.
    /// Keeps mode + map context in one explicit object.
    /// </summary>
    public sealed class BattleStartRequest
    {
        public BattleStartRequest(GameModePreset preset, BattleMapConfig mapConfig, int? seedOverride = null, bool debugStart = false)
        {
            Preset = preset;
            MapConfig = mapConfig;
            SeedOverride = seedOverride;
            DebugStart = debugStart;
        }

        public GameModePreset Preset { get; }
        public BattleMapConfig MapConfig { get; }
        public int? SeedOverride { get; }
        public bool DebugStart { get; }
    }
}
