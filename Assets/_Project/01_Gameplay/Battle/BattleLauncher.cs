using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Diceforge.Battle
{
    /// <summary>
    /// New battle scene entry point that carries a request payload into the scene.
    /// </summary>
    public static class BattleLauncher
    {
        private const string BattleSceneName = "Battle";

        public static BattleStartRequest PendingRequest { get; private set; }

        public static void Start(BattleStartRequest request)
        {
            if (request == null)
                throw new InvalidOperationException("[BattleLauncher] Start failed: request is null.");

            if (request.presetOverride == null)
                throw new InvalidOperationException("[BattleLauncher] Start failed: request preset is null.");

            if (request.mapConfigOverride == null)
                throw new InvalidOperationException($"[BattleLauncher] Start failed: map is null for preset '{request.presetOverride.name}' modeId='{request.presetOverride.modeId}'.");

            PendingRequest = request;
            Debug.Log($"[BattleLauncher] Loading battle scene with request: {request.DebugSummary()}");
            SceneManager.LoadScene(BattleSceneName);
        }

        public static BattleStartRequest ConsumePendingRequest()
        {
            BattleStartRequest request = PendingRequest;
            PendingRequest = null;
            return request;
        }
    }
}
