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
            {
                Debug.LogError("[BattleLauncher] Start called with null request.");
                return;
            }

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
