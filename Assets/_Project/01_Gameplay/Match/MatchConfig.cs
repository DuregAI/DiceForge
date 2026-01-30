using System;
using System.Collections.Generic;
using Diceforge.Core;

namespace Diceforge.Match
{
    public sealed class MatchConfig
    {
        public RulesetConfig Rules { get; }
        public SetupConfig Setup { get; }
        public DiceBagRuntime BagA { get; }
        public DiceBagRuntime BagB { get; }

        public MatchConfig(RulesetConfig rules, SetupConfig setup, DiceBagRuntime bagA, DiceBagRuntime bagB)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            BagA = bagA ?? throw new ArgumentNullException(nameof(bagA));
            BagB = bagB ?? throw new ArgumentNullException(nameof(bagB));
        }

        public static MatchConfig Create(RulesetConfig rules, SetupConfig setup, DiceBagDefinition bagADefinition, DiceBagDefinition bagBDefinition)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));

            var bagAConfig = BuildBagConfig(bagADefinition ?? rules.diceBagA, rules);
            var bagBConfig = BuildBagConfig(bagBDefinition ?? rules.diceBagB, rules);

            var bagA = new DiceBagRuntime(bagAConfig, rules.randomSeed + 1000);
            var bagB = new DiceBagRuntime(bagBConfig, rules.randomSeed + 2000);

            return new MatchConfig(rules, setup, bagA, bagB);
        }

        private static DiceBagConfigData BuildBagConfig(DiceBagDefinition definition, RulesetConfig rules)
        {
            int dieMin = rules?.dieMin ?? 1;
            int dieMax = rules?.dieMax ?? 6;
            int safeMin = Math.Min(dieMin, dieMax);
            int safeMax = Math.Max(dieMin, dieMax);
            var outcomes = new List<DiceOutcomeData>();
            var drawMode = definition != null ? definition.drawMode : DiceBagDrawMode.Sequential;

            if (definition?.outcomes != null)
            {
                foreach (var outcome in definition.outcomes)
                {
                    if (outcome == null || outcome.dice == null || outcome.dice.Length == 0)
                        continue;

                    int weight = Math.Max(1, outcome.weight);
                    int length = Math.Clamp(outcome.dice.Length, 1, 6);
                    var dice = new int[length];
                    for (int i = 0; i < length; i++)
                        dice[i] = Math.Clamp(outcome.dice[i], safeMin, safeMax);

                    outcomes.Add(new DiceOutcomeData(outcome.label, weight, dice));
                }
            }

            if (outcomes.Count == 0)
            {
                int defaultDie = Math.Clamp(dieMax, safeMin, safeMax);
                outcomes.Add(new DiceOutcomeData("Default", 1, new[] { defaultDie }));
            }

            return new DiceBagConfigData(drawMode, outcomes);
        }
    }
}
