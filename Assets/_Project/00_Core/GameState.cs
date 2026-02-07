using System;
namespace Diceforge.Core
{
    public enum PlayerId : byte
    {
        A = 0,
        B = 1
    }

    public sealed class GameState
    {
        private readonly int[] _stonesAByCell;
        private readonly int[] _stonesBByCell;

        public RulesetConfig Rules { get; }
        public int TurnIndex { get; private set; }
        public PlayerId CurrentPlayer { get; private set; }
        public DiceOutcomeResult CurrentOutcome { get; private set; }

        public ReadOnlySpan<int> StonesAByCell => _stonesAByCell;
        public ReadOnlySpan<int> StonesBByCell => _stonesBByCell;

        public int BorneOffA { get; private set; }
        public int BorneOffB { get; private set; }
        public int BarA { get; private set; }
        public int BarB { get; private set; }

        public int TurnsTakenA { get; private set; }
        public int TurnsTakenB { get; private set; }

        public bool IsFinished { get; private set; }
        public PlayerId? Winner { get; private set; }

        public GameState(RulesetConfig rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();

            _stonesAByCell = new int[Rules.boardSize];
            _stonesBByCell = new int[Rules.boardSize];

            Reset();
        }

        public void Reset()
        {
            Array.Clear(_stonesAByCell, 0, _stonesAByCell.Length);
            Array.Clear(_stonesBByCell, 0, _stonesBByCell.Length);

            int startCellA = Rules.startCellA;
            int startCellB = Rules.startCellB;
            int startStones = Math.Max(0, Rules.totalStonesPerPlayer);
            _stonesAByCell[startCellA] = startStones;
            _stonesBByCell[startCellB] = startStones;

            BorneOffA = 0;
            BorneOffB = 0;
            BarA = 0;
            BarB = 0;

            TurnIndex = 0;
            CurrentPlayer = PlayerId.A;
            CurrentOutcome = new DiceOutcomeResult(string.Empty, Array.Empty<int>());

            TurnsTakenA = 0;
            TurnsTakenB = 0;

            IsFinished = false;
            Winner = null;
        }

        public int GetBorneOff(PlayerId p) => p == PlayerId.A ? BorneOffA : BorneOffB;

        public int GetBarCount(PlayerId p) => p == PlayerId.A ? BarA : BarB;

        public void AddBorneOff(PlayerId p)
        {
            if (p == PlayerId.A) BorneOffA++;
            else BorneOffB++;
        }

        public void AddToBar(PlayerId p, int count = 1)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Bar increment must be > 0.");

            if (p == PlayerId.A) BarA += count;
            else BarB += count;
        }

        public bool RemoveFromBar(PlayerId p, int count = 1)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Bar decrement must be > 0.");

            if (p == PlayerId.A)
            {
                if (BarA < count) return false;
                BarA -= count;
                return true;
            }

            if (BarB < count) return false;
            BarB -= count;
            return true;
        }

        public int GetTurnsTaken(PlayerId p) => p == PlayerId.A ? TurnsTakenA : TurnsTakenB;

        public int GetStonesAt(PlayerId p, int cell)
        {
            if (cell < 0 || cell >= Rules.boardSize)
                return 0;

            return p == PlayerId.A ? _stonesAByCell[cell] : _stonesBByCell[cell];
        }

        internal ReadOnlySpan<int> GetStonesByCell(PlayerId p)
        {
            return p == PlayerId.A ? _stonesAByCell : _stonesBByCell;
        }

        public void AddStoneToCell(PlayerId p, int cell)
        {
            if (cell < 0 || cell >= Rules.boardSize)
                throw new ArgumentOutOfRangeException(nameof(cell), $"Cell index must be in [0..{Rules.boardSize - 1}] for state writes.");

            if (p == PlayerId.A) _stonesAByCell[cell]++;
            else _stonesBByCell[cell]++;
        }

        public bool RemoveStoneFromCell(PlayerId p, int cell)
        {
            if (cell < 0 || cell >= Rules.boardSize)
                return false;

            if (p == PlayerId.A)
            {
                if (_stonesAByCell[cell] <= 0) return false;
                _stonesAByCell[cell]--;
                return true;
            }

            if (_stonesBByCell[cell] <= 0) return false;
            _stonesBByCell[cell]--;
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

        public string DebugSnapshot() => GameStateDebug.Snapshot(this);

        public static int Mod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
