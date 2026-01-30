using UnityEngine;

namespace Diceforge.Match
{
    public sealed class MatchController : MonoBehaviour
    {
        public MatchConfig Config { get; private set; }

        public void Initialize(MatchConfig config)
        {
            if (config == null)
            {
                Debug.LogWarning("[MatchController] Null MatchConfig provided.", this);
                return;
            }

            Config = config;
            Debug.Log($"[MatchController] Initialized. Ruleset={config.Rules.rulesetId} Setup={config.Setup.SetupId}");
        }
    }
}
