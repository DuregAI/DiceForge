using System;
using System.Collections.Generic;
using Diceforge.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    public sealed class DebugHudUITK : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        public UIDocument Document => document;

        private VisualElement _root;
        private VisualElement _hudRoot;
        private Label _turnLabel;
        private Label _playerLabel;
        private Label _rolledDiceLabel;
        private Label _remainingDiceLabel;
        private Label _usedDiceLabel;
        private Label _bagLabel;
        private Label _headMovesLabel;
        private Label _lastMoveLabel;
        private Label _winnerLabel;
        private Label _statusLabel;
        private Label _aStatsLabel;
        private Label _bStatsLabel;
        private Toggle _autoRunToggle;
        private Button _stepButton;
        private Button _restartButton;
        private Button _moveButton;
        private Button _enterButton;
        private Button _placeButton;
        private Button _rerollButton;
        private Button _surrenderButton;
        private Button _surrenderCancelButton;
        private Button _surrenderConfirmButton;
        private Toggle _humanAToggle;
        private Toggle _humanBToggle;
        private Slider _speedSlider;
        private Label _speedValueLabel;
        private VisualElement _diceButtonsRow;
        private VisualElement _centerDicePresentation;
        private VisualElement _centerDiceRow;
        private VisualElement _battleTopRightControls;
        private VisualElement _surrenderDialog;
        private VisualElement _surrenderDialogWindow;

        private readonly HashSet<string> _missingWarnings = new HashSet<string>();

        public event Action OnStep;
        public event Action OnRestart;
        public event Action OnMove;
        public event Action OnEnter;
        public event Action OnPlace;
        public event Action OnReroll;
        public event Action OnSurrenderRequest;
        public event Action OnSurrenderCancel;
        public event Action OnSurrenderConfirm;
        public event Action<bool> OnToggleAutoRun;
        public event Action<float> OnSpeedChanged;
        public event Action<bool> OnHumanAChanged;
        public event Action<bool> OnHumanBChanged;
        public event Action<int> OnDieSelected;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            _root = document != null ? document.rootVisualElement : null;
            if (_root == null)
            {
                WarnMissingOnce("UIDocument/root");
                return;
            }

            _root.pickingMode = PickingMode.Ignore;

            CacheElements();
            RegisterCallbacks();
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        public void SetTurn(int turn)
        {
            if (_turnLabel != null)
                _turnLabel.text = $"Turn: {turn}";
        }

        public void SetCurrentPlayer(string player)
        {
            if (_playerLabel != null)
                _playerLabel.text = $"Player: {player}";
        }

        public void SetDiceOutcome(DiceOutcomeResult outcome, IReadOnlyList<int> remainingDice, IReadOnlyList<int> usedDice)
        {
            if (_rolledDiceLabel != null)
            {
                string diceText = outcome.Dice.Length == 0 ? "-" : string.Join(" ", outcome.Dice);
                string label = string.IsNullOrWhiteSpace(outcome.Label) ? string.Empty : $" ({outcome.Label})";
                _rolledDiceLabel.text = $"Rolled: {diceText}{label}";
            }

            if (_remainingDiceLabel != null)
            {
                string pips = remainingDice == null || remainingDice.Count == 0 ? "-" : string.Join(", ", remainingDice);
                _remainingDiceLabel.text = $"Remaining: {pips}";
            }

            if (_usedDiceLabel != null)
            {
                string used = usedDice == null || usedDice.Count == 0 ? "-" : string.Join(", ", usedDice);
                _usedDiceLabel.text = $"Used: {used}";
            }
        }

        public void SetBagStatus(int remaining, int total)
        {
            if (_bagLabel != null)
                _bagLabel.text = $"Bag: {remaining}/{total}";
        }

        public void SetHeadMovesInfo(int used, int limit)
        {
            if (_headMovesLabel != null)
            {
                string limitText = limit == int.MaxValue ? "inf" : limit.ToString();
                _headMovesLabel.text = $"Head: {used}/{limitText}";
            }
        }

        public void SetLastMove(string text)
        {
            if (_lastMoveLabel != null)
                _lastMoveLabel.text = $"Last Move: {text}";
        }

        public void SetWinner(string text)
        {
            if (_winnerLabel != null)
                _winnerLabel.text = $"Winner: {text}";
        }

        public void SetStatus(string text)
        {
            if (_statusLabel != null)
                _statusLabel.text = text;
        }

        public void SetPlayerStatsA(string text)
        {
            if (_aStatsLabel != null)
                _aStatsLabel.text = text;
        }

        public void SetPlayerStatsB(string text)
        {
            if (_bStatsLabel != null)
                _bStatsLabel.text = text;
        }

        public void SetHumanControlsEnabled(bool enabled)
        {
            SetMoveEnabled(enabled);
            SetEnterEnabled(enabled);
            SetPlaceEnabled(enabled);
        }

        public void SetMoveEnabled(bool enabled)
        {
            _moveButton?.SetEnabled(enabled);
        }

        public void SetEnterEnabled(bool enabled)
        {
            _enterButton?.SetEnabled(enabled);
        }

        public void SetPlaceEnabled(bool enabled)
        {
            _placeButton?.SetEnabled(enabled);
        }

        public void SetRerollState(int count, bool enabled, bool visible)
        {
            if (_rerollButton == null)
                return;

            _rerollButton.text = $"Reroll ({Mathf.Max(0, count)})";
            _rerollButton.SetEnabled(enabled);
            _rerollButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetSurrenderEnabled(bool enabled)
        {
            _surrenderButton?.SetEnabled(enabled);
        }

        public void SetSurrenderDialogVisible(bool visible)
        {
            if (_surrenderDialog == null)
                return;

            _surrenderDialog.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            _surrenderDialog.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;

            if (_surrenderDialogWindow != null)
                _surrenderDialogWindow.pickingMode = visible ? PickingMode.Position : PickingMode.Ignore;
        }

        public void SetDiceButtons(IReadOnlyList<int> remainingDice, int? selectedIndex, bool enabled, bool dimmed)
        {
            _diceButtonsRow?.Clear();
            _centerDiceRow?.Clear();

            bool hasDice = remainingDice != null && remainingDice.Count > 0;

            if (_centerDicePresentation != null)
            {
                _centerDicePresentation.style.display = hasDice ? DisplayStyle.Flex : DisplayStyle.None;
                _centerDicePresentation.EnableInClassList("center-dice-presentation--dimmed", dimmed);
            }

            if (!hasDice)
                return;

            for (int i = 0; i < remainingDice.Count; i++)
            {
                int index = i;
                int dieValue = remainingDice[i];
                bool isSelected = selectedIndex.HasValue && selectedIndex.Value == index;
                Button dieButton = CreateDieButton(index, dieValue, enabled, isSelected);
                _centerDiceRow?.Add(dieButton);
            }
        }

        public void SetAutoRunToggle(bool autoRun)
        {
            if (_autoRunToggle != null)
                _autoRunToggle.SetValueWithoutNotify(autoRun);
        }

        public void SetHumanAToggle(bool isHuman)
        {
            if (_humanAToggle != null)
                _humanAToggle.SetValueWithoutNotify(isHuman);
        }

        public void SetHumanBToggle(bool isHuman)
        {
            if (_humanBToggle != null)
                _humanBToggle.SetValueWithoutNotify(isHuman);
        }

        public void SetSpeed(float speed)
        {
            if (_speedSlider != null)
                _speedSlider.SetValueWithoutNotify(speed);

            UpdateSpeedLabel(speed);
        }

        private void CacheElements()
        {
            _hudRoot = _root.Q<VisualElement>(className: "hud-root");
            if (_hudRoot != null)
                _hudRoot.pickingMode = PickingMode.Position;

            _turnLabel = GetElement<Label>("turnLabel");
            _playerLabel = GetElement<Label>("playerLabel");
            _rolledDiceLabel = GetElement<Label>("rolledDiceLabel");
            _remainingDiceLabel = GetElement<Label>("remainingDiceLabel");
            _usedDiceLabel = GetElement<Label>("usedDiceLabel");
            _bagLabel = GetElement<Label>("bagLabel");
            _headMovesLabel = GetElement<Label>("headMovesLabel");
            _lastMoveLabel = GetElement<Label>("lastMoveLabel");
            _winnerLabel = GetElement<Label>("winnerLabel");
            _statusLabel = GetElement<Label>("statusLabel");
            _aStatsLabel = GetElement<Label>("aStatsLabel");
            _bStatsLabel = GetElement<Label>("bStatsLabel");
            _autoRunToggle = GetElement<Toggle>("startStopButton");
            _stepButton = GetElement<Button>("stepButton");
            _restartButton = GetElement<Button>("restartButton");
            _moveButton = GetElement<Button>("moveButton");
            _enterButton = GetElement<Button>("enterButton");
            _placeButton = GetElement<Button>("placeButton");
            _rerollButton = GetElement<Button>("rerollButton");
            _surrenderButton = GetElement<Button>("surrenderButton");
            _surrenderCancelButton = GetElement<Button>("surrenderCancelButton");
            _surrenderConfirmButton = GetElement<Button>("surrenderConfirmButton");
            _humanAToggle = GetElement<Toggle>("humanAToggle");
            _humanBToggle = GetElement<Toggle>("humanBToggle");
            _speedSlider = GetElement<Slider>("speedSlider");
            _speedValueLabel = GetElement<Label>("speedValueLabel");
            _diceButtonsRow = GetElement<VisualElement>("diceButtonsRow");
            _centerDicePresentation = GetElement<VisualElement>("centerDicePresentation");
            _centerDiceRow = GetElement<VisualElement>("centerDiceRow");
            _battleTopRightControls = GetElement<VisualElement>("battleTopRightControls");
            _surrenderDialog = GetElement<VisualElement>("surrenderDialog");
            _surrenderDialogWindow = GetElement<VisualElement>("surrenderDialogWindow");

            if (_diceButtonsRow != null)
                _diceButtonsRow.style.display = DisplayStyle.None;

            if (_centerDicePresentation != null)
            {
                _centerDicePresentation.pickingMode = PickingMode.Position;
                _centerDicePresentation.style.display = DisplayStyle.None;
            }

            if (_centerDiceRow != null)
                _centerDiceRow.pickingMode = PickingMode.Position;

            if (_battleTopRightControls != null)
                _battleTopRightControls.pickingMode = PickingMode.Position;

            SetSurrenderDialogVisible(false);
        }

        private void RegisterCallbacks()
        {
            if (_autoRunToggle != null)
                _autoRunToggle.RegisterValueChangedCallback(HandleAutoRunChanged);
            if (_stepButton != null)
                _stepButton.clicked += HandleStepClicked;
            if (_restartButton != null)
                _restartButton.clicked += HandleRestartClicked;
            if (_moveButton != null)
                _moveButton.clicked += HandleMoveClicked;
            if (_enterButton != null)
                _enterButton.clicked += HandleEnterClicked;
            if (_placeButton != null)
                _placeButton.clicked += HandlePlaceClicked;
            if (_rerollButton != null)
                _rerollButton.clicked += HandleRerollClicked;
            if (_surrenderButton != null)
                _surrenderButton.clicked += HandleSurrenderClicked;
            if (_surrenderCancelButton != null)
                _surrenderCancelButton.clicked += HandleSurrenderCancelClicked;
            if (_surrenderConfirmButton != null)
                _surrenderConfirmButton.clicked += HandleSurrenderConfirmClicked;
            if (_speedSlider != null)
            {
                _speedSlider.RegisterValueChangedCallback(HandleSpeedChanged);
                UpdateSpeedLabel(_speedSlider.value);
            }
            if (_humanAToggle != null)
                _humanAToggle.RegisterValueChangedCallback(HandleHumanAChanged);
            if (_humanBToggle != null)
                _humanBToggle.RegisterValueChangedCallback(HandleHumanBChanged);
        }

        private void UnregisterCallbacks()
        {
            if (_autoRunToggle != null)
                _autoRunToggle.UnregisterValueChangedCallback(HandleAutoRunChanged);
            if (_stepButton != null)
                _stepButton.clicked -= HandleStepClicked;
            if (_restartButton != null)
                _restartButton.clicked -= HandleRestartClicked;
            if (_moveButton != null)
                _moveButton.clicked -= HandleMoveClicked;
            if (_enterButton != null)
                _enterButton.clicked -= HandleEnterClicked;
            if (_placeButton != null)
                _placeButton.clicked -= HandlePlaceClicked;
            if (_rerollButton != null)
                _rerollButton.clicked -= HandleRerollClicked;
            if (_surrenderButton != null)
                _surrenderButton.clicked -= HandleSurrenderClicked;
            if (_surrenderCancelButton != null)
                _surrenderCancelButton.clicked -= HandleSurrenderCancelClicked;
            if (_surrenderConfirmButton != null)
                _surrenderConfirmButton.clicked -= HandleSurrenderConfirmClicked;
            if (_speedSlider != null)
                _speedSlider.UnregisterValueChangedCallback(HandleSpeedChanged);
            if (_humanAToggle != null)
                _humanAToggle.UnregisterValueChangedCallback(HandleHumanAChanged);
            if (_humanBToggle != null)
                _humanBToggle.UnregisterValueChangedCallback(HandleHumanBChanged);
        }

        private void HandleStepClicked()
        {
            OnStep?.Invoke();
        }

        private void HandleRestartClicked()
        {
            OnRestart?.Invoke();
        }

        private void HandleMoveClicked()
        {
            OnMove?.Invoke();
        }

        private void HandleEnterClicked()
        {
            OnEnter?.Invoke();
        }

        private void HandlePlaceClicked()
        {
            OnPlace?.Invoke();
        }

        private void HandleRerollClicked()
        {
            OnReroll?.Invoke();
        }

        private void HandleSurrenderClicked()
        {
            OnSurrenderRequest?.Invoke();
        }

        private void HandleSurrenderCancelClicked()
        {
            OnSurrenderCancel?.Invoke();
        }

        private void HandleSurrenderConfirmClicked()
        {
            OnSurrenderConfirm?.Invoke();
        }

        private void HandleAutoRunChanged(ChangeEvent<bool> evt)
        {
            OnToggleAutoRun?.Invoke(evt.newValue);
        }

        private void HandleSpeedChanged(ChangeEvent<float> evt)
        {
            UpdateSpeedLabel(evt.newValue);
            OnSpeedChanged?.Invoke(evt.newValue);
        }

        private void HandleHumanAChanged(ChangeEvent<bool> evt)
        {
            OnHumanAChanged?.Invoke(evt.newValue);
        }

        private void HandleHumanBChanged(ChangeEvent<bool> evt)
        {
            OnHumanBChanged?.Invoke(evt.newValue);
        }

        private void UpdateSpeedLabel(float value)
        {
            if (_speedValueLabel != null)
                _speedValueLabel.text = $"Speed (s): {value:0.00}";
        }

        private Button CreateDieButton(int index, int dieValue, bool enabled, bool isSelected)
        {
            var button = new Button(() => OnDieSelected?.Invoke(index))
            {
                text = dieValue.ToString()
            };

            button.SetEnabled(enabled);
            button.AddToClassList("center-dice-button");
            if (isSelected)
                button.AddToClassList("center-dice-button--selected");

            return button;
        }

        private T GetElement<T>(string name) where T : VisualElement
        {
            if (_root == null)
                return null;

            var element = _root.Q<T>(name);
            if (element == null)
                WarnMissingOnce(name);
            return element;
        }

        private void WarnMissingOnce(string name)
        {
            if (_missingWarnings.Add(name))
                Debug.LogWarning($"[DebugHudUITK] Missing UI element: {name}", this);
        }
    }
}