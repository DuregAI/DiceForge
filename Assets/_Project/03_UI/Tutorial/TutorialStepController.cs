using Diceforge.Core;
using Diceforge.View;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TutorialStepController : MonoBehaviour
{
    [SerializeField] private BattleDebugController battleController;
    [SerializeField] private VisualTreeAsset tutorialStepsLayout;

    private TutorialStepsView _view;
    private int _stepIndex;
    private int _movesMade;
    private bool _doubleDiceModeEnabled;
    private bool _waitingDoubleRoll;
    private bool _isActive;
    private bool _battleEventsSubscribed;
    private readonly System.Collections.Generic.HashSet<string> _countedMoves = new();

    private readonly struct TutorialStep
    {
        public TutorialStep(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    private static readonly TutorialStep[] Steps =
    {
        new("Roll a single die to begin your training."),
        new("Move one stone once to learn basic movement."),
        new("Use one reroll to change your outcome."),
        new("Keep going: make a total of 4 valid moves."),
        new("Enable Double Dice Mode, then start your next roll."),
        new("Bring everyone home to finish the tactical objective."),
        new("ChiefWombat: Great job, {playerName}! The squad is ready.")
    };

    private void Start()
    {
        if (!TutorialFlow.IsTrainingBattleActive)
            return;

        _isActive = true;

        if (battleController == null)
            battleController = FindAnyObjectByType<BattleDebugController>();

        if (battleController == null)
        {
            Debug.LogWarning("[Tutorial] BattleDebugController not found.");
            return;
        }

        BuildView();
        ResetTutorialState();
        SetStep(0);
        SubscribeBattleEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeBattleEvents();

        if (_view != null)
        {
            _view.SkipStepClicked -= HandleSkipStepClicked;
            _view.SkipTutorialClicked -= HandleSkipTutorialClicked;
            _view.DoubleDiceModeChanged -= HandleDoubleDiceModeChanged;
            _view.Dispose();
            _view = null;
        }
    }

    private void BuildView()
    {
        UIDocument doc = battleController.Hud != null ? battleController.Hud.Document : null;
        var root = doc != null ? doc.rootVisualElement : null;
        if (root == null)
            return;

/*        if (tutorialStepsLayout == null)
            tutorialStepsLayout = Resources.Load<VisualTreeAsset>("Tutorial/TutorialStepsView");*/

        if (tutorialStepsLayout == null)
        {
            Debug.LogWarning("[Tutorial] TutorialStepsView layout is not assigned.");
            return;
        }

        _view = new TutorialStepsView(root, tutorialStepsLayout);
        _view.SkipStepClicked += HandleSkipStepClicked;
        _view.SkipTutorialClicked += HandleSkipTutorialClicked;
        _view.DoubleDiceModeChanged += HandleDoubleDiceModeChanged;
        _view.SetVisible(true);
    }

    private void SubscribeBattleEvents()
    {
        if (battleController == null || _battleEventsSubscribed)
            return;

        UnsubscribeBattleEvents();

        battleController.OnHumanTurnStarted += HandleHumanTurnStarted;
        battleController.OnHumanMoveApplied += HandleHumanMoveApplied;
        battleController.OnHumanRerollUsed += HandleHumanRerollUsed;
        battleController.OnMatchEnded += HandleMatchEnded;
        _battleEventsSubscribed = true;
    }

    private void UnsubscribeBattleEvents()
    {
        if (battleController == null || !_battleEventsSubscribed)
            return;

        battleController.OnHumanTurnStarted -= HandleHumanTurnStarted;
        battleController.OnHumanMoveApplied -= HandleHumanMoveApplied;
        battleController.OnHumanRerollUsed -= HandleHumanRerollUsed;
        battleController.OnMatchEnded -= HandleMatchEnded;
        _battleEventsSubscribed = false;
    }

    private void ResetTutorialState()
    {
        _stepIndex = 0;
        _movesMade = 0;
        _countedMoves.Clear();
        _doubleDiceModeEnabled = false;
        _waitingDoubleRoll = false;

        if (_view == null)
            return;

        _view.SetDoubleDiceToggleValue(false);
        _view.SetDoubleDiceToggleVisible(false);
        _view.SetHighlightVisible(false);
    }

    private void HandleHumanTurnStarted()
    {
        if (!_isActive)
            return;

        if (_stepIndex == 0 && battleController.CurrentRollDiceCount <= 1)
        {
            AdvanceStep();
            return;
        }

        if (_stepIndex == 4 && _waitingDoubleRoll)
        {
            AdvanceStep();
            return;
        }

        TryCompleteHomeObjective();
    }

    private void HandleHumanMoveApplied(MoveRecord record)
    {
        if (!_isActive || record.ApplyResult == ApplyResult.Illegal || !record.Move.HasValue)
            return;

        var moveKey = $"{record.TurnIndex}:{record.FromCell}:{record.ToCell}:{record.PipUsed}:{record.PlayerId}";
        if (!_countedMoves.Add(moveKey))
            return;

        _movesMade = Mathf.Max(_movesMade + 1, 0);

        if (_stepIndex == 1)
        {
            AdvanceStep();
            return;
        }

        if (_stepIndex == 3 && _movesMade >= 4)
        {
            AdvanceStep();
            return;
        }

        TryCompleteHomeObjective();
    }

    private void HandleHumanRerollUsed()
    {
        if (!_isActive || _stepIndex != 2)
            return;

        AdvanceStep();
    }

    private void HandleMatchEnded(MatchResult _)
    {
        if (!_isActive)
            return;

        TryCompleteHomeObjective();
    }

    private void TryCompleteHomeObjective()
    {
        if (_stepIndex != 5)
            return;

        int target = Mathf.Max(1, battleController.TotalStonesPerPlayer);
        if (battleController.LocalPlayerBorneOffCount < target)
            return;

        AdvanceStep();
        FinishTutorial();
    }

    private void HandleDoubleDiceModeChanged(bool enabled)
    {
        _doubleDiceModeEnabled = enabled;
        _waitingDoubleRoll = _stepIndex == 4 && _doubleDiceModeEnabled;
    }

    private void HandleSkipStepClicked()
    {
        if (!_isActive)
            return;

        if (_stepIndex >= Steps.Length - 1)
            return;

        AdvanceStep();
    }

    private void HandleSkipTutorialClicked()
    {
        if (!_isActive)
            return;

        TutorialFlow.CompleteTutorialAndExit();
    }

    private void SetStep(int stepIndex)
    {
        _stepIndex = Mathf.Clamp(stepIndex, 0, Steps.Length - 1);
        if (_view == null)
            return;

        string text = Steps[_stepIndex].Text.Replace("{playerName}", Diceforge.Progression.ProfileService.GetDisplayName());
        _view.SetStepText(text);
        _view.SetProgress(_stepIndex + 1, Steps.Length);
        _view.SetSkipStepEnabled(_stepIndex < Steps.Length - 1);

        bool showDoubleToggle = _stepIndex == 4;
        _view.SetDoubleDiceToggleVisible(showDoubleToggle);
        _view.SetHighlightVisible(false);
        if (!showDoubleToggle)
        {
            _doubleDiceModeEnabled = false;
            _waitingDoubleRoll = false;
            _view.SetDoubleDiceToggleValue(false);
        }
    }

    private void AdvanceStep()
    {
        int next = Mathf.Min(_stepIndex + 1, Steps.Length - 1);
        if (next == _stepIndex)
            return;

        SetStep(next);
    }

    private void FinishTutorial()
    {
        _isActive = false;
        TutorialFlow.CompleteTutorialAndExit();
    }
}
