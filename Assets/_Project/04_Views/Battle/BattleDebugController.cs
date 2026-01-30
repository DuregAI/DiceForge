using System.Collections;
using System.Collections.Generic;
using Diceforge.Core;
using Diceforge.Presets;
using UnityEngine;

namespace Diceforge.View
{
    public sealed class BattleDebugController : MonoBehaviour
    {
        public enum ControlMode : byte
        {
            Bot = 0,
            Human = 1
        }

        [Header("Rules")]
        [SerializeField] private RulesetPreset rulesetPreset;

        [Header("Controls")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool runContinuously = true;
        [SerializeField] private bool stepMode = false;
        [SerializeField] private float secondsPerTurn = 0.5f;
        [SerializeField] private bool verboseLog = true;

        [Header("View")]
        [SerializeField] private BoardDebugView boardView;

        [Header("Control Modes")]
        [SerializeField] private ControlMode controlModeA = ControlMode.Human;
        [SerializeField] private ControlMode controlModeB = ControlMode.Bot;

        [Header("UI")]
        [SerializeField] private DebugHudUITK hud;

        private BattleRunner _runner;
        private RulesetConfig _rules;
        private bool _isRunning;
        private float _elapsed;
        private bool _waitingForFromCell;
        private bool _isInitialized;
        private bool _hasExternalPreset;
        private GameModePreset _externalPreset;
        private Coroutine _autoStartRoutine;

        private void Awake()
        {
            if (boardView == null)
                boardView = GetComponent<BoardDebugView>();

            _runner = new BattleRunner();
            _runner.OnMatchStarted += HandleMatchStarted;
            _runner.OnTurnStarted += HandleTurnStarted;
            _runner.OnMoveApplied += HandleMoveApplied;
            _runner.OnMatchEnded += HandleMatchEnded;

            if (boardView != null)
                boardView.OnCellClicked += HandleCellClicked;

            if (hud == null)
                hud = GetComponentInChildren<DebugHudUITK>(true);

            RegisterHudCallbacks();
        }

        private void OnDestroy()
        {
            if (_runner == null) return;

            _runner.OnMatchStarted -= HandleMatchStarted;
            _runner.OnTurnStarted -= HandleTurnStarted;
            _runner.OnMoveApplied -= HandleMoveApplied;
            _runner.OnMatchEnded -= HandleMatchEnded;

            if (boardView != null)
                boardView.OnCellClicked -= HandleCellClicked;

            UnregisterHudCallbacks();
        }

        private void Start()
        {
            _autoStartRoutine = StartCoroutine(DeferredInitialize());

            SyncHudState();
        }

        private void Update()
        {
            if (!_isRunning || stepMode || !runContinuously)
                return;

            if (IsHumanTurn())
                return;

            float interval = secondsPerTurn <= 0f ? 0f : secondsPerTurn;
            _elapsed += Time.deltaTime;

            if (interval == 0f)
            {
                TickOnce();
                return;
            }

            if (_elapsed >= interval)
            {
                _elapsed = 0f;
                TickOnce();
            }
        }

        public void StartFromPreset(GameModePreset preset)
        {
            if (preset == null)
            {
                Debug.LogWarning("[Diceforge] Null GameModePreset provided. Aborting external start.");
                return;
            }

            if (_isInitialized && _isRunning)
            {
                Debug.LogWarning("[Diceforge] Match already running. External preset ignored.");
                return;
            }

            _hasExternalPreset = true;
            _externalPreset = preset;
            InitializeMatchFromPreset(preset);
            if (autoStart && !_isRunning)
                StartMatch();
        }

        private IEnumerator DeferredInitialize()
        {
            yield return null;

            if (_hasExternalPreset && _externalPreset != null)
            {
                if (!_isInitialized)
                    InitializeMatchFromPreset(_externalPreset);

                if (autoStart && !_isRunning)
                    StartMatch();

                yield break;
            }

            InitializeMatchFromRuleset(rulesetPreset);
            if (autoStart && !_isRunning)
                StartMatch();
        }

        private void InitializeMatchFromRuleset(RulesetPreset preset)
        {
            if (preset == null)
            {
                Debug.LogError("[Diceforge] Missing RulesetPreset. Aborting match bootstrap.");
                enabled = false;
                return;
            }

            _rules = RulesetConfig.FromPreset(preset);
            var bagA = BuildBagConfig(_rules.diceBagA);
            var bagB = BuildBagConfig(_rules.diceBagB);
            _runner.Init(_rules, bagA, bagB, _rules.randomSeed);
            _isInitialized = true;
        }

        private void InitializeMatchFromPreset(GameModePreset preset)
        {
            if (preset.rulesetPreset == null)
            {
                Debug.LogError("[Diceforge] Missing RulesetPreset on GameModePreset. Aborting match bootstrap.");
                enabled = false;
                return;
            }

            _rules = RulesetConfig.FromPreset(preset.rulesetPreset);
            var bagA = BuildBagConfig(preset.diceBagA);
            var bagB = BuildBagConfig(preset.diceBagB);
            _runner.Init(_rules, bagA, bagB, _rules.randomSeed);
            _isInitialized = true;
        }

        [ContextMenu("Start")]
        public void StartMatch()
        {
            if (!_isInitialized)
                InitializeMatchFromRuleset(rulesetPreset);

            _isRunning = true;
            SyncHudState();
        }

        [ContextMenu("Stop")]
        public void StopMatch()
        {
            _isRunning = false;
            SyncHudState();
        }

        [ContextMenu("Step")]
        public void StepOnce()
        {
            TickOnce();
        }

        [ContextMenu("Restart")]
        public void RestartMatch()
        {
            bool wasRunning = _isRunning;
            _elapsed = 0f;
            _runner.Reset();
            _isRunning = wasRunning;
            SyncHudState();
            UpdateUI();
        }

        private void TickOnce()
        {
            if (_runner.State == null || _runner.State.IsFinished)
                return;

            if (IsHumanTurn())
                return;

            _runner.Tick();
        }

        private void HandleMatchStarted(GameState state)
        {
            boardView?.HandleMatchStarted(state, _runner.Log);
            UpdateUI();
            if (verboseLog)
                Debug.Log("[Diceforge] Match start: " + state.DebugSnapshot());
        }

        private void HandleTurnStarted(GameState state)
        {
            UpdateUI();
        }

        private void HandleMoveApplied(MoveRecord record)
        {
            boardView?.HandleMoveApplied(record);
            UpdateUI();
            if (!verboseLog) return;

            string moveText = record.Move?.ToString() ?? "NoMove";
            string pipText = record.PipUsed.HasValue ? record.PipUsed.Value.ToString() : "-";
            Debug.Log($"[Diceforge] {record.PlayerId} -> {moveText}  Outcome={record.Outcome}  Pip={pipText}  T{record.TurnIndex}");
        }

        private void HandleMatchEnded(GameState state)
        {
            boardView?.HandleMatchEnded(state);
            if (verboseLog)
                Debug.Log($"[Diceforge] Match end. Winner: {state.Winner}  Turns: {state.TurnIndex}");
            UpdateUI();
        }

        private bool IsHumanTurn()
        {
            if (_runner?.State == null) return false;

            var current = _runner.State.CurrentPlayer;
            if (current == PlayerId.A)
                return controlModeA == ControlMode.Human;
            return controlModeB == ControlMode.Human;
        }

        private void HandleMoveClicked()
        {
            if (!IsHumanTurn() || _runner.State == null)
                return;

            if (_runner.IsWaitingForDieSelection)
                return;

            if (!HasLegalMove())
                return;

            _waitingForFromCell = true;
            boardView?.SetCellSelectionEnabled(true);
        }

        private void HandleCellClicked(int cellIndex)
        {
            if (!_waitingForFromCell || _runner?.State == null)
                return;

            var move = SelectMoveForCell(cellIndex);
            if (!move.HasValue)
                return;

            _waitingForFromCell = false;
            boardView?.SetCellSelectionEnabled(false);
            ApplyHumanMove(move.Value);
        }

        private void HandleDieSelected(int index)
        {
            if (!IsHumanTurn() || _runner?.State == null)
                return;

            if (!_runner.SelectDieIndex(index))
                return;

            _waitingForFromCell = false;
            boardView?.SetCellSelectionEnabled(false);
            UpdateUI();
        }

        private void HandleEnterClicked()
        {
        }

        private void HandlePlaceClicked()
        {
        }

        private void ApplyHumanMove(Move move)
        {
            if (_runner == null || _runner.State == null || _runner.State.IsFinished)
                return;

            bool applied = _runner.TryApplyHumanMove(move);
            if (!applied && verboseLog)
                Debug.Log($"[Diceforge] Illegal human move: {move}");
        }

        private void UpdateUI()
        {
            if (_runner?.State == null)
            {
                RefreshTurnUIAndInput();
                return;
            }

            var state = _runner.State;
            string lastMove = BuildLastMoveText();

            hud?.SetTurn(state.TurnIndex);
            hud?.SetCurrentPlayer(state.CurrentPlayer.ToString());
            hud?.SetDiceOutcome(_runner.CurrentOutcome, _runner.RemainingDice, _runner.UsedDice);
            hud?.SetBagStatus(_runner.CurrentBagRemaining, _runner.CurrentBagTotal);
            hud?.SetLastMove(lastMove);
            hud?.SetWinner(state.IsFinished ? state.Winner?.ToString() ?? "-" : "-");
            hud?.SetPlayerStatsA($"A: Off {state.BorneOffA}");
            hud?.SetPlayerStatsB($"B: Off {state.BorneOffB}");
            hud?.SetHeadMovesInfo(_runner.HeadMovesUsed, _runner.HeadMovesLimit);
            hud?.SetStatus(BuildStatusText());
            RefreshTurnUIAndInput();
        }

        private void RefreshTurnUIAndInput()
        {
            if (_runner?.State == null)
            {
                hud?.SetHumanControlsEnabled(false);
                hud?.SetMoveEnabled(false);
                hud?.SetEnterEnabled(false);
                hud?.SetPlaceEnabled(false);
                hud?.SetDiceButtons(null, null, false);
                _waitingForFromCell = false;
                boardView?.SetCellSelectionEnabled(false);
                boardView?.SetHighlightedCells(null);
                return;
            }

            var state = _runner.State;
            bool humanTurn = IsHumanTurn() && !state.IsFinished;
            if (humanTurn && _runner.EndTurnIfNoMoves())
                return;

            bool needsDieSelection = humanTurn && _runner.IsWaitingForDieSelection;
            bool canMove = humanTurn && !needsDieSelection && HasLegalMove();

            hud?.SetHumanControlsEnabled(humanTurn);
            hud?.SetMoveEnabled(canMove);
            hud?.SetEnterEnabled(false);
            hud?.SetPlaceEnabled(false);
            hud?.SetDiceButtons(_runner.RemainingDice, _runner.SelectedDieIndex, humanTurn);

            if (!canMove)
                _waitingForFromCell = false;

            boardView?.SetCellSelectionEnabled(_waitingForFromCell);
            UpdateHighlightedCells();
        }

        private void RegisterHudCallbacks()
        {
            if (hud == null)
                return;

            hud.OnStartStop += HandleStartStopChanged;
            hud.OnStep += StepOnce;
            hud.OnRestart += RestartMatch;
            hud.OnMove += HandleMoveClicked;
            hud.OnEnter += HandleEnterClicked;
            hud.OnPlace += HandlePlaceClicked;
            hud.OnToggleAutoRun += HandleAutoRunChanged;
            hud.OnSpeedChanged += HandleSpeedChanged;
            hud.OnHumanAChanged += HandleHumanAChanged;
            hud.OnHumanBChanged += HandleHumanBChanged;
            hud.OnDieSelected += HandleDieSelected;
        }

        private void UnregisterHudCallbacks()
        {
            if (hud == null)
                return;

            hud.OnStartStop -= HandleStartStopChanged;
            hud.OnStep -= StepOnce;
            hud.OnRestart -= RestartMatch;
            hud.OnMove -= HandleMoveClicked;
            hud.OnEnter -= HandleEnterClicked;
            hud.OnPlace -= HandlePlaceClicked;
            hud.OnToggleAutoRun -= HandleAutoRunChanged;
            hud.OnSpeedChanged -= HandleSpeedChanged;
            hud.OnHumanAChanged -= HandleHumanAChanged;
            hud.OnHumanBChanged -= HandleHumanBChanged;
            hud.OnDieSelected -= HandleDieSelected;
        }

        private void HandleStartStopChanged(bool isRunning)
        {
            if (isRunning)
                StartMatch();
            else
                StopMatch();
        }

        private void HandleAutoRunChanged(bool autoRun)
        {
            runContinuously = autoRun;
            SyncHudState();
        }

        private void HandleSpeedChanged(float speed)
        {
            secondsPerTurn = Mathf.Max(0f, speed);
            SyncHudState();
        }

        private void HandleHumanAChanged(bool isHuman)
        {
            controlModeA = isHuman ? ControlMode.Human : ControlMode.Bot;
            UpdateUI();
        }

        private void HandleHumanBChanged(bool isHuman)
        {
            controlModeB = isHuman ? ControlMode.Human : ControlMode.Bot;
            UpdateUI();
        }

        private void SyncHudState()
        {
            if (hud == null)
                return;

            hud.SetRunToggle(_isRunning);
            hud.SetAutoRunToggle(runContinuously);
            hud.SetSpeed(secondsPerTurn);
            hud.SetHumanAToggle(controlModeA == ControlMode.Human);
            hud.SetHumanBToggle(controlModeB == ControlMode.Human);
            UpdateUI();
        }

        private DiceBagConfigData BuildBagConfig(DiceBagDefinition definition)
        {
            int dieMin = _rules?.dieMin ?? 1;
            int dieMax = _rules?.dieMax ?? 6;
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
                if (definition == null && verboseLog)
                    Debug.LogWarning("[Diceforge] Missing DiceBagDefinition, using fallback outcome.");
            }

            return new DiceBagConfigData(drawMode, outcomes);
        }

