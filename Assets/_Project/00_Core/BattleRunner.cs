using System;
using System.Collections.Generic;
using UnityEngine;

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
        private SetupConfig _setup;
        private bool _matchEnded;
        private bool _matchEndedEventFired;
        private MatchResult? _matchResult;

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
        public bool MatchEnded => _matchEnded;
        public MatchResult? MatchResult => _matchResult;

        public event Action<GameState> OnMatchStarted;
        public event Action<GameState> OnTurnStarted;
        public event Action<MoveRecord> OnMoveApplied;
        public event Action<MatchResult> OnMatchEnded;
        public event Action<TrailHazardMove> OnTrailHazardMoved;

        public DemoBattleCheckpoint CaptureDemoCheckpoint()
        {
            string error = DemoCheckpointSupportError();
            if (error != null) throw new InvalidOperationException(error);
            return new DemoBattleCheckpoint
            {
                rulesetId = Rules.rulesetId, boardSize = Rules.boardSize,
                cellsA = State.StonesAByCell.ToArray(), cellsB = State.StonesBByCell.ToArray(),
                borneOffA = State.BorneOffA, borneOffB = State.BorneOffB, barA = State.BarA, barB = State.BarB,
                turnIndex = State.TurnIndex, turnsTakenA = State.TurnsTakenA, turnsTakenB = State.TurnsTakenB,
                currentPlayer = (int)State.CurrentPlayer,
                outcomeLabel = _currentOutcome.Label, outcomeDice = (int[])_currentOutcome.Dice.Clone(),
                remainingDice = _remainingDice.ToArray(), usedDice = _usedDice.ToArray(),
                selectedDieIndex = _selectedDieIndex ?? -1, headMovesUsed = _headMovesUsed, headMovesLimit = _maxHeadMovesThisTurn,
                trailHazardCell = State.TrailHazardCell, trailHazardYielded = State.TrailHazardYielded,
                finished = State.IsFinished, winner = State.Winner.HasValue ? (int)State.Winner.Value : -1,
                endReason = _matchResult?.Reason ?? MatchEndReason.None,
                orderedBagCursor = _bagA.SequentialCursor
            };
        }

        // No gameplay/presentation events are replayed. The owner rebinds its view after success.
        public bool TryRestoreDemoCheckpoint(DemoBattleCheckpoint checkpoint, out string error)
        {
            error = ValidateDemoCheckpoint(checkpoint);
            if (error != null) return false;
            State.RestoreDemoCheckpoint(checkpoint);
            _currentOutcome = State.CurrentOutcome;
            _remainingDice.Clear(); _remainingDice.AddRange(checkpoint.remainingDice);
            _usedDice.Clear(); _usedDice.AddRange(checkpoint.usedDice);
            _selectedDieIndex = checkpoint.selectedDieIndex < 0 ? null : checkpoint.selectedDieIndex;
            _headMovesUsed = checkpoint.headMovesUsed;
            _maxHeadMovesThisTurn = checkpoint.headMovesLimit;
            _bagA.RestoreSequentialCursor(checkpoint.orderedBagCursor);
            _matchEnded = checkpoint.finished;
            _matchEndedEventFired = checkpoint.finished;
            _matchResult = checkpoint.finished ? new MatchResult(State.Winner, checkpoint.endReason) : null;
            Log.Clear();
            return true;
        }

        private string DemoCheckpointSupportError()
        {
            if (State == null || Rules == null) return "BattleRunner is not initialized.";
            if (Rules.gameMode != GameMode.SoloTrail || Rules.allowReroll)
                return "Demo checkpoints require SoloTrail without rerolls.";
            if (_bagA == null || _bagA.DrawMode != DiceBagDrawMode.Sequential || _bagA.TotalCount == 0)
                return "Demo checkpoints require a nonempty sequential step bag.";
            return null;
        }

        private string ValidateDemoCheckpoint(DemoBattleCheckpoint c)
        {
            string unsupported = DemoCheckpointSupportError();
            if (unsupported != null) return unsupported;
            if (c == null || c.schemaVersion != DemoBattleCheckpoint.CurrentSchemaVersion)
                return "Demo checkpoint schema is unsupported.";
            if (!string.Equals(c.rulesetId, Rules.rulesetId, StringComparison.Ordinal) || c.boardSize != Rules.boardSize)
                return "Demo checkpoint rules do not match this level.";
            if (c.cellsA == null || c.cellsB == null || c.cellsA.Length != Rules.boardSize || c.cellsB.Length != Rules.boardSize ||
                c.barA != 0 || c.barB != 0 || c.borneOffA < 0 || c.borneOffA > Rules.totalStonesPerPlayer || c.borneOffB != 0)
                return "Demo checkpoint board counts are invalid.";
            long totalA = c.borneOffA;
            for (int cell = 0; cell < Rules.boardSize; cell++)
            {
                if (c.cellsA[cell] < 0 || c.cellsB[cell] < 0 ||
                    (Rules.blockIfOpponentAnyStone && c.cellsA[cell] > 0 && c.cellsB[cell] > 0))
                    return "Demo checkpoint contains an invalid board position.";
                totalA += c.cellsA[cell];
                int expectedB = cell == c.trailHazardCell ? 1 : 0;
                if (c.cellsB[cell] != expectedB) return "Demo checkpoint hazard counts are inconsistent.";
            }
            if (totalA != Rules.totalStonesPerPlayer) return "Demo checkpoint team count is inconsistent.";
            if (c.currentPlayer != (int)PlayerId.A || c.turnIndex < 0 || c.turnIndex > Rules.maxTurns ||
                c.turnsTakenA != c.turnIndex || c.turnsTakenB != 0)
                return "Demo checkpoint turn state is invalid.";
            if (!c.finished && (c.winner != -1 || c.endReason != MatchEndReason.None ||
                c.borneOffA == Rules.totalStonesPerPlayer || c.turnIndex >= Rules.maxTurns))
                return "Demo checkpoint unfinished result is inconsistent.";
            if (c.finished && !((c.endReason == MatchEndReason.Win && c.winner == (int)PlayerId.A &&
                    c.borneOffA == Rules.totalStonesPerPlayer && c.turnIndex < Rules.maxTurns) ||
                (c.endReason == MatchEndReason.Timeout && c.winner == -1 && c.turnIndex == Rules.maxTurns &&
                    c.borneOffA < Rules.totalStonesPerPlayer)))
                return "Demo checkpoint finished result is inconsistent.";

            string hazardError = ValidateDemoHazard(c);
            if (hazardError != null) return hazardError;
            int drawnTurns = c.finished && c.endReason == MatchEndReason.Timeout ? c.turnIndex : c.turnIndex + 1;
            int expectedCursor = ((drawnTurns - 1) % _bagA.TotalCount) + 1;
            if (c.orderedBagCursor != expectedCursor || !_bagA.TryGetSequentialOutcome(c.orderedBagCursor, out var expectedOutcome) ||
                !string.Equals(c.outcomeLabel, expectedOutcome.Label, StringComparison.Ordinal) ||
                !EqualSteps(c.outcomeDice, expectedOutcome.Dice))
                return "Demo checkpoint step bag position is inconsistent.";
            if (c.remainingDice == null || c.usedDice == null || c.usedDice.Length > Rules.actionsPerTurn ||
                (c.finished && c.endReason == MatchEndReason.Win && c.usedDice.Length == 0))
                return "Demo checkpoint remaining actions are invalid.";
            bool sequential = Rules.soloTrailStepOfferMode == SoloTrailStepOfferMode.Sequential;
            if (!sequential && c.usedDice.Length > 1) return "Demo checkpoint single-action turn is inconsistent.";
            var unused = new List<int>(c.outcomeDice);
            foreach (int step in c.usedDice)
                if (!unused.Remove(step)) return "Demo checkpoint consumed steps are inconsistent.";
            bool clearedOffer = c.usedDice.Length > 0 && (!sequential || c.usedDice.Length >= Rules.actionsPerTurn);
            if (clearedOffer ? c.remainingDice.Length != 0 : !SameStepMultiset(c.remainingDice, unused))
                return "Demo checkpoint available steps are inconsistent.";
            if (!IsStepSubsequence(c.remainingDice, c.outcomeDice) ||
                (!c.finished && (clearedOffer || c.remainingDice.Length == 0)) ||
                c.selectedDieIndex < -1 || c.selectedDieIndex >= c.remainingDice.Length ||
                (c.remainingDice.Length == 0 && c.selectedDieIndex != -1))
                return "Demo checkpoint step selection is invalid.";
            int outcomeTurn = c.finished && c.endReason == MatchEndReason.Timeout ? c.turnIndex - 1 : c.turnIndex;
            int expectedHeadLimit = CalculateHeadMoveLimit(outcomeTurn, expectedOutcome);
            if (c.headMovesLimit != expectedHeadLimit || c.headMovesUsed < 0 ||
                c.headMovesUsed > c.usedDice.Length || c.headMovesUsed > c.headMovesLimit)
                return "Demo checkpoint head action limit is inconsistent.";
            return null;
        }

        private string ValidateDemoHazard(DemoBattleCheckpoint c)
        {
            if (c.trailHazardCell < -1 || c.trailHazardCell >= Rules.boardSize)
                return "Demo checkpoint hazard cell is invalid.";
            switch (Rules.soloTrailHazard)
            {
                case SoloTrailHazard.None:
                    return c.trailHazardCell == -1 && !c.trailHazardYielded ? null : "This level has no trail hazard.";
                case SoloTrailHazard.Bark:
                    return c.trailHazardCell == Rules.soloTrailHazardStartCell && !c.trailHazardYielded
                        ? null : "The stationary trail hazard has moved.";
                case SoloTrailHazard.Ryzh:
                    int completedHazardTurns = c.turnIndex - (c.finished && c.endReason == MatchEndReason.Timeout ? 1 : 0);
                    int expected = Rules.soloTrailHazardStartCell + completedHazardTurns;
                    bool valid = c.trailHazardCell >= 0
                        ? c.trailHazardCell == expected && !c.trailHazardYielded
                        : c.trailHazardYielded ? completedHazardTurns > 0 : expected >= Rules.boardSize;
                    return valid ? null : "Demo checkpoint moving hazard state is inconsistent.";
                default: return "Demo checkpoint hazard rule is unsupported.";
            }
        }

        private static bool EqualSteps(int[] actual, int[] expected)
        {
            if (actual == null || actual.Length != expected.Length) return false;
            for (int i = 0; i < actual.Length; i++) if (actual[i] != expected[i]) return false;
            return true;
        }

        private static bool SameStepMultiset(int[] actual, List<int> expected)
        {
            if (actual.Length != expected.Count) return false;
            var remaining = new List<int>(expected);
            foreach (int step in actual) if (!remaining.Remove(step)) return false;
            return true;
        }

        private static bool IsStepSubsequence(int[] actual, int[] offered)
        {
            int cursor = 0;
            foreach (int step in offered) if (cursor < actual.Length && step == actual[cursor]) cursor++;
            return cursor == actual.Length;
        }

        public void Init(RulesetConfig rules, DiceBagConfigData bagA, DiceBagConfigData bagB, int seed, SetupConfig setup = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Rules.Validate();
            _seed = seed;
            _setup = setup;

            _bagA = bagA == null ? null : new DiceBagRuntime(bagA, _seed + 1000);
            _bagB = bagB == null ? null : new DiceBagRuntime(bagB, _seed + 2000);

            State = new GameState(Rules);
            ApplySetupPresetIfAvailable();
            State.ResetTrailHazard();
            CreateBots();
            Log.Clear();
            _matchEnded = false;
            _matchEndedEventFired = false;
            _matchResult = null;
            BeginTurn();
            LogHomeZonesOnce();

            OnMatchStarted?.Invoke(State);
        }

        public void Reset()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            State.Reset();
            ApplySetupPresetIfAvailable();
            State.ResetTrailHazard();
            CreateBots();
            Log.Clear();
            _bagA?.Reset();
            _bagB?.Reset();
            _matchEnded = false;
            _matchEndedEventFired = false;
            _matchResult = null;
            BeginTurn();
            LogHomeZonesOnce();

            OnMatchStarted?.Invoke(State);
        }

        private void ApplySetupPresetIfAvailable()
        {
            if (_setup == null || _setup.UnitPlacements == null || _setup.UnitPlacements.Count == 0)
                return;

            if (_setup.BoardSize != Rules.boardSize)
            {
                Debug.LogWarning($"[BattleRunner] Setup board size ({_setup.BoardSize}) does not match rules board size ({Rules.boardSize}). Using rules board size.");
            }

            Debug.Log($"Applying SetupPreset: {_setup.SetupId} ({_setup.DisplayName}), placements={_setup.UnitPlacements.Count}");

            ClearInitialStones();

            int placedA = 0;
            int placedB = 0;
            int boardSize = Rules.boardSize;

            for (int i = 0; i < _setup.UnitPlacements.Count; i++)
            {
                var placement = _setup.UnitPlacements[i];
                if (placement.count <= 0)
                {
                    Debug.LogWarning($"[BattleRunner] Skipping setup placement at index {i}: count must be > 0.");
                    continue;
                }

                if (placement.cellIndex < 0 || placement.cellIndex >= boardSize)
                {
                    Debug.LogWarning($"[BattleRunner] Skipping setup placement at index {i}: cellIndex {placement.cellIndex} is outside [0..{boardSize - 1}].");
                    continue;
                }

                for (int c = 0; c < placement.count; c++)
                    State.AddStoneToCell(placement.player, placement.cellIndex);

                if (placement.player == PlayerId.A)
                    placedA += placement.count;
                else
                    placedB += placement.count;
            }

            Debug.Log($"[BattleRunner] Setup counts after placement: A={placedA}, B={placedB}");

            if (placedA != Rules.totalStonesPerPlayer ||
                (Rules.gameMode != GameMode.SoloTrail && placedB != Rules.totalStonesPerPlayer))
            {
                Debug.LogWarning($"[BattleRunner] Setup stone count mismatch. Expected per player={Rules.totalStonesPerPlayer}, actual A={placedA}, B={placedB}.");
            }
        }

        private void ClearInitialStones()
        {
            for (int cell = 0; cell < Rules.boardSize; cell++)
            {
                int countA = State.GetStonesAt(PlayerId.A, cell);
                for (int i = 0; i < countA; i++)
                    State.RemoveStoneFromCell(PlayerId.A, cell);

                int countB = State.GetStonesAt(PlayerId.B, cell);
                for (int i = 0; i < countB; i++)
                    State.RemoveStoneFromCell(PlayerId.B, cell);
            }
        }

        public bool Tick()
        {
            if (State == null)
                throw new InvalidOperationException("BattleRunner is not initialized. Call Init first.");

            if (_matchEnded || State.IsFinished)
                return false;

            // The short trail has one human side; never let the bot play it.
            if (Rules.gameMode == GameMode.SoloTrail)
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
            if (State == null || _matchEnded || State.IsFinished)
                return false;

            int? selectedIndex = ResolveSelectedDieIndex();
            if (!selectedIndex.HasValue)
                return false;

            int dieValue = _remainingDice[selectedIndex.Value];
            var legal = MoveGenerator.GenerateLegalMoves(State, dieValue, _headMovesUsed, _maxHeadMovesThisTurn);
            if (legal.Count == 0)
            {
                if (Rules.gameMode == GameMode.SoloTrail)
                    return false;
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
            if (State == null || _matchEnded || State.IsFinished)
                return false;
            if (index < 0 || index >= _remainingDice.Count)
                return false;

            _selectedDieIndex = index;
            return true;
        }

        public bool EnsureSelectedDie()
        {
            if (State == null || _matchEnded || State.IsFinished)
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

        public bool RerollCurrentTurnOutcome()
        {
            if (State == null || _matchEnded || State.IsFinished)
                return false;

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
            return true;
        }

        public bool EndTurnIfNoMoves()
        {
            if (State == null || _matchEnded || State.IsFinished)
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
            => CalculateHeadMoveLimit(State.GetTurnsTaken(player), outcome);

        private int CalculateHeadMoveLimit(int turnsTaken, DiceOutcomeResult outcome)
        {
            if (Rules.headRules == null || !Rules.headRules.restrictHeadMoves)
                return int.MaxValue;

            if (turnsTaken == 0)
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

            if (!_matchEnded && result == ApplyResult.Finished)
            {
                var winner = State.Winner ?? State.CurrentPlayer;
                CompleteMatch(winner, MatchEndReason.Win);
            }

            if (result == ApplyResult.Ok || result == ApplyResult.Finished)
            {
                ConsumeSelectedDie(move.PipUsed);
                if (Rules.gameMode == GameMode.SoloTrail &&
                    (Rules.soloTrailStepOfferMode != SoloTrailStepOfferMode.Sequential || _usedDice.Count >= Rules.actionsPerTurn))
                {
                    _remainingDice.Clear();
                    _selectedDieIndex = null;
                }
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

            if (_matchEnded || State.IsFinished)
            {
                if (!_matchEnded && State.IsFinished && State.Winner.HasValue)
                    CompleteMatch(State.Winner.Value, MatchEndReason.Win);

                FireMatchEndedIfNeeded();
                return true;
            }

            if (_remainingDice.Count == 0)
            {
                EndTurn();
                return true;
            }

            if (Rules.gameMode != GameMode.SoloTrail && !HasAnyLegalMove())
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
            if (_matchEnded || State.IsFinished)
                return;

            // The limit counts completed turns, including passes and empty rolls.
            // Keep the final turn's remaining dice playable before evaluating it.
            PlayerId player = State.CurrentPlayer;
            int completedTurn = State.TurnIndex;
            State.AdvanceTurn();
            if (State.TurnIndex >= Rules.maxTurns)
            {
                CompleteMatch(DecideWinnerOnTimeout(State), MatchEndReason.Timeout);
                Log.Add(new MoveRecord(completedTurn, player, null, null, null, null,
                    _currentOutcome, _remainingDice.ToArray(), ApplyResult.Finished,
                    MatchEndReason.Timeout, State.Winner));
                FireMatchEndedIfNeeded();
                return;
            }

            var hazardMove = State.AdvanceTrailHazard();
            if (hazardMove.HasValue) OnTrailHazardMoved?.Invoke(hazardMove.Value);
            BeginTurn();
        }

        private static PlayerId? DecideWinnerOnTimeout(GameState state)
        {
            if (state.Rules.gameMode == GameMode.SoloTrail)
                return null;
            int offA = state.GetBorneOff(PlayerId.A);
            int offB = state.GetBorneOff(PlayerId.B);
            if (offA != offB)
                return offA > offB ? PlayerId.A : PlayerId.B;

            int distanceA = GetRemainingDistance(state, PlayerId.A);
            int distanceB = GetRemainingDistance(state, PlayerId.B);
            if (distanceA != distanceB)
                return distanceA < distanceB ? PlayerId.A : PlayerId.B;

            return null;
        }

        private static int GetRemainingDistance(GameState state, PlayerId player)
        {
            // A bar token must enter the board before completing the full route.
            int distance = state.GetBarCount(player) * (state.Rules.boardSize + 1);
            for (int cell = 0; cell < state.Rules.boardSize; cell++)
                distance += state.GetStonesAt(player, cell) * BoardPathRules.PipsToBearOff(state.Rules, player, cell);
            return distance;
        }

        private void CompleteMatch(PlayerId? winner, MatchEndReason reason)
        {
            if (_matchEnded)
                return;

            State.Finish(winner);
            _matchEnded = true;
            _matchResult = new MatchResult(winner, reason);
        }

        private void FireMatchEndedIfNeeded()
        {
            if (_matchEndedEventFired || !_matchResult.HasValue)
                return;

            _matchEndedEventFired = true;
            OnMatchEnded?.Invoke(_matchResult.Value);
        }

        private (int? FromCell, int? ToCell) DescribeMoveBeforeApply(Move move)
        {
            if (State == null) return (null, null);

            if (move.Kind == MoveKind.EnterFromBar)
            {
                var entryCells = MoveGenerator.GetEntryCellsForPlayer(State.Rules, State.CurrentPlayer);
                if (move.PipUsed > 0 && move.PipUsed <= entryCells.Count)
                    return (null, entryCells[move.PipUsed - 1]);
                return (null, null);
            }

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
