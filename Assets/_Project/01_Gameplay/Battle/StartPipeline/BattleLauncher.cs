using UnityEngine;
using UnityEngine.SceneManagement;

namespace Diceforge.BattleStart
{
    public static class BattleLauncher
    {
        private const string BattleSceneName = "Battle";

        public static void Start(BattleStartRequest request)
        {
            if (request == null)
            {
                Debug.LogError("[BattleLauncher] Start called with null request.");
                return;
            }

            if (request.Preset == null)
            {
                Debug.LogError("[BattleLauncher] Request is missing a GameModePreset.");
                return;
            }

            if (request.MapConfig == null)
            {
                Debug.LogError("[BattleLauncher] Request is missing a BattleMapConfig.");
                return;
            }

            BattleStartSession.Set(request);
            Debug.Log($"[BattleLauncher] New pipeline start -> preset={request.Preset.name}, map={request.MapConfig.name}, seedOverride={(request.SeedOverride.HasValue ? request.SeedOverride.Value.ToString() : "none")}, debugStart={request.DebugStart}");
            SceneManager.LoadScene(BattleSceneName);
        }
    }
}
