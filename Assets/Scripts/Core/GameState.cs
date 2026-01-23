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

        // позиции на кольце
        public int PosA { get; private set; }
        public int PosB { get; private set; }

        // заблокированные клетки
        public bool[] Blocked { get; }

        public int BlocksLeftA { get; private set; }
        public int BlocksLeftB { get; private set; }

        public bool IsFinished { get; private set; }
        public PlayerId? Winner { get; private set; }

        public GameState(RulesetConfig rules)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();

            Blocked = new bool[Rules.ringSize];

            Reset();
        }

        public void Reset()
        {
            Array.Clear(Blocked, 0, Blocked.Length);

            // старт: напротив друг друга (для 9 будет 0 и 4)
            PosA = 0;
            PosB = Rules.ringSize / 2;

            BlocksLeftA = Rules.blocksPerPlayer;
            BlocksLeftB = Rules.blocksPerPlayer;

            TurnIndex = 0;
            CurrentPlayer = PlayerId.A;

            IsFinished = false;
            Winner = null;
        }

        public int GetPos(PlayerId p) => p == PlayerId.A ? PosA : PosB;
        public int GetOpponentPos(PlayerId p) => p == PlayerId.A ? PosB : PosA;

        public int GetBlocksLeft(PlayerId p) => p == PlayerId.A ? BlocksLeftA : BlocksLeftB;

        public void SpendBlock(PlayerId p)
        {
            if (p == PlayerId.A) BlocksLeftA--;
            else BlocksLeftB--;
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

        public string DebugSnapshot()
        {
            // компактный снимок: позиции, блоки, чей ход
            var sb = new StringBuilder();
            sb.Append($"T{TurnIndex} P:{CurrentPlayer}  ");
            sb.Append($"A@{PosA}({BlocksLeftA}b)  B@{PosB}({BlocksLeftB}b)  ");
            sb.Append("Blocked:");
            for (int i = 0; i < Blocked.Length; i++)
                if (Blocked[i]) sb.Append(i).Append(' ');
            return sb.ToString();
        }

        public static int Mod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
