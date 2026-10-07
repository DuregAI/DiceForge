using System.Collections;
using System.Collections.Generic;
using Diceforge.Core;
using Diceforge.Diagnostics;
using Diceforge.Progression;
using Diceforge.Presets;
using Diceforge.Map;
using Diceforge.MapSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

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
        [SerializeField] private BattleBoardViewController boardViewController;

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
        private string _lastHumanInputFeedback = string.Empty;
        private bool _isInitialized;
        private bool _hasExternalPreset;
        private GameModePreset _externalPreset;
        private Coroutine _autoStartRoutine;
        private bool _rerollUsedThisTurn;
        private bool _wasBoardAnimating;
        private string _pendingAnimatedTokenName;
        private bool _hasBattleResultTriggered;
        private int matchGeneration;
        private Coroutine resultPresentation;
        internal BattleRewardSession RewardSession { get; private set; }
        public bool HasPendingResultSave => RewardSession != null && RewardSession.HasPendingSave;
        public void RetryResultSave() { RewardSession?.Retry(); DemoNarrative?.RefreshSaveStatus(); }

        [Header("Debug")]
        [SerializeField] private bool logRerollInventory;

        [SerializeField] private PlayerId localPlayer = PlayerId.A;

        public event System.Action<MatchResult> OnMatchEnded;
        public event System.Action OnHumanTurnStarted;
        public event System.Action<MoveRecord> OnHumanMoveApplied;
        public event System.Action OnHumanRerollUsed;
        public event System.Action OnDemoStepSelected;
        public event System.Action<DemoInputRejection, string> OnDemoInputRejected;
        public event System.Action<string> OnDemoBlockedPreview;
        public event System.Action OnDemoRestarted;
        public Diceforge.UI.Dialogue.DemoNarrativeController DemoNarrative { get; private set; }
        public DemoCheckpointController DemoCheckpoint { get; private set; }
        internal void RestoreDemoSession(string operationId, string heroId)
        {
            RewardSession = new BattleRewardSession(_externalPreset.modeId, operationId);
            SelectedHeroId = heroId;
        }
        public PlayerId LocalPlayer => localPlayer;
        public bool IsMatchEnded => _hasBattleResultTriggered || (_runner != null && (_runner.MatchEnded || (_runner.State != null && _runner.State.IsFinished)));
        public DebugHudUITK Hud => hud;
        public int CurrentRollDiceCount => _runner?.CurrentOutcome.Dice?.Length ?? 0;
        public int TotalStonesPerPlayer => _runner?.State?.Rules.totalStonesPerPlayer ?? _rules?.totalStonesPerPlayer ?? 0;
        public int LocalPlayerBorneOffCount => _runner?.State?.GetBorneOff(localPlayer) ?? 0;
        public PlayerId CurrentPlayer => _runner?.State?.CurrentPlayer ?? localPlayer;
        public GameState PresentationState => _runner?.State;
        public bool PresentationCanInteract => _runner?.State != null && IsHumanTurn() && !IsMatchEnded && !IsBoardAnimating() && DemoNarrative?.IsModal != true;
        public bool PresentationIsAnimating => IsBoardAnimating();
        public bool PresentationIsHumanTurn => _runner?.State != null && IsHumanTurn() && !IsMatchEnded;
        public bool PresentationHasLegalMove => _runner?.State != null && !IsMatchEnded && HasLegalMove();
        private Coroutine selectionPresentation;
        public System.Collections.Generic.IReadOnlyList<int> PresentationDice => _runner?.RemainingDice;
        public int? PresentationSelectedDie => _runner?.SelectedDieIndex;
        public void SelectPresentationDie(int index) => HandleDieSelected(index);
        public void PresentationReroll() => HandleRerollClicked();
        public bool PresentationCanReroll => CanUseReroll() && !IsBoardAnimating();
        public Move? PreviewMove(int cell) => SelectMoveForCell(cell);
        public void PreviewPresentationCell(DioramaBoard geometry, int cell, string heroId = null)
        {
            var move = PreviewMove(cell);
            if (move.HasValue) { geometry.Preview(move, PresentationState, cell); return; }
            if (DemoLevel != null && PresentationSelectedDie.HasValue && PresentationState.GetStonesAt(PlayerId.A, cell) > 0)
            {
                int step = PresentationDice[PresentationSelectedDie.Value];
                if (cell + step < 8 && PresentationState.GetStonesAt(PlayerId.B, cell + step) > 0)
                {
                    geometry.PreviewBlocked(cell, step, PresentationState);
                    OnDemoBlockedPreview?.Invoke(heroId);
                    return;
                }
            }
            geometry.Preview(null, null, -1);
        }
        public void RefreshPresentation() => UpdateUI();
        public Diceforge.GameModes.DemoLevelDefinition DemoLevel => _externalPreset?.demoLevel;
        public string SelectedHeroId { get; private set; }
        public string DemoInputFeedback => _lastHumanInputFeedback;
        public bool IsPassingTrailTurn { get; private set; }
        public bool TryGetHero(string heroId, out int cell, out string tokenName, out bool exited)
        {
            cell = -1; tokenName = null; exited = false;
            return boardViewController != null && boardViewController.TryGetHero(heroId, out cell, out tokenName, out exited);
        }
        public void MovePresentationHero(string heroId)
        {
            if (DemoLevel == null || !PresentationCanInteract || DioramaHud.BlocksGameplay) return;
            if (!TryGetHero(heroId, out int cell, out string tokenName, out bool exited))
            { OnDemoInputRejected?.Invoke(DemoInputRejection.UnknownTarget, heroId); return; }
            if (exited) { OnDemoInputRejected?.Invoke(DemoInputRejection.HeroAlreadyExited, heroId); return; }
            if (cell < 0) { OnDemoInputRejected?.Invoke(DemoInputRejection.UnknownTarget, heroId); return; }
            HandleSelectedCell(cell, tokenName);
        }

        private void EnsureRunnerInitialized()
        {
            if (_runner != null)
                return;

            _runner = new BattleRunner();
            _runner.OnMatchStarted += HandleMatchStarted;
            _runner.OnTurnStarted += HandleTurnStarted;
            _runner.OnMoveApplied += HandleMoveApplied;
            _runner.OnMatchEnded += HandleMatchEnded;

            Debug.LogWarning("[BattleDebugController] BattleRunner was lazily initialized. Check scene execution order/references.", this);
        }

        private void EnsureBoardViewControllerResolved()
        {
            if (boardViewController == null)
                boardViewController = GetComponent<BattleBoardViewController>();

            if (boardViewController == null)
                boardViewController = FindAnyObjectByType<BattleBoardViewController>();
        }

        private void Awake()
        {
            if (boardView == null)
                boardView = GetComponent<BoardDebugView>();

            if (boardViewController == null)
                boardViewController = GetComponent<BattleBoardViewController>();

            EnsureRunnerInitialized();

            if (boardView != null)
                boardView.OnCellClicked += HandleCellClicked;

            if (hud == null)
                hud = GetComponentInChildren<DebugHudUITK>(true);

            RegisterHudCallbacks();
            ProfileService.ProfileChanged += HandleProfileChanged;
        }

        private void OnEnable()
        {
            ProfileService.ProfileChanged += HandleProfileChanged;
            RefreshRerollUI();
            LogRerollCount("OnEnable");
        }

        private void OnDisable()
        {
            ProfileService.ProfileChanged -= HandleProfileChanged;
            if (selectionPresentation != null) StopCoroutine(selectionPresentation);
            selectionPresentation = null;
            if (resultPresentation != null) StopCoroutine(resultPresentation);
            resultPresentation = null;
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.OnMatchStarted -= HandleMatchStarted;
                _runner.OnTurnStarted -= HandleTurnStarted;
                _runner.OnMoveApplied -= HandleMoveApplied;
                _runner.OnMatchEnded -= HandleMatchEnded;
            }

            if (boardView != null)
                boardView.OnCellClicked -= HandleCellClicked;

            UnregisterHudCallbacks();
            ProfileService.ProfileChanged -= HandleProfileChanged;
        }

        private void Start()
        {
            _autoStartRoutine = StartCoroutine(DeferredInitialize());

            SyncHudState();
            LogRerollCount("Start");
        }

        private void Update()
        {
            if (DioramaHud.BlocksGameplay) return;
            if (Diceforge.Transitions.ScreenTransition.IsBusy) return;
            bool isBoardAnimating = IsBoardAnimating();
            if (isBoardAnimating != _wasBoardAnimating)
            {
                _wasBoardAnimating = isBoardAnimating;
                if (_runner?.State != null)
                    UpdateUI();
            }

            HandleShortcutInput(isBoardAnimating);

            if (stepMode || !runContinuously || !_isInitialized)
                return;

            float interval = secondsPerTurn <= 0f ? 0f : secondsPerTurn;
            _elapsed += Time.deltaTime;

            if (_elapsed < interval)
                return;

            _elapsed = 0f;

            if (_runner?.State == null || _runner.State.IsFinished || _runner.MatchEnded)
                return;

            if (_runner.Rules.gameMode == GameMode.SoloTrail && !isBoardAnimating && !_runner.HasAnyLegalMove())
            {
                if (DemoLevel != null) selectionPresentation = StartCoroutine(PassTrailTurn());
                else _runner.EndTurnIfNoMoves();
                return;
            }

            if (_runner.State == null || _runner.State.IsFinished || _runner.MatchEnded || IsHumanTurn() || isBoardAnimating)
                return;

            ExecuteAutoTurn();
        }
        private IEnumerator PassTrailTurn()
        {
            int generation = matchGeneration;
            IsPassingTrailTurn = true;
            _lastHumanInputFeedback = "No steps available. Starting a new turn.";
            yield return new WaitForSeconds(DioramaBoard.ReducedMotion ? .2f : .8f);
            IsPassingTrailTurn = false;
            selectionPresentation = null;
            if (generation == matchGeneration && !_runner.MatchEnded)
            { _runner.EndTurnIfNoMoves(); DemoCheckpoint?.MarkDirty(); }
        }
        public void ConfigureBoardSelection(BoardLayout layout, Tilemap positionTilemap)
        {
            if (boardView == null)
                boardView = GetComponent<BoardDebugView>();

            boardView?.ConfigureSelectionSpace(layout, positionTilemap);
        }

        public void StartFromPreset(GameModePreset preset)
        {
            EnsureRunnerInitialized();
            EnsureBoardViewControllerResolved();
            if (preset == null)
                throw new System.InvalidOperationException("[BattleDebugController] StartFromPreset failed: preset is null.");

            if (_isInitialized && _isRunning)
                throw new System.InvalidOperationException($"[BattleDebugController] StartFromPreset failed: match already running. preset='{preset.name}' modeId='{preset.modeId}'.");

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

            throw new System.InvalidOperationException("[BattleDebugController] Strict battle pipeline requires StartFromPreset(...) before DeferredInitialize. Ensure battle entry uses BattleLauncher + BattleSceneBootstrapper.");
        }

        private void ApplySelectedMapBoardSizeOverride(RulesetConfig rules)
        {
            BattleMapConfig selectedMap = BattleMapSelectionService.SelectedMap;
            if (selectedMap == null)
                throw new System.InvalidOperationException("[BattleDebugController] Strict pipeline failure: BattleMapSelectionService.SelectedMap is null.");

            if (selectedMap.boardLayout == null || selectedMap.boardLayout.cells == null)
                throw new System.InvalidOperationException($"[BattleDebugController] Strict pipeline failure: selected map '{selectedMap.name}' mapId='{selectedMap.mapId}' has no boardLayout cells.");

            if (selectedMap.boardLayout.cells.Count == 0)
                throw new System.InvalidOperationException($"[BattleDebugController] Strict pipeline failure: selected map '{selectedMap.name}' mapId='{selectedMap.mapId}' has zero cells.");

            int boardSize = selectedMap.boardLayout.cells.Count;
            rules.boardSize = boardSize;

            Debug.Assert(
                rules.boardSize == selectedMap.boardLayout.cells.Count,
                "Board size mismatch between layout and rules.");
        }

        private void InitializeMatchFromRuleset(RulesetPreset preset)
        {
            EnsureRunnerInitialized();
            EnsureBoardViewControllerResolved();
            if (preset == null)
                throw new System.InvalidOperationException("[BattleDebugController] InitializeMatchFromRuleset failed: RulesetPreset is null.");

            if (boardViewController == null)
                throw new System.InvalidOperationException("[BattleDebugController] InitializeMatchFromRuleset failed: BattleBoardViewController reference is missing.");

            _rules = RulesetConfig.FromPreset(preset);
            ApplySelectedMapBoardSizeOverride(_rules);
            var bagA = BuildBagConfig(_rules.diceBagA);
            var bagB = BuildBagConfig(_rules.diceBagB);
            _runner.Init(_rules, bagA, bagB, _rules.randomSeed);
            boardViewController.Bind(_runner);
            _isInitialized = true;
        }

        private void InitializeMatchFromPreset(GameModePreset preset)
        {
            EnsureRunnerInitialized();
            EnsureBoardViewControllerResolved();
            if (preset == null)
                throw new System.InvalidOperationException("[BattleDebugController] InitializeMatchFromPreset failed: GameModePreset is null.");

            if (preset.rulesetPreset == null)
                throw new System.InvalidOperationException($"[BattleDebugController] InitializeMatchFromPreset failed: RulesetPreset missing on preset '{preset.name}' modeId='{preset.modeId}'.");

            if (preset.setupPreset == null)
                throw new System.InvalidOperationException($"[BattleDebugController] InitializeMatchFromPreset failed: SetupPreset missing on preset '{preset.name}' modeId='{preset.modeId}'.");

            if (boardViewController == null)
                throw new System.InvalidOperationException("[BattleDebugController] InitializeMatchFromPreset failed: BattleBoardViewController reference is missing.");

            _rules = RulesetConfig.FromPreset(preset.rulesetPreset);
            ApplySelectedMapBoardSizeOverride(_rules);
            preset.demoLevel?.Validate(_rules);
            boardViewController.ConfigureDemo(preset.demoLevel);
            if (_rules.gameMode == GameMode.SoloTrail)
            {
                localPlayer = PlayerId.A;
                controlModeA = ControlMode.Human;
            }
            var bagA = BuildBagConfig(preset.diceBagA);
            var bagB = BuildBagConfig(preset.diceBagB);
            _runner.Init(_rules, bagA, bagB, _rules.randomSeed, SetupConfig.FromPreset(preset.setupPreset));
            if (preset.demoLevel != null)
            {
                DemoCheckpoint = gameObject.AddComponent<DemoCheckpointController>();
                DemoCheckpoint.Configure(this, _runner, preset);
            }
            boardViewController.Bind(_runner);
            if (DemoCheckpoint?.Resumed == true) boardViewController.RestoreDemoHeroes(DemoCheckpoint.Restored.heroes);
            _isInitialized = true;
            if (preset.demoLevel != null)
            {
                DemoNarrative = gameObject.AddComponent<Diceforge.UI.Dialogue.DemoNarrativeController>();
                DemoNarrative.Configure(this);
                DemoCheckpoint.BeginWatching();
                if (DemoCheckpoint.Resumed && _runner.MatchResult.HasValue)
                    StartCoroutine(ResumeDemoCompletion(_runner.MatchResult.Value));
            }
        }
        private IEnumerator ResumeDemoCompletion(MatchResult result)
        {
            int generation = matchGeneration;
            while (generation == matchGeneration && DemoNarrative?.Ready != true) yield return null;
            if (generation != matchGeneration || _runner?.State.IsFinished != true) yield break;
            HandleMatchEnded(result);
        }

        [ContextMenu("Start")]
        public void StartMatch()
        {
            if (!_isInitialized)
                InitializeMatchFromRuleset(rulesetPreset);

            _isRunning = true;
            _elapsed = 0f;
            SyncHudState();
        }

        [ContextMenu("Stop")]
        public void StopMatch()
        {
            _isRunning = false;
            _elapsed = 0f;
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
            if (HasPendingResultSave) return;
            bool wasRunning = _isRunning;
            if (selectionPresentation != null) StopCoroutine(selectionPresentation);
            selectionPresentation = null;
            if (resultPresentation != null) StopCoroutine(resultPresentation);
            resultPresentation = null;
            _elapsed = 0f;
            _pendingAnimatedTokenName = null;
            _hasBattleResultTriggered = false;
            hud?.SetSurrenderDialogVisible(false);
            _runner.Reset();
            boardViewController?.Bind(_runner);
            OnDemoRestarted?.Invoke();
            _isRunning = wasRunning;
            SyncHudState();
            UpdateUI();
        }

        private void TickOnce()
        {
            if (_runner?.State == null || _runner.State.IsFinished || _runner.MatchEnded || IsBoardAnimating())
                return;

            if (!_runner.HasAnyLegalMove())
            {
                _runner.EndTurnIfNoMoves();
                UpdateUI();
                return;
            }

            _runner.Tick();
        }

        private void ExecuteAutoTurn()
        {
            if (_runner?.State == null || _runner.State.IsFinished || _runner.MatchEnded || IsHumanTurn() || IsBoardAnimating())
                return;

            if (!_runner.HasAnyLegalMove())
            {
                _runner.EndTurnIfNoMoves();
                UpdateUI();
                return;
            }

            _runner.Tick();
        }

        private void HandleMatchStarted(GameState state)
        {
            matchGeneration++;
            IsPassingTrailTurn = false;
            SelectedHeroId = null;
            _lastHumanInputFeedback = string.Empty;
            RewardSession = new BattleRewardSession(_externalPreset != null ? _externalPreset.modeId : MatchService.ActivePreset?.modeId);
            _hasBattleResultTriggered = false;
            _waitingForFromCell = false;
            _pendingAnimatedTokenName = null;
            hud?.SetSurrenderDialogVisible(false);
            boardView?.HandleMatchStarted(state, _runner.Log);
            UpdateUI();
            /*if (verboseLog)
                Debug.Log("[Diceforge] Match start: " + state.DebugSnapshot());*/
        }

        private void HandleTurnStarted(GameState state)
        {
            _elapsed = 0f;
            _rerollUsedThisTurn = false;
            _pendingAnimatedTokenName = null;
            if (DemoLevel != null) _lastHumanInputFeedback = string.Empty;
            LogRerollCount("TurnStarted");
            if (IsHumanTurn())
                OnHumanTurnStarted?.Invoke();
            UpdateUI();
            RefreshRerollUI();
        }

        private void HandleMoveApplied(MoveRecord record)
        {
            boardView?.HandleMoveApplied(record);

            if (record.PlayerId == localPlayer && record.ApplyResult != ApplyResult.Illegal && record.Move.HasValue)
                OnHumanMoveApplied?.Invoke(record);
            UpdateUI();
            if (!verboseLog) return;

            string moveText = record.Move?.ToString() ?? "NoMove";
            string pipText = record.PipUsed.HasValue ? record.PipUsed.Value.ToString() : "-";
            Debug.Log($"[Diceforge] {record.PlayerId} -> {moveText}  Outcome={record.Outcome}  Pip={pipText}  T{record.TurnIndex}");
        }


        private void HandleMatchEnded(MatchResult result)
        {
            if (FindAnyObjectByType<DioramaBoard>() != null) resultPresentation = StartCoroutine(FinishAfterPresentation(result));
            else FinalizeMatchResult(result);
        }
        private System.Collections.IEnumerator FinishAfterPresentation(MatchResult result)
        {
            var endedRunner=_runner;
            int endedGeneration = matchGeneration;
            var profile = Resources.Load<BattlePresentationProfile>("BattlePresentationProfile");
            yield return WaitForPresentation(
                () => ReferenceEquals(endedRunner, _runner) && endedGeneration == matchGeneration && IsBoardAnimating(),
                DioramaBoard.ReducedMotion ? 0f : profile != null ? profile.victorySeconds : .5f,
                () =>
                {
                    if (!ReferenceEquals(endedRunner, _runner) || endedGeneration != matchGeneration) return;
                    boardViewController?.ReactToMatchEnd(result.Winner);
                    var cue = result.Winner == localPlayer ? profile?.victoryClip : profile?.defeatClip;
                    Diceforge.Audio.AudioManager.Instance?.PlayGameSfx(cue, profile != null ? profile.soundGain : 1f);
                }, () => { });
            if (!ReferenceEquals(endedRunner, _runner) || endedGeneration != matchGeneration || !_runner.State.IsFinished) yield break;
            DemoCheckpoint?.RetrySave();
            if (DemoNarrative != null) yield return DemoNarrative.PlayCompletion(result);
            if (ReferenceEquals(endedRunner, _runner) && endedGeneration == matchGeneration && _runner.State.IsFinished)
                FinalizeMatchResult(result);
        }

        internal static System.Collections.IEnumerator WaitForPresentation(System.Func<bool> isAnimating,
            float reactionSeconds, System.Action react, System.Action finalize)
        {
            while (isAnimating()) yield return null;
            react();
            if (reactionSeconds > 0f) yield return new WaitForSeconds(reactionSeconds);
            finalize();
        }

        private bool IsHumanTurn()
        {
            if (_runner?.State == null) return false;

            var current = _runner.State.CurrentPlayer;
            if (current == PlayerId.A)
                return controlModeA == ControlMode.Human;
            return controlModeB == ControlMode.Human;
        }

        private bool IsBoardAnimating()
        {
            EnsureBoardViewControllerResolved();
            return selectionPresentation != null || (boardViewController != null && boardViewController.IsAnimating);
        }

        private void HandleMoveClicked()
        {
            if (!IsHumanTurn() || _runner?.State == null || _runner.MatchEnded || _runner.State.IsFinished || IsBoardAnimating())
                return;

            EnsureHumanTurnReady();
            _waitingForFromCell = true;
            boardView?.SetCellSelectionEnabled(true);
            UpdateHighlightedCells();
        }

        private void HandleCellClicked(int cellIndex)
        {
            HandleSelectedCell(cellIndex, boardView?.ConsumeLastClickedPlayerATokenName());
        }

        private void HandleSelectedCell(int cellIndex, string clickedTokenName)
        {
            if (DemoNarrative?.IsModal == true) return;
            if (_runner?.State == null || !IsHumanTurn() || _runner.State.IsFinished || _runner.MatchEnded || IsBoardAnimating())
                return;

            EnsureHumanTurnReady();

            if (DemoLevel != null)
            {
                string heroId = boardViewController.HeroForToken(clickedTokenName);
                if (heroId == null)
                {
                    int candidates = 0;
                    foreach (string id in DemoLevel.heroIds)
                        if (TryGetHero(id, out int heroCell, out string name, out bool exited) && !exited && heroCell == cellIndex)
                        {
                            candidates++;
                            heroId = id;
                            clickedTokenName = name;
                        }
                    if (candidates != 1)
                    {
                        OnDemoInputRejected?.Invoke(candidates == 0 ? DemoInputRejection.EmptyOrigin : DemoInputRejection.UnknownTarget, null);
                        _lastHumanInputFeedback = "Choose a friend by name.";
                        UpdateUI();
                        return;
                    }
                }
                SelectedHeroId = heroId;
            }

            var move = SelectMoveForCell(cellIndex);
            if (!move.HasValue)
            {
                OnDemoInputRejected?.Invoke(_runner.SelectedDieIndex.HasValue ? DemoInputRejection.BlockedDestination : DemoInputRejection.NoStepSelected, SelectedHeroId);
                _pendingAnimatedTokenName = null;

                if (_runner.SelectedDieIndex.HasValue && _runner.RemainingDice.Count > _runner.SelectedDieIndex.Value)
                {
                    int die = _runner.RemainingDice[_runner.SelectedDieIndex.Value];
                    _lastHumanInputFeedback = DemoLevel != null ? "This landing is unavailable. Choose another step or friend."
                        : $"No legal move from cell {cellIndex} with die {die}";
                }
                else
                {
                    _lastHumanInputFeedback = DemoLevel != null ? "Choose a step." : $"No legal move from cell {cellIndex}";
                }

                if (verboseLog)
                    Debug.Log($"[Diceforge] {_lastHumanInputFeedback}");
                UpdateUI();
                return;
            }

            _pendingAnimatedTokenName = clickedTokenName;
            _waitingForFromCell = false;
            boardView?.SetCellSelectionEnabled(false);
            _lastHumanInputFeedback = string.Empty;
            if (FindAnyObjectByType<DioramaBoard>() != null)
            {
                boardViewController?.ReactToSelection(_pendingAnimatedTokenName);
                var profile = Resources.Load<BattlePresentationProfile>("BattlePresentationProfile");
                Diceforge.Audio.AudioManager.Instance?.PlayGameSfx(profile?.selectionClip, profile != null ? profile.soundGain : 1f);
                selectionPresentation = StartCoroutine(ApplyAfterSelection(move.Value, profile != null ? profile.selectionSeconds : .2f));
            }
            else ApplyHumanMove(move.Value);
        }

        private System.Collections.IEnumerator ApplyAfterSelection(Move move, float seconds)
        {
            var selectedRunner = _runner;
            int selectedGeneration = matchGeneration;
            yield return new WaitForSeconds(seconds);
            selectionPresentation = null;
            if (ReferenceEquals(selectedRunner, _runner) && selectedGeneration == matchGeneration && _runner?.State != null && !_runner.State.IsFinished)
                ApplyHumanMove(move);
        }

        private void HandleDieSelected(int index)
        {
            if (DemoNarrative?.IsModal == true) return;
            if (!IsHumanTurn() || _runner?.State == null || _runner.MatchEnded || _runner.State.IsFinished || IsBoardAnimating())
                return;

            TrySelectDieIndex(index);
        }

        private void HandleEnterClicked()
        {
        }

        private void HandlePlaceClicked()
        {
        }

        private void HandleRerollClicked()
        {
            if (IsBoardAnimating() || !CanUseReroll())
                return;

            if (!ProfileService.RemoveItem(ProgressionIds.ItemConsumableReroll, 1))
                return;

            _rerollUsedThisTurn = true;
            _runner.RerollCurrentTurnOutcome();
            OnHumanRerollUsed?.Invoke();
            _waitingForFromCell = false;
            _lastHumanInputFeedback = string.Empty;
            UpdateUI();
            RefreshRerollUI();
        }

        public void RequestSurrender()
        {
            if (!CanSurrender())
                return;

            hud?.SetSurrenderDialogVisible(true);
        }

        public void ForceDebugWin()
        {
            if (!CanSurrender())
                return;

            var state = _runner.State;
            hud?.SetSurrenderDialogVisible(false);
            state.Finish(localPlayer);
            FinalizeMatchResult(new MatchResult(localPlayer, MatchEndReason.Win));
        }

        public void ConfirmSurrender()
        {
            if (!CanSurrender())
            {
                hud?.SetSurrenderDialogVisible(false);
                return;
            }

            var state = _runner.State;
            PlayerId? winner = state.Rules.gameMode == GameMode.SoloTrail ? null : GetOpponent(localPlayer);

            // The battle rules do not track HP directly, so analytics reports remaining stones as HP-equivalent state.
            ClientDiagnostics.RecordBattleSurrender(new BattleSurrenderDiagnosticsContext(
                BuildBattleAnalyticsId(),
                state.TurnIndex,
                GetRemainingStones(localPlayer),
                winner.HasValue ? GetRemainingStones(winner.Value) : 0));

            if (IsDebugHudVisible())
                Debug.Log("[Battle] Player surrendered", this);

            hud?.SetSurrenderDialogVisible(false);
            state.Finish(winner);
            FinalizeMatchResult(new MatchResult(winner, MatchEndReason.Surrender));
        }

        private void HandleSurrenderCancelled()
        {
            hud?.SetSurrenderDialogVisible(false);
        }

        private void ApplyHumanMove(Move move)
        {
            if (_runner == null || _runner.State == null || _runner.State.IsFinished || _runner.MatchEnded || IsBoardAnimating())
                return;

            EnsureBoardViewControllerResolved();
            boardViewController?.SetPendingAnimatedTokenName(_pendingAnimatedTokenName);
            _pendingAnimatedTokenName = null;

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
            hud?.SetWinner(state.IsFinished ? state.Winner?.ToString() ?? "Draw" : "-");
            hud?.SetPlayerStatsA($"A: Off {state.BorneOffA} | Bar {state.BarA}");
            hud?.SetPlayerStatsB($"B: Off {state.BorneOffB} | Bar {state.BarB}");
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
                hud?.SetSurrenderEnabled(false);
                hud?.SetSurrenderDialogVisible(false);
                RefreshRerollUI();
                hud?.SetDiceButtons(null, null, false, false);
                _waitingForFromCell = false;
                boardView?.SetCellSelectionEnabled(false);
                boardView?.SetHighlightedCells(null);
                return;
            }

            var state = _runner.State;
            bool humanTurn = IsHumanTurn() && !state.IsFinished;
            bool boardAnimating = IsBoardAnimating();
            bool allowInteraction = humanTurn && !boardAnimating;
            if (allowInteraction)
            {
                EnsureHumanTurnReady();
                if (_runner.Rules.gameMode != GameMode.SoloTrail && _runner.EndTurnIfNoMoves())
                    return;
            }

            bool canMove = allowInteraction && HasLegalMove();

            hud?.SetHumanControlsEnabled(allowInteraction);
            hud?.SetMoveEnabled(canMove);
            hud?.SetEnterEnabled(false);
            hud?.SetPlaceEnabled(false);
            hud?.SetSurrenderEnabled(CanSurrender());
            RefreshRerollUI();
            bool dimDicePresentation = !humanTurn && !state.IsFinished && !_runner.MatchEnded;
            hud?.SetDiceButtons(_runner.RemainingDice, _runner.SelectedDieIndex, allowInteraction, dimDicePresentation);

            if (allowInteraction)
                _waitingForFromCell = canMove;
            else if (_waitingForFromCell)
                _waitingForFromCell = false;

            boardView?.SetCellSelectionEnabled(_waitingForFromCell);
            UpdateHighlightedCells();
        }

        private void EnsureHumanTurnReady()
        {
            if (_runner?.State == null || _runner.State.IsFinished || _runner.MatchEnded || !IsHumanTurn())
                return;

            _runner.EnsureSelectedDie();
        }

        private void HandleShortcutInput(bool isBoardAnimating)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                StepOnce();

            if (!CanCycleSelectedDie(isBoardAnimating))
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            int direction = 0;
            float scrollY = mouse.scroll.ReadValue().y;
            if (scrollY > 0.01f)
                direction = -1;
            else if (scrollY < -0.01f)
                direction = 1;
            else if (mouse.middleButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                direction = 1;

            if (direction != 0)
                CycleSelectedDie(direction);
        }

        private bool CanCycleSelectedDie(bool isBoardAnimating)
        {
            return _runner?.State != null
                && IsHumanTurn()
                && !_runner.State.IsFinished
                && !_runner.MatchEnded
                && !isBoardAnimating
                && _runner.RemainingDice.Count > 1;
        }

        private void CycleSelectedDie(int direction)
        {
            EnsureHumanTurnReady();

            int count = _runner != null ? _runner.RemainingDice.Count : 0;
            if (count <= 1)
                return;

            int currentIndex = _runner.SelectedDieIndex ?? 0;
            int nextIndex = (currentIndex + direction) % count;
            if (nextIndex < 0)
                nextIndex += count;

            TrySelectDieIndex(nextIndex);
        }

        private bool TrySelectDieIndex(int index)
        {
            if (DemoNarrative?.IsModal == true) return false;
            if (_runner == null || !_runner.SelectDieIndex(index))
            {
                if (DemoLevel != null) OnDemoInputRejected?.Invoke(DemoInputRejection.StaleStepChoice, SelectedHeroId);
                return false;
            }

            _waitingForFromCell = false;
            boardView?.SetCellSelectionEnabled(false);
            _lastHumanInputFeedback = string.Empty;
            UpdateUI();
            OnDemoStepSelected?.Invoke();
            return true;
        }

        private void RegisterHudCallbacks()
        {
            if (hud == null)
                return;

            hud.OnStep += StepOnce;
            hud.OnRestart += RestartMatch;
            hud.OnMove += HandleMoveClicked;
            hud.OnEnter += HandleEnterClicked;
            hud.OnPlace += HandlePlaceClicked;
            hud.OnReroll += HandleRerollClicked;
            hud.OnForceWinRequest += ForceDebugWin;
            hud.OnSurrenderRequest += RequestSurrender;
            hud.OnSurrenderCancel += HandleSurrenderCancelled;
            hud.OnSurrenderConfirm += ConfirmSurrender;
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

            hud.OnStep -= StepOnce;
            hud.OnRestart -= RestartMatch;
            hud.OnMove -= HandleMoveClicked;
            hud.OnEnter -= HandleEnterClicked;
            hud.OnPlace -= HandlePlaceClicked;
            hud.OnReroll -= HandleRerollClicked;
            hud.OnForceWinRequest -= ForceDebugWin;
            hud.OnSurrenderRequest -= RequestSurrender;
            hud.OnSurrenderCancel -= HandleSurrenderCancelled;
            hud.OnSurrenderConfirm -= ConfirmSurrender;
            hud.OnToggleAutoRun -= HandleAutoRunChanged;
            hud.OnSpeedChanged -= HandleSpeedChanged;
            hud.OnHumanAChanged -= HandleHumanAChanged;
            hud.OnHumanBChanged -= HandleHumanBChanged;
            hud.OnDieSelected -= HandleDieSelected;
        }


        public void NotifyTutorialRollIfReady()
        {
            if (_runner?.State == null || _runner.State.IsFinished || _runner.MatchEnded)
                return;

            if (IsHumanTurn() && _runner.CurrentOutcome.Dice != null && _runner.CurrentOutcome.Dice.Length > 0)
                OnHumanTurnStarted?.Invoke();
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

        private void HandleProfileChanged()
        {
            UpdateUI();
            RefreshRerollUI();
        }

        private void RefreshRerollUI()
        {
            if (hud == null)
                return;

            int rerollCount = ProfileService.GetItemCount(ProgressionIds.ItemConsumableReroll);
            bool humanTurn = _runner?.State != null && IsHumanTurn() && !_runner.State.IsFinished;
            bool matchNotEnded = _runner?.State != null && !_runner.MatchEnded && !_runner.State.IsFinished;
            bool visible = matchNotEnded && humanTurn;
            bool enabled = matchNotEnded && humanTurn && !IsBoardAnimating() && rerollCount > 0 && !_rerollUsedThisTurn;
            hud.SetRerollState(rerollCount, enabled, visible);
        }

        private void LogRerollCount(string stage)
        {
            if (!logRerollInventory)
                return;

            int rerollCount = ProfileService.GetItemCount(ProgressionIds.ItemConsumableReroll);
            Debug.Log($"[Diceforge] Reroll inventory ({stage}): {rerollCount}", this);
        }

        private bool CanUseReroll()
        {
            if (_runner?.State == null)
                return false;

            var state = _runner.State;
            if (state.IsFinished || _runner.MatchEnded)
                return false;

            if (!IsHumanTurn() || _rerollUsedThisTurn)
                return false;

            return ProfileService.GetItemCount(ProgressionIds.ItemConsumableReroll) > 0;
        }

        private bool CanSurrender()
        {
            return _runner?.State != null
                && !_runner.State.IsFinished
                && !_runner.MatchEnded
                && !_hasBattleResultTriggered;
        }

        private void FinalizeMatchResult(MatchResult result)
        {
            if (_hasBattleResultTriggered)
                return;

            var state = _runner?.State;
            if (state == null)
                return;

            _hasBattleResultTriggered = true;
            _isRunning = false;
            _elapsed = 0f;
            _waitingForFromCell = false;
            _pendingAnimatedTokenName = null;

            boardView?.SetCellSelectionEnabled(false);
            boardView?.SetHighlightedCells(null);
            hud?.SetSurrenderDialogVisible(false);
            hud?.SetSurrenderEnabled(false);

            boardView?.HandleMatchEnded(state);
            if (DemoNarrative != null) RewardSession?.SetDemoProgress(DemoNarrative.ExportCompletion());
            RewardSession?.Complete(result, localPlayer == result.Winner);
            DemoNarrative?.RefreshSaveStatus();
            if (verboseLog)
                Debug.Log($"[Diceforge] Match end. Winner: {result.Winner?.ToString() ?? "Draw"}  Turns: {state.TurnIndex}");

            if (Application.isPlaying)
                ClientDiagnostics.RecordBattleEnded(new BattleEndDiagnosticsContext(
                    result.Winner?.ToString() ?? "Draw",
                    result.Reason.ToString(),
                    state.TurnIndex));
            OnMatchEnded?.Invoke(result);
            UpdateUI();
            RefreshRerollUI();
        }

        private static PlayerId GetOpponent(PlayerId player)
        {
            return player == PlayerId.A ? PlayerId.B : PlayerId.A;
        }

        private int GetRemainingStones(PlayerId player)
        {
            var state = _runner?.State;
            if (state == null)
                return 0;

            return Mathf.Max(0, state.Rules.totalStonesPerPlayer - state.GetBorneOff(player));
        }

        private string BuildBattleAnalyticsId()
        {
            string modeId = MatchService.ActivePreset != null ? MatchService.ActivePreset.modeId : string.Empty;
            string mapId = BattleMapSelectionService.SelectedMap != null ? BattleMapSelectionService.SelectedMap.mapId : string.Empty;

            if (!string.IsNullOrWhiteSpace(modeId) && !string.IsNullOrWhiteSpace(mapId))
                return $"{modeId}:{mapId}";

            return !string.IsNullOrWhiteSpace(modeId) ? modeId : mapId;
        }

        private bool IsDebugHudVisible()
        {
            if (hud == null)
                return false;

            var visibilityController = hud.GetComponent<DebugUiVisibilityController>();
            return visibilityController != null && visibilityController.IsDebugUiVisible;
        }

        private void SyncHudState()
        {
            if (hud == null)
                return;

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

            if (_runner.State.IsFinished || _runner.MatchEnded)
                return $"Status: Finished ({_runner.State.Winner?.ToString() ?? "Draw"})";

            if (IsBoardAnimating())
                return "Status: Animating move";

            if (IsHumanTurn())
            {
                if (!string.IsNullOrWhiteSpace(_lastHumanInputFeedback))
                    return $"Status: {_lastHumanInputFeedback}";
                return "Status: Waiting: click a cell";
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
                text = $"{player} {fromText}в†’{toText} (Pip {pipText})";
            }
            else if (move.Kind == MoveKind.EnterFromBar)
            {
                string toText = record.ToCell.HasValue ? record.ToCell.Value.ToString() : "-";
                string pipText = record.PipUsed.HasValue ? record.PipUsed.Value.ToString() : "-";
                text = $"{player} Barв†’{toText} (Pip {pipText})";
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

            if (!IsHumanTurn() || _runner.State.IsFinished || _runner.MatchEnded || IsBoardAnimating())
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
            {
                int? selectableCell = GetSelectableCellForMove(move);
                if (selectableCell.HasValue)
                    cells.Add(selectableCell.Value);
            }

            boardView.SetHighlightedCells(cells);
        }

        private bool HasLegalMove()
        {
            if (_runner?.State == null) return false;
            return _runner.HasLegalMoveForSelectedDie();
        }


        private int? GetSelectableCellForMove(Move move)
        {
            if (_runner?.State == null)
                return null;

            if (move.Kind == MoveKind.EnterFromBar)
            {
                var entryCells = MoveGenerator.GetEntryCellsForPlayer(_runner.State.Rules, _runner.State.CurrentPlayer);
                if (move.PipUsed <= 0 || move.PipUsed > entryCells.Count)
                    return null;
                return entryCells[move.PipUsed - 1];
            }

            return move.FromCell;
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
                int? selectableCell = GetSelectableCellForMove(move);
                if (!selectableCell.HasValue || selectableCell.Value != cellIndex)
                    continue;

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



