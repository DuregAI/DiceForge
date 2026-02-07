using System;
using System.Collections.Generic;
using Diceforge.Core;
using Diceforge.Presets;
using UnityEngine;

public static class MatchService
{
    public static GameModePreset ActivePreset { get; private set; }
    public static RulesetConfig Rules { get; private set; }
    public static DiceBagConfigData BagA { get; private set; }
    public static DiceBagConfigData BagB { get; private set; }
    public static SetupConfig Setup { get; private set; }
    public static BattleRunner Runner { get; private set; }

    public static event Action<GameState> OnMatchStarted;
    public static event Action<GameState> OnTurnStarted;
    public static event Action<MoveRecord> OnMoveApplied;
    public static event Action<MatchResult> OnMatchEnded;

    public static void BuildFromPreset(GameModePreset preset)
    {
        if (preset == null)
        {
            Debug.LogError("[MatchService] Cannot build match config: preset is null.");
            return;
        }

        ActivePreset = preset;
        Rules = BuildRules(preset);
        if (Rules == null)
            return;
        BagA = BuildBagConfig(preset.diceBagA, Rules, "A");
        BagB = BuildBagConfig(preset.diceBagB, Rules, "B");
        Setup = BuildSetup(preset.setupPreset);

        InitializeRunner();

        Debug.Log($"[MatchService] Built match config for preset: {preset.modeId} / {preset.displayName}.");
    }

    public static void Reset()
    {
        if (ActivePreset == null)
        {
            Debug.LogWarning("[MatchService] Reset requested without an active preset.");
            return;
        }

        DisposeRunner();
        ClearEventSubscribers();
        BuildFromPreset(ActivePreset);

        Debug.Log("[MatchService] Reset complete.");
    }

    private static RulesetConfig BuildRules(GameModePreset preset)
    {
        if (preset.rulesetPreset == null)
        {
            Debug.LogError("[MatchService] Missing RulesetPreset on GameModePreset.");
            return null;
        }

        var rules = RulesetConfig.FromPreset(preset.rulesetPreset);
        rules.Validate();
        return rules;
    }

    private static SetupConfig BuildSetup(SetupPreset preset)
    {
        if (preset == null)
        {
            Debug.LogWarning("[MatchService] Missing SetupPreset, using empty setup.");
            return new SetupConfig(string.Empty, "Empty", 0, Array.Empty<UnitPlacement>());
        }

        return SetupConfig.FromPreset(preset);
    }

    private static DiceBagConfigData BuildBagConfig(DiceBagDefinition definition, RulesetConfig rules, string label)
    {
        int dieMin = rules?.dieMin ?? 1;
        int dieMax = rules?.dieMax ?? 6;
        var outcomes = new List<DiceOutcomeData>();
        var drawMode = definition != null ? definition.drawMode : DiceBagDrawMode.Sequential;

        if (definition != null && definition.outcomes != null)
        {
            foreach (var outcome in definition.outcomes)
            {
                if (outcome == null || outcome.dice == null || outcome.dice.Length == 0)
                    continue;

                int weight = Mathf.Max(1, outcome.weight);
                int length = Mathf.Clamp(outcome.dice.Length, 1, 6);
                var dice = new int[length];
                for (int i = 0; i < length; i++)
                    dice[i] = Mathf.Clamp(outcome.dice[i], dieMin, dieMax);

                outcomes.Add(new DiceOutcomeData(outcome.label, weight, dice));
            }
        }

        if (outcomes.Count == 0)
        {
            int defaultDie = Mathf.Clamp(dieMax, dieMin, dieMax);
            outcomes.Add(new DiceOutcomeData("Default", 1, new[] { defaultDie }));
            if (definition == null)
                Debug.LogWarning($"[MatchService] Missing DiceBagDefinition for bag {label}, using fallback outcome.");
        }

        return new DiceBagConfigData(drawMode, outcomes);
    }

    private static void InitializeRunner()
    {
        if (Rules == null)
        {
            Debug.LogError("[MatchService] Cannot initialize runner without rules.");
            return;
        }

        DisposeRunner();
        Runner = new BattleRunner();
        AttachRunner(Runner);
        Runner.Init(Rules, BagA, BagB, Rules.randomSeed, Setup);
    }

    private static void DisposeRunner()
    {
        if (Runner == null)
            return;

        DetachRunner(Runner);
        Runner = null;
    }

    private static void AttachRunner(BattleRunner runner)
    {
        runner.OnMatchStarted += HandleMatchStarted;
        runner.OnTurnStarted += HandleTurnStarted;
        runner.OnMoveApplied += HandleMoveApplied;
        runner.OnMatchEnded += HandleMatchEnded;
    }

    private static void DetachRunner(BattleRunner runner)
    {
        runner.OnMatchStarted -= HandleMatchStarted;
        runner.OnTurnStarted -= HandleTurnStarted;
        runner.OnMoveApplied -= HandleMoveApplied;
        runner.OnMatchEnded -= HandleMatchEnded;
    }

    private static void HandleMatchStarted(GameState state)
    {
        OnMatchStarted?.Invoke(state);
    }

    private static void HandleTurnStarted(GameState state)
    {
        OnTurnStarted?.Invoke(state);
    }

    private static void HandleMoveApplied(MoveRecord record)
    {
        OnMoveApplied?.Invoke(record);
    }

    private static void HandleMatchEnded(MatchResult result)
    {
        OnMatchEnded?.Invoke(result);
    }

    private static void ClearEventSubscribers()
    {
        OnMatchStarted = null;
        OnTurnStarted = null;
        OnMoveApplied = null;
        OnMatchEnded = null;
    }
}
