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
        public int TurnIndex { get; private set; }
        public PlayerId CurrentPlayer { get; private set; }
        public DiceOutcomeResult CurrentOutcome { get; private set; }

        public int[] StonesAByCell { get; }
        public int[] StonesBByCell { get; }

        public int BorneOffA { get; private set; }
        public int BorneOffB { get; private set; }

        public int TurnsTakenA { get; private set; }
        public int TurnsTakenB { get; private set; }

        public bool IsFinished { get; private set; }
        public PlayerId? Winner { get; private set; }

        public GameState(RulesetConfig rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();

            StonesAByCell = new int[Rules.boardSize];
            StonesBByCell = new int[Rules.boardSize];

            Reset();
        }

        public void Reset()
        {
            Array.Clear(StonesAByCell, 0, StonesAByCell.Length);
            Array.Clear(StonesBByCell, 0, StonesBByCell.Length);

            int startCellA = Mod(Rules.startCellA, Rules.boardSize);
            int startCellB = Mod(Rules.startCellB, Rules.boardSize);
            int startStones = Math.Clamp(Rules.totalStonesPerPlayer, 0, Rules.totalStonesPerPlayer);
            StonesAByCell[startCellA] = startStones;
            StonesBByCell[startCellB] = startStones;

            BorneOffA = 0;
            BorneOffB = 0;

            TurnIndex = 0;
            CurrentPlayer = PlayerId.A;
            CurrentOutcome = new DiceOutcomeResult(string.Empty, Array.Empty<int>());

            TurnsTakenA = 0;
            TurnsTakenB = 0;

            IsFinished = false;
            Winner = null;
        }

        public int GetBorneOff(PlayerId p) => p == PlayerId.A ? BorneOffA : BorneOffB;

        public void AddBorneOff(PlayerId p)
        {
            if (p == PlayerId.A) BorneOffA++;
            else BorneOffB++;
        }

        public int GetTurnsTaken(PlayerId p) => p == PlayerId.A ? TurnsTakenA : TurnsTakenB;

        public int GetStonesAt(PlayerId p, int cell)
        {
            cell = Mod(cell, Rules.boardSize);
            return p == PlayerId.A ? StonesAByCell[cell] : StonesBByCell[cell];
        }

        public void AddStoneToCell(PlayerId p, int cell)
        {
            cell = Mod(cell, Rules.boardSize);
            if (p == PlayerId.A) StonesAByCell[cell]++;
            else StonesBByCell[cell]++;
        }

        public bool RemoveStoneFromCell(PlayerId p, int cell)
        {
            cell = Mod(cell, Rules.boardSize);
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
            if (CurrentPlayer == PlayerId.A) TurnsTakenA++;
            else TurnsTakenB++;

            TurnIndex++;
            CurrentPlayer = (CurrentPlayer == PlayerId.A) ? PlayerId.B : PlayerId.A;
        }

        public void Finish(PlayerId winner)
        {
            IsFinished = true;
            Winner = winner;
        }

        public void SetCurrentOutcome(DiceOutcomeResult outcome)
        {
            CurrentOutcome = outcome;
        }

        public string DebugSnapshot()
        {
            var sb = new StringBuilder();
            sb.Append($"T{TurnIndex} P:{CurrentPlayer}  ");
            sb.Append($"Off A:{BorneOffA}  Off B:{BorneOffB}  ");
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
