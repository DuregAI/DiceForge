using System;
using System.Text;

namespace Diceforge.Core
{
    public enum PlayerId : byte
    {
        A = 0,
        B = 1
    }

    public sealed class GameState
    {
        public RulesetConfig Rules { get; }
        public int TurnIndex { get; private set; } // 0-based
        public PlayerId CurrentPlayer { get; private set; }
        public int CurrentRoll { get; private set; }

        // позиции на кольце
        public int PosA { get; private set; }
        public int PosB { get; private set; }

        // клетки с фишками-препятствиями
        public bool[] HasChip { get; }

        public int ChipsInHandA { get; private set; }
        public int ChipsInHandB { get; private set; }

        public bool IsFinished { get; private set; }
        public PlayerId? Winner { get; private set; }

        public GameState(RulesetConfig rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();

            HasChip = new bool[Rules.ringSize];

            Reset();
        }

        public void Reset()
        {
            Array.Clear(HasChip, 0, HasChip.Length);

            // старт: напротив друг друга (для 9 будет 0 и 4)
            PosA = 0;
            PosB = Rules.ringSize / 2;

            ChipsInHandA = Rules.chipsPerPlayer;
            ChipsInHandB = Rules.chipsPerPlayer;

            TurnIndex = 0;
            CurrentPlayer = PlayerId.A;
            CurrentRoll = 0;

            IsFinished = false;
            Winner = null;
        }

        public int GetPos(PlayerId p) => p == PlayerId.A ? PosA : PosB;
        public int GetOpponentPos(PlayerId p) => p == PlayerId.A ? PosB : PosA;

        public int GetChipsInHand(PlayerId p) => p == PlayerId.A ? ChipsInHandA : ChipsInHandB;

        public void SpendChip(PlayerId p)
        {
            if (p == PlayerId.A) ChipsInHandA--;
            else ChipsInHandB--;
        }

        public void SetPos(PlayerId p, int pos)
        {
            pos = Mod(pos, Rules.ringSize);
            if (p == PlayerId.A) PosA = pos;
            else PosB = pos;
        }

        public void AdvanceTurn()
        {
            TurnIndex++;
            CurrentPlayer = (CurrentPlayer == PlayerId.A) ? PlayerId.B : PlayerId.A;
        }

        public void Finish(PlayerId winner)
        {
            IsFinished = true;
            Winner = winner;
        }

        public void SetCurrentRoll(int roll)
        {
            CurrentRoll = roll;
        }

        public string DebugSnapshot()
        {
            // компактный снимок: позиции, блоки, чей ход
            var sb = new StringBuilder();
            sb.Append($"T{TurnIndex} P:{CurrentPlayer}  ");
            sb.Append($"A@{PosA}({ChipsInHandA}c)  B@{PosB}({ChipsInHandB}c)  ");
            sb.Append("Chips:");
            for (int i = 0; i < HasChip.Length; i++)
                if (HasChip[i]) sb.Append(i).Append(' ');
            return sb.ToString();
        }

        public static int Mod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
