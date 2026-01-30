using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public readonly struct DiceOutcomeResult
    {
        public string Label { get; }
        public int[] Dice { get; }

        public DiceOutcomeResult(string label, int[] dice)
        {
            Label = label ?? string.Empty;
            Dice = dice ?? Array.Empty<int>();
        }

        public override string ToString()
        {
            return Dice.Length == 0 ? "-" : string.Join(", ", Dice);
        }
    }

    public sealed class DiceOutcomeData
    {
        public string Label { get; }
        public int Weight { get; }
        public int[] Dice { get; }

        public DiceOutcomeData(string label, int weight, int[] dice)
        {
            if (weight < 1) throw new ArgumentOutOfRangeException(nameof(weight));
            if (dice == null || dice.Length == 0) throw new ArgumentException("Dice must have at least one value.", nameof(dice));

            Label = label ?? string.Empty;
            Weight = weight;
            Dice = dice;
        }

        public DiceOutcomeResult ToResult()
        {
            var copy = new int[Dice.Length];
            Array.Copy(Dice, copy, Dice.Length);
            return new DiceOutcomeResult(Label, copy);
        }
    }

    public sealed class DiceBagConfigData
    {
        public DiceBagDrawMode DrawMode { get; }
        public IReadOnlyList<DiceOutcomeData> Outcomes { get; }

        public DiceBagConfigData(DiceBagDrawMode drawMode, IReadOnlyList<DiceOutcomeData> outcomes)
        {
            DrawMode = drawMode;
            Outcomes = outcomes ?? Array.Empty<DiceOutcomeData>();
        }
    }

    public sealed class DiceBagRuntime
    {
        private readonly DiceBagConfigData _config;
        private readonly Random _rng;
        private readonly List<int> _bagItems = new List<int>();
        private int _cursor;

        public DiceBagDrawMode DrawMode => _config.DrawMode;
        public int RemainingCount => Math.Max(0, _bagItems.Count - _cursor);
        public int TotalCount => _bagItems.Count;

        public DiceBagRuntime(DiceBagConfigData config, int seed)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _rng = new Random(seed);
            Reset();
        }

        public void Reset()
        {
            BuildBag();
            ShuffleIfNeeded();
            _cursor = 0;
        }

        public DiceOutcomeResult Draw()
        {
            if (_config.Outcomes.Count == 0)
                return new DiceOutcomeResult("Empty", Array.Empty<int>());

            if (_bagItems.Count == 0 || _cursor >= _bagItems.Count)
            {
                BuildBag();
                ShuffleIfNeeded();
                _cursor = 0;
            }

            int outcomeIndex = _bagItems[_cursor++];
            return _config.Outcomes[outcomeIndex].ToResult();
        }

        private void BuildBag()
        {
            _bagItems.Clear();

            for (int i = 0; i < _config.Outcomes.Count; i++)
            {
                var outcome = _config.Outcomes[i];
                int weight = Math.Max(1, outcome.Weight);
                for (int w = 0; w < weight; w++)
                    _bagItems.Add(i);
            }
        }

        private void ShuffleIfNeeded()
        {
            if (_config.DrawMode != DiceBagDrawMode.Shuffled)
                return;

            for (int i = _bagItems.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_bagItems[i], _bagItems[j]) = (_bagItems[j], _bagItems[i]);
            }
        }
    }
}