        private string BuildStatusText()
        {
            if (_runner?.State == null)
                return "Status: -";

            if (_runner.State.IsFinished)
                return $"Status: Finished ({_runner.State.Winner})";

            if (IsHumanTurn())
            {
                if (_runner.IsWaitingForDieSelection)
                    return "Status: Waiting: choose die";
                return "Status: Waiting: choose move";
            }

            string running = _isRunning ? "Running" : "Paused";
            string mode = runContinuously ? "Auto" : "Manual";
            return $"Status: {running} ({mode})";
        }

        private string BuildLastMoveText()
        {
            var last = _runner?.Log.Last;
            if (!last.HasValue || !last.Value.Move.HasValue)
                return "None";

            var record = last.Value;
            var move = record.Move.Value;
            string player = record.PlayerId.ToString();
            string text;

            if (move.Kind == MoveKind.MoveStone)
            {
                string fromText = record.FromCell.HasValue ? record.FromCell.Value.ToString() : "-";
                string toText = record.ToCell.HasValue ? record.ToCell.Value.ToString() : "-";
                string pipText = record.PipUsed.HasValue ? record.PipUsed.Value.ToString() : "-";
                text = $"{player} {fromText}→{toText} (Pip {pipText})";
            }
            else
            {
                string fromText = record.FromCell.HasValue ? record.FromCell.Value.ToString() : "-";
                string pipText = record.PipUsed.HasValue ? record.PipUsed.Value.ToString() : "-";
                text = $"{player} BearOff {fromText} (Pip {pipText})";
            }

            if (record.ApplyResult == ApplyResult.Illegal)
                text += " [Illegal]";

            return text;
        }

