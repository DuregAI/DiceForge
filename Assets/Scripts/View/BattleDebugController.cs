using Diceforge.Core;
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
        [SerializeField] private RulesetConfig rules = new RulesetConfig();

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
        private bool _isRunning;
        private float _elapsed;
        private bool _waitingForFromCell;

        private void Awake()
        {
            if (boardView == null)
                boardView = GetComponent<BoardDebugView>();

            _runner = new BattleRunner();
            _runner.OnMatchStarted += HandleMatchStarted;
            _runner.OnMoveApplied += HandleMoveApplied;
            _runner.OnMatchEnded += HandleMatchEnded;

            if (boardView != null)
                boardView.OnCellClicked += HandleCellClicked;

            if (hud == null)
                hud = GetComponentInChildren<DebugHudUITK>(true);

            RegisterHudCallbacks();

            InitializeMatch();
        }

        private void OnDestroy()
        {
            if (_runner == null) return;

            _runner.OnMatchStarted -= HandleMatchStarted;
            _runner.OnMoveApplied -= HandleMoveApplied;
            _runner.OnMatchEnded -= HandleMatchEnded;

            if (boardView != null)
                boardView.OnCellClicked -= HandleCellClicked;

            UnregisterHudCallbacks();
        }

        private void Start()
        {
            if (autoStart)
                StartMatch();

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

        private void InitializeMatch()
        {
            _runner.Init(rules, rules.randomSeed);
        }

        [ContextMenu("Start")]
        public void StartMatch()
        {
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

        private void HandleMoveApplied(MoveRecord record)
        {
            boardView?.HandleMoveApplied(record);
            UpdateUI();
            if (!verboseLog) return;

            string moveText = record.Move?.ToString() ?? "NoMove";
            Debug.Log($"[Diceforge] {record.PlayerId} -> {moveText}  Roll={record.Roll}  T{record.TurnIndex}");
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

            if (!HasLegalMoveOneStone())
                return;

            _waitingForFromCell = true;
            boardView?.SetCellSelectionEnabled(true);
        }

        private void HandlePlaceChipClicked()
        {
            HandleEnterClicked();
        }

        private void HandleCellClicked(int cellIndex)
        {
            if (!_waitingForFromCell || _runner?.State == null)
                return;

            var move = Move.MoveOneStone(cellIndex);
            if (!IsMoveLegal(move))
                return;

            _waitingForFromCell = false;
            boardView?.SetCellSelectionEnabled(false);
            ApplyHumanMove(move);
        }

        private void HandleEnterClicked()
        {
            if (!IsHumanTurn() || _runner?.State == null)
                return;

            var target = _runner.State.CurrentPlayer == PlayerId.A
                ? _runner.State.Rules.startCellA
                : _runner.State.Rules.startCellB;
            var move = Move.EnterFromHand(target);
            ApplyHumanMove(move);
        }

        private void HandlePlaceClicked()
        {
            HandlePlaceChipClicked();
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
            hud?.SetRoll(state.CurrentRoll);
            hud?.SetLastMove(lastMove);
            hud?.SetWinner(state.IsFinished ? state.Winner?.ToString() ?? "-" : "-");
            hud?.SetPlayerStatsA($"A: Hand {state.StonesInHandA}");
            hud?.SetPlayerStatsB($"B: Hand {state.StonesInHandB}");
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
                _waitingForFromCell = false;
                boardView?.SetCellSelectionEnabled(false);
                return;
            }

            var state = _runner.State;
            bool humanTurn = IsHumanTurn() && !state.IsFinished;
            bool canMove = humanTurn && HasLegalMoveOneStone();
            bool canEnter = humanTurn && HasLegalEnter();

            hud?.SetHumanControlsEnabled(humanTurn);
            hud?.SetMoveEnabled(canMove);
            hud?.SetEnterEnabled(canEnter);
            hud?.SetPlaceEnabled(canEnter);

            _waitingForFromCell = canMove;
            boardView?.SetCellSelectionEnabled(canMove);
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

        private string BuildStatusText()
        {
            if (_runner?.State == null)
                return "Status: -";

            if (_runner.State.IsFinished)
                return $"Status: Finished ({_runner.State.Winner})";

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

            if (move.Kind == MoveKind.MoveOneStone)
            {
                string fromText = record.FromCell.HasValue ? record.FromCell.Value.ToString() : "-";
                string toText = record.ToCell.HasValue ? record.ToCell.Value.ToString() : "-";
                string hitText = record.WasHit ? " hit" : string.Empty;
                text = $"{player} {fromText}→{toText} (Roll {record.Roll}){hitText}";
            }
            else
            {
                string toText = record.ToCell.HasValue ? record.ToCell.Value.ToString() : "-";
                string hitText = record.WasHit ? " hit" : string.Empty;
                text = $"{player} Enter {toText}{hitText}";
            }

            if (record.ApplyResult == ApplyResult.Illegal)
                text += " [Illegal]";

            return text;
        }

        private bool HasLegalMoveOneStone()
        {
            if (_runner?.State == null) return false;
            var legal = MoveGenerator.GenerateLegalMoves(_runner.State, _runner.CurrentRoll);
            foreach (var move in legal)
            {
                if (move.Kind == MoveKind.MoveOneStone)
                    return true;
            }
            return false;
        }

        private bool HasLegalEnter()
        {
            if (_runner?.State == null) return false;
            var legal = MoveGenerator.GenerateLegalMoves(_runner.State, _runner.CurrentRoll);
            foreach (var move in legal)
            {
                if (move.Kind == MoveKind.EnterFromHand)
                    return true;
            }
            return false;
        }

        private bool IsMoveLegal(Move move)
        {
            if (_runner?.State == null) return false;
            var legal = MoveGenerator.GenerateLegalMoves(_runner.State, _runner.CurrentRoll);
            return legal.Contains(move);
        }
    }
}
