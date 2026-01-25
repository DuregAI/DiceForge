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

        // позиции на кольце (устаревшее, не используется в новой модели)
        public int PosA { get; private set; }
        public int PosB { get; private set; }

        // клетки с фишками-препятствиями (устаревшее, не используется)
        public bool[] HasChip { get; }

        public int[] StonesAByCell { get; }
        public int[] StonesBByCell { get; }

        public int StonesInHandA { get; private set; }
        public int StonesInHandB { get; private set; }

        public bool IsFinished { get; private set; }
        public PlayerId? Winner { get; private set; }

        public GameState(RulesetConfig rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();

            HasChip = new bool[Rules.ringSize];
            StonesAByCell = new int[Rules.ringSize];
            StonesBByCell = new int[Rules.ringSize];

            Reset();
        }

        public void Reset()
        {
            Array.Clear(HasChip, 0, HasChip.Length);
            Array.Clear(StonesAByCell, 0, StonesAByCell.Length);
            Array.Clear(StonesBByCell, 0, StonesBByCell.Length);

            PosA = 0;
            PosB = Rules.ringSize / 2;

            int startCellA = Mod(Rules.startCellA, Rules.ringSize);
            int startCellB = Mod(Rules.startCellB, Rules.ringSize);
            int startStones = Math.Clamp(Rules.startStonesPerPlayer, 0, Rules.totalStonesPerPlayer);
            StonesAByCell[startCellA] = startStones;
            StonesBByCell[startCellB] = startStones;

            int stonesInHand = Rules.totalStonesPerPlayer - startStones;
            StonesInHandA = stonesInHand;
            StonesInHandB = stonesInHand;

            TurnIndex = 0;
            CurrentPlayer = PlayerId.A;
            CurrentRoll = 0;

            IsFinished = false;
            Winner = null;
        }

        public int GetPos(PlayerId p) => p == PlayerId.A ? PosA : PosB;
        public int GetOpponentPos(PlayerId p) => p == PlayerId.A ? PosB : PosA;

        public int GetStonesInHand(PlayerId p) => p == PlayerId.A ? StonesInHandA : StonesInHandB;

        public void SpendStoneFromHand(PlayerId p)
        {
            if (p == PlayerId.A) StonesInHandA--;
            else StonesInHandB--;
        }

        public void AddStoneToHand(PlayerId p)
        {
            if (p == PlayerId.A) StonesInHandA++;
            else StonesInHandB++;
        }

        public int GetStonesAt(PlayerId p, int cell)
        {
            cell = Mod(cell, Rules.ringSize);
            return p == PlayerId.A ? StonesAByCell[cell] : StonesBByCell[cell];
        }

        public void AddStoneToCell(PlayerId p, int cell)
        {
            cell = Mod(cell, Rules.ringSize);
            if (p == PlayerId.A) StonesAByCell[cell]++;
            else StonesBByCell[cell]++;
        }

        public bool RemoveStoneFromCell(PlayerId p, int cell)
        {
            cell = Mod(cell, Rules.ringSize);
            if (p == PlayerId.A)
            {
                if (StonesAByCell[cell] <= 0) return false;
                StonesAByCell[cell]--;
                return true;
            }

            if (StonesBByCell[cell] <= 0) return false;
            StonesBByCell[cell]--;
            return true;
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
            sb.Append($"Hand A:{StonesInHandA}  Hand B:{StonesInHandB}  ");
            sb.Append("A cells:");
            for (int i = 0; i < StonesAByCell.Length; i++)
                if (StonesAByCell[i] > 0) sb.Append($"{i}({StonesAByCell[i]}) ");
            sb.Append(" B cells:");
            for (int i = 0; i < StonesBByCell.Length; i++)
                if (StonesBByCell[i] > 0) sb.Append($"{i}({StonesBByCell[i]}) ");
            return sb.ToString();
        }

        public static int Mod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
