using System;
using System.Collections.Generic;

namespace Diceforge.Core
{
    public sealed class BattleRunner
    {
        private BotEasy _botA;
        private BotEasy _botB;
        private int _seed;
        private DiceBagRuntime _bagA;
        private DiceBagRuntime _bagB;
        private DiceOutcomeResult _currentOutcome;
        private readonly List<int> _remainingDice = new List<int>();
        private readonly List<int> _usedDice = new List<int>();
        private int? _selectedDieIndex;
        private int _headMovesUsed;
        private int _maxHeadMovesThisTurn;

        public GameState State { get; private set; }
        public MatchLog Log { get; } = new MatchLog();
        public RulesetConfig Rules { get; private set; }
        public DiceOutcomeResult CurrentOutcome => _currentOutcome;
        public IReadOnlyList<int> RemainingDice => _remainingDice;
        public IReadOnlyList<int> UsedDice => _usedDice;
        public int? SelectedDieIndex => _selectedDieIndex;
        public bool IsWaitingForDieSelection => _remainingDice.Count > 1 && !_selectedDieIndex.HasValue;
        public int HeadMovesUsed => _headMovesUsed;
        public int HeadMovesLimit => _maxHeadMovesThisTurn;
        public int CurrentBagRemaining => GetCurrentBag()?.RemainingCount ?? 0;
        public int CurrentBagTotal => GetCurrentBag()?.TotalCount ?? 0;

        public event Action<GameState> OnMatchStarted;
        public event Action<GameState> OnTurnStarted;
        public event Action<MoveRecord> OnMoveApplied;
        public event Action<GameState> OnMatchEnded;

        public void Init(RulesetConfig rules, DiceBagConfigData bagA, DiceBagConfigData bagB, int seed)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();
            _seed = seed;

            _bagA = bagA == null ? null : new DiceBagRuntime(bagA, _seed + 1000);
            _bagB = bagB == null ? null : new DiceBagRuntime(bagB, _seed + 2000);

            State = new GameState(Rules);
            CreateBots();
            Log.Clear();
            BeginTurn();
            LogHomeZonesOnce();

            OnMatchStarted?.Invoke(State);
        }

        public void Reset()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            State.Reset();
            CreateBots();
            Log.Clear();
            _bagA?.Reset();
            _bagB?.Reset();
            BeginTurn();
            LogHomeZonesOnce();

