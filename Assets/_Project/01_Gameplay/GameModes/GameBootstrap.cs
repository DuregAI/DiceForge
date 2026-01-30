using Diceforge.Core;
using Diceforge.Match;
using Diceforge.View;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameModePreset fallbackPreset;
    [SerializeField] private MatchController matchController;

    private void Start()
    {
        var selectedPreset = GameModeSelection.SelectedPreset;
        if (selectedPreset == null)
        {
            if (fallbackPreset != null)
            {
                Debug.LogWarning("[GameBootstrap] No preset selected, using fallback.");
                selectedPreset = fallbackPreset;
            }
            else
            {
                Debug.LogWarning("[GameBootstrap] No preset selected, using fallback.");
                return;
            }
        }

        Debug.Log($"[GameBootstrap] Selected mode: {selectedPreset.modeId} / {selectedPreset.displayName}");

        if (selectedPreset.rulesetPreset == null)
        {
            Debug.LogWarning("[GameBootstrap] Selected preset is missing RulesetPreset.");
            return;
        }

        if (selectedPreset.setupPreset == null)
        {
            Debug.LogWarning("[GameBootstrap] Selected preset is missing SetupPreset.");
            return;
        }

        Debug.Log($"[GameBootstrap] Ruleset: {selectedPreset.rulesetPreset.rulesetId} / {selectedPreset.rulesetPreset.displayName}");
        Debug.Log($"[GameBootstrap] Setup: {selectedPreset.setupPreset.setupId} / {selectedPreset.setupPreset.displayName}");

        string bagAName = ResolveBagName(selectedPreset.diceBagA);
        string bagBName = ResolveBagName(selectedPreset.diceBagB);
        Debug.Log($"[GameBootstrap] Dice bags: A={bagAName} B={bagBName}");

        if (selectedPreset.diceBagA == null)
            Debug.LogWarning("[GameBootstrap] Missing DiceBagDefinition for Bag A; using fallback.");
        if (selectedPreset.diceBagB == null)
            Debug.LogWarning("[GameBootstrap] Missing DiceBagDefinition for Bag B; using fallback.");

        var battleController = FindAnyObjectByType<BattleDebugController>();
        if (battleController != null)
        {
            Debug.Log("[GameBootstrap] BattleDebugController found; bootstrapping battle debug mode.");
            battleController.StartFromPreset(selectedPreset);
            return;
        }

        var rules = RulesetConfig.FromPreset(selectedPreset.rulesetPreset);
        var setup = SetupConfig.FromPreset(selectedPreset.setupPreset);
        Debug.Log("[GameBootstrap] Creating match config.");
        var matchConfig = MatchConfig.Create(rules, setup, selectedPreset.diceBagA, selectedPreset.diceBagB);
        Debug.Log("[GameBootstrap] Match config created.");

        if (matchController == null)
        {
            Debug.LogWarning("[GameBootstrap] MatchController reference missing, searching scene.");
            matchController = FindAnyObjectByType<MatchController>();
        }

        if (matchController == null)
        {
            Debug.LogWarning("[GameBootstrap] MatchController not found. Aborting match initialization.");
            return;
        }

        matchController.Initialize(matchConfig);
    }

    private static string ResolveBagName(DiceBagDefinition primary)
    {
        if (primary != null)
            return primary.name;
        return "Default";
    }
}
