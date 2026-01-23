using Diceforge.Core;
using UnityEngine;

namespace Diceforge.View
{
    public sealed class BattleDebugController : MonoBehaviour
    {
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

        private BattleRunner _runner;
        private bool _isRunning;
        private float _elapsed;

        private void Awake()
        {
            if (boardView == null)
                boardView = GetComponent<BoardDebugView>();

            _runner = new BattleRunner();
            _runner.OnMatchStarted += HandleMatchStarted;
            _runner.OnMoveApplied += HandleMoveApplied;
            _runner.OnMatchEnded += HandleMatchEnded;

            InitializeMatch();
        }

        private void OnDestroy()
        {
            if (_runner == null) return;

            _runner.OnMatchStarted -= HandleMatchStarted;
            _runner.OnMoveApplied -= HandleMoveApplied;
            _runner.OnMatchEnded -= HandleMatchEnded;
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

            _runner.Tick();
        }

        private void HandleMatchStarted(GameState state)
        {
            boardView?.HandleMatchStarted(state, _runner.Log);
            if (verboseLog)
                Debug.Log("[Diceforge] Match start: " + state.DebugSnapshot());
        }

        private void HandleMoveApplied(MoveRecord record)
        {
            boardView?.HandleMoveApplied(record);
            if (!verboseLog) return;

            string moveText = record.Move?.ToString() ?? "NoMove";
            Debug.Log($"[Diceforge] {record.PlayerId} -> {moveText}  T{record.TurnIndex}");
        }

        private void HandleMatchEnded(GameState state)
        {
            boardView?.HandleMatchEnded(state);
            if (verboseLog)
                Debug.Log($"[Diceforge] Match end. Winner: {state.Winner}  Turns: {state.TurnIndex}");
        }
    }
}