            OnMatchStarted?.Invoke(State);
        }

        public bool Tick()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            if (State.IsFinished)
                return false;

            if (_remainingDice.Count == 0)
            {
                EndTurn();
                return true;
            }

            if (!TryApplyBotMove())
            {
                EndTurn();
                return true;
            }

            return true;
        }

        public bool TryApplyHumanMove(Move move)
        {
            if (State == null || State.IsFinished)
                return false;

            int? selectedIndex = ResolveSelectedDieIndex();
            if (!selectedIndex.HasValue)
                return false;

            int dieValue = _remainingDice[selectedIndex.Value];
            var legal = MoveGenerator.GenerateLegalMoves(State, dieValue, _headMovesUsed, _maxHeadMovesThisTurn);
            if (legal.Count == 0)
            {
                EndTurn();
                return true;
            }

            if (move.PipUsed != dieValue)
                return false;

            if (!legal.Contains(move))
                return false;

            _selectedDieIndex = selectedIndex;
            return ApplyCurrentMove(move);
        }

        public bool SelectDieIndex(int index)
        {
            if (State == null || State.IsFinished)
                return false;
            if (index < 0 || index >= _remainingDice.Count)
                return false;

            _selectedDieIndex = index;
            return true;
        }

        public bool EnsureSelectedDie()
        {
            if (State == null || State.IsFinished)
                return false;

            if (_remainingDice.Count == 0)
            {
                _selectedDieIndex = null;
                return false;
            }

            if (_selectedDieIndex.HasValue && _selectedDieIndex.Value >= 0 && _selectedDieIndex.Value < _remainingDice.Count)
                return true;

            _selectedDieIndex = 0;
            return true;
        }

        public bool HasAnyLegalMove()
        {
            if (State == null || _remainingDice.Count == 0)
                return false;

            var seen = new HashSet<int>();
            foreach (var die in _remainingDice)
            {
                if (!seen.Add(die))
                    continue;

                var legal = MoveGenerator.GenerateLegalMoves(State, die, _headMovesUsed, _maxHeadMovesThisTurn);
                if (legal.Count > 0)
                    return true;
            }

            return false;
        }

        public bool HasLegalMoveForSelectedDie()
        {
            if (State == null || _remainingDice.Count == 0)
                return false;

            int? index = ResolveSelectedDieIndex();
            if (!index.HasValue)
                return false;

            int dieValue = _remainingDice[index.Value];
            return MoveGenerator.GenerateLegalMoves(State, dieValue, _headMovesUsed, _maxHeadMovesThisTurn).Count > 0;
        }

        public bool EndTurnIfNoMoves()
        {
            if (State == null || State.IsFinished)
                return false;

            if (HasAnyLegalMove())
                return false;

            EndTurn();
            return true;
        }

        private bool TryApplyBotMove()
        {
            var bot = State.CurrentPlayer == PlayerId.A ? _botA : _botB;
            var candidateIndices = new List<int>();

            for (int i = 0; i < _remainingDice.Count; i++)
            {
                int dieValue = _remainingDice[i];
                var legal = MoveGenerator.GenerateLegalMoves(State, dieValue, _headMovesUsed, _maxHeadMovesThisTurn);
                if (legal.Count > 0)
                    candidateIndices.Add(i);
            }

            if (candidateIndices.Count == 0)
                return false;

            int chosenIndex = bot.ChooseDieIndex(candidateIndices);
            if (chosenIndex < 0)
                return false;

            _selectedDieIndex = chosenIndex;
            int chosenValue = _remainingDice[chosenIndex];
            var chosenLegal = MoveGenerator.GenerateLegalMoves(State, chosenValue, _headMovesUsed, _maxHeadMovesThisTurn);
            if (chosenLegal.Count == 0)
                return false;

            var move = bot.ChooseMove(State, chosenLegal);
            return ApplyCurrentMove(move);
        }

        private void CreateBots()
        {
            _botA = new BotEasy(_seed + 100);
            _botB = new BotEasy(_seed + 200);
        }

        private void BeginTurn()
        {
            if (State.IsFinished) return;

            var bag = GetCurrentBag();
            _currentOutcome = bag != null
                ? bag.Draw()
                : new DiceOutcomeResult("Empty", Array.Empty<int>());
            State.SetCurrentOutcome(_currentOutcome);

            _remainingDice.Clear();
            _remainingDice.AddRange(_currentOutcome.Dice);
            _usedDice.Clear();

            _selectedDieIndex = _remainingDice.Count > 0 ? 0 : (int?)null;
            _headMovesUsed = 0;
            _maxHeadMovesThisTurn = CalculateHeadMoveLimit(State.CurrentPlayer, _currentOutcome);

            OnTurnStarted?.Invoke(State);
        }

        private DiceBagRuntime GetCurrentBag()
        {
            if (State == null)
                return null;

            return State.CurrentPlayer == PlayerId.A ? _bagA : _bagB;
        }

        private int CalculateHeadMoveLimit(PlayerId player, DiceOutcomeResult outcome)
        {
            if (Rules.headRules == null || !Rules.headRules.restrictHeadMoves)
                return int.MaxValue;

            if (State.GetTurnsTaken(player) == 0)
            {
                int dieA = outcome.Dice.Length > 0 ? outcome.Dice[0] : Rules.dieMin;
                int dieB = outcome.Dice.Length > 1 ? outcome.Dice[1] : dieA;
                int? allowance = Rules.headRules.GetFirstTurnAllowance(dieA, dieB);
                if (allowance.HasValue)
                    return allowance.Value;
            }

            return Rules.headRules.maxHeadMovesPerTurn;
        }

        private MoveRecord BuildRecord(
            PlayerId player,
            Move? move,
            int? fromCell,
            int? toCell,
            int? pipUsed,
            ApplyResult result,
            MatchEndReason endReason)
        {
            return new MoveRecord(
                State.TurnIndex,
                player,
                move,
                fromCell,
                toCell,
                pipUsed,
                _currentOutcome,
                _remainingDice.ToArray(),
                result,
                endReason,
                State.Winner
            );
        }

        private bool ApplyCurrentMove(Move move)
        {
            var beforeMove = DescribeMoveBeforeApply(move);
            var result = MoveGenerator.ApplyMove(State, move);
            var endReason = MatchEndReason.None;

            if (result == ApplyResult.Finished || State.IsFinished)
                endReason = MatchEndReason.Win;

            if (!State.IsFinished && State.TurnIndex >= Rules.maxTurns - 1)
            {
                var winner = DecideWinnerOnTimeout(State);
                State.Finish(winner);
                endReason = MatchEndReason.Timeout;
            }

            if (result == ApplyResult.Ok || result == ApplyResult.Finished)
            {
                ConsumeSelectedDie(move.PipUsed);
                if (beforeMove.FromCell.HasValue && beforeMove.FromCell.Value == GetHeadCell(State.CurrentPlayer))
                    _headMovesUsed++;
            }

            var record = BuildRecord(
                State.CurrentPlayer,
                move,
                beforeMove.FromCell,
                beforeMove.ToCell,
                move.PipUsed,
                result,
                endReason
            );
            Log.Add(record);
            OnMoveApplied?.Invoke(record);

            if (State.IsFinished)
            {
                OnMatchEnded?.Invoke(State);
                return true;
            }

            if (_remainingDice.Count == 0)
            {
                EndTurn();
                return true;
            }

            if (!HasAnyLegalMove())
            {
                EndTurn();
                return true;
            }

            return true;
        }

        private void ConsumeSelectedDie(int pip)
        {
            int index = ResolveSelectedDieIndex() ?? _remainingDice.IndexOf(pip);
            if (index >= 0 && index < _remainingDice.Count)
            {
                int value = _remainingDice[index];
                _remainingDice.RemoveAt(index);
                _usedDice.Add(value);
            }

            _selectedDieIndex = _remainingDice.Count > 0 ? 0 : (int?)null;
        }

        private int? ResolveSelectedDieIndex()
        {
            if (_selectedDieIndex.HasValue && _selectedDieIndex.Value >= 0 && _selectedDieIndex.Value < _remainingDice.Count)
                return _selectedDieIndex.Value;

            if (_remainingDice.Count == 1)
                return 0;

            return null;
        }

        private void EndTurn()
        {
            if (State.IsFinished)
                return;

            State.AdvanceTurn();
            BeginTurn();
        }

        private static PlayerId DecideWinnerOnTimeout(GameState s)
        {
            return PlayerId.A;
        }

        private (int? FromCell, int? ToCell) DescribeMoveBeforeApply(Move move)
        {
            if (State == null) return (null, null);
            if (move.FromCell < 0 || move.FromCell >= State.Rules.boardSize)
                return (null, null);

            int from = move.FromCell;
            var classification = BoardPathRules.ClassifyMove(State.Rules, State.CurrentPlayer, from, move.PipUsed, out _, out int to);

            if (move.Kind == MoveKind.BearOff || classification == MovePathClassification.ExactBearOff)
                return (from, null);

            if (classification != MovePathClassification.Normal)
                return (from, null);

            return (from, to);
        }


        private void LogHomeZonesOnce()
        {
            if (!Rules.verboseLog)
                return;

            var homeA = BoardPathRules.GetHomeCells(Rules, PlayerId.A);
            var homeB = BoardPathRules.GetHomeCells(Rules, PlayerId.B);
            Console.WriteLine($"[HomeZone] A: [{string.Join(",", homeA)}]");
            Console.WriteLine($"[HomeZone] B: [{string.Join(",", homeB)}]");
        }

        private int GetHeadCell(PlayerId player)
        {
            return player == PlayerId.A ? Rules.startCellA : Rules.startCellB;
        }
    }
}