        private void UpdateHighlightedCells()
        {
            if (_runner?.State == null || boardView == null)
            {
                boardView?.SetHighlightedCells(null);
                return;
            }

            if (!IsHumanTurn() || _runner.State.IsFinished)
            {
                boardView.SetHighlightedCells(null);
                return;
            }

            int? selectedIndex = _runner.SelectedDieIndex;
            if (!selectedIndex.HasValue && _runner.RemainingDice.Count == 1)
                selectedIndex = 0;

            if (!selectedIndex.HasValue)
            {
                boardView.SetHighlightedCells(null);
                return;
            }

            int dieValue = _runner.RemainingDice[selectedIndex.Value];
            var legal = MoveGenerator.GenerateLegalMoves(
                _runner.State,
                dieValue,
                _runner.HeadMovesUsed,
                _runner.HeadMovesLimit);

            var cells = new HashSet<int>();
            foreach (var move in legal)
                cells.Add(move.FromCell);

            boardView.SetHighlightedCells(cells);
        }

        private bool HasLegalMove()
        {
            if (_runner?.State == null) return false;
            return _runner.HasLegalMoveForSelectedDie();
        }

        private Move? SelectMoveForCell(int cellIndex)
        {
            if (_runner?.State == null) return null;

            int? selectedIndex = _runner.SelectedDieIndex;
            if (!selectedIndex.HasValue && _runner.RemainingDice.Count == 1)
                selectedIndex = 0;

            if (!selectedIndex.HasValue)
                return null;

            int dieValue = _runner.RemainingDice[selectedIndex.Value];
            var legal = MoveGenerator.GenerateLegalMoves(
                _runner.State,
                dieValue,
                _runner.HeadMovesUsed,
                _runner.HeadMovesLimit);

            Move? best = null;
            int bestPip = int.MaxValue;
            foreach (var move in legal)
            {
                if (move.FromCell != cellIndex) continue;
                if (move.PipUsed < bestPip)
                {
                    best = move;
                    bestPip = move.PipUsed;
                }
            }

            return best;
        }
    }
}
