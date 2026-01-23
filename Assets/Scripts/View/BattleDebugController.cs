using Diceforge.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private TMP_Text currentPlayerText;
        [SerializeField] private TMP_Text rollText;
        [SerializeField] private TMP_Text chipsInHandText;
        [SerializeField] private TMP_Text lastMoveText;
        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button placeChipButton;

        private BattleRunner _runner;
        private bool _isRunning;
        private float _elapsed;
        private bool _waitingForChipCell;

        private void Awake()
        {
            if (boardView == null)
                boardView = GetComponent<BoardDebugView>();

            _runner = new BattleRunner();
            _runner.OnMatchStarted += HandleMatchStarted;
            _runner.OnMoveApplied += HandleMoveApplied;
            _runner.OnMatchEnded += HandleMatchEnded;

            EnsureRuntimeUI();

            if (boardView != null)
                boardView.OnCellClicked += HandleCellClicked;

            if (moveButton != null)
                moveButton.onClick.AddListener(HandleMoveClicked);
            if (placeChipButton != null)
                placeChipButton.onClick.AddListener(HandlePlaceChipClicked);

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

            if (moveButton != null)
                moveButton.onClick.RemoveListener(HandleMoveClicked);
            if (placeChipButton != null)
                placeChipButton.onClick.RemoveListener(HandlePlaceChipClicked);
        }

        private void Start()
        {
            if (autoStart)
                StartMatch();
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
        }

        [ContextMenu("Stop")]
        public void StopMatch()
        {
            _isRunning = false;
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
            _waitingForChipCell = false;
            boardView?.SetCellSelectionEnabled(false);
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
            _waitingForChipCell = false;
            boardView?.SetCellSelectionEnabled(false);
            UpdateUI();
        }

        private void EnsureRuntimeUI()
        {
            bool needsBuild = turnText == null || currentPlayerText == null || rollText == null
                              || chipsInHandText == null || lastMoveText == null || winnerText == null
                              || moveButton == null || placeChipButton == null;

            if (!needsBuild)
                return;

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasObj = new GameObject("DebugCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            if (FindObjectOfType<EventSystem>() == null)
            {
                var eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
            }

            var panelObj = new GameObject("DebugPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            var panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(20f, -20f);
            panelRect.sizeDelta = new Vector2(260f, 300f);

            var layout = panelObj.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 6f;
            layout.padding = new RectOffset(6, 6, 6, 6);

            var fitter = panelObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            turnText = CreateText(panelObj.transform, "Turn");
            currentPlayerText = CreateText(panelObj.transform, "Player");
            rollText = CreateText(panelObj.transform, "Roll");
            chipsInHandText = CreateText(panelObj.transform, "Chips");
            lastMoveText = CreateText(panelObj.transform, "Last Move");
            winnerText = CreateText(panelObj.transform, "Winner");

            moveButton = CreateButton(panelObj.transform, "Move");
            placeChipButton = CreateButton(panelObj.transform, "Place Chip");
        }

        private static TMP_Text CreateText(Transform parent, string label)
        {
            var obj = new GameObject($"Text_{label}");
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Left;
            text.text = $"{label}:";
            return text;
        }

        private static Button CreateButton(Transform parent, string label)
        {
            var obj = new GameObject($"Button_{label}");
            obj.transform.SetParent(parent, false);
            var image = obj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
            var button = obj.AddComponent<Button>();

            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 32f);

            var textObj = new GameObject("Label");
            textObj.transform.SetParent(obj.transform, false);
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 18;
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            text.color = Color.white;

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
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

            var roll = _runner.State.CurrentRoll;
            ApplyHumanMove(Move.Step(roll));
        }

        private void HandlePlaceChipClicked()
        {
            if (!IsHumanTurn() || _runner.State == null)
                return;

            _waitingForChipCell = true;
            boardView?.SetCellSelectionEnabled(true);
        }

        private void HandleCellClicked(int cellIndex)
        {
            if (!_waitingForChipCell)
                return;

            _waitingForChipCell = false;
            boardView?.SetCellSelectionEnabled(false);
            ApplyHumanMove(Move.PlaceChip(cellIndex));
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
            if (_runner?.State == null) return;

            var state = _runner.State;
            string lastMove = _runner.Log.Last?.Move?.ToString() ?? "None";

            if (turnText != null)
                turnText.text = $"Turn: {state.TurnIndex}";
            if (currentPlayerText != null)
                currentPlayerText.text = $"Player: {state.CurrentPlayer}";
            if (rollText != null)
                rollText.text = $"Roll: {state.CurrentRoll}";
            if (chipsInHandText != null)
                chipsInHandText.text = $"Chips In Hand A:{state.ChipsInHandA} B:{state.ChipsInHandB}";
            if (lastMoveText != null)
                lastMoveText.text = $"Last Move: {lastMove}";
            if (winnerText != null)
                winnerText.text = state.IsFinished ? $"Winner: {state.Winner}" : "Winner: -";

            bool humanTurn = IsHumanTurn() && !state.IsFinished;
            if (moveButton != null)
                moveButton.interactable = humanTurn;
            if (placeChipButton != null)
                placeChipButton.interactable = humanTurn && state.GetChipsInHand(state.CurrentPlayer) > 0;
        }
    }
}
