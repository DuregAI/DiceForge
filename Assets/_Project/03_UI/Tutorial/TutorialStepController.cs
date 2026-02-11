using Diceforge.Core;
using Diceforge.View;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TutorialStepController : MonoBehaviour
{
    [SerializeField] private BattleDebugController battleController;

    private VisualElement _panel;
    private Label _stepLabel;
    private Button _skipButton;
    private int _stepIndex;
    private bool _isActive;

    private static readonly string[] StepTexts =
    {
        "Roll the dice to begin.",
        "Now make your first move.",
        "Use a reroll to change your fate.",
        "Nice! Training complete."
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

        BuildHintPanel();
        SubscribeBattleEvents();
        SetStep(0);
        battleController.NotifyTutorialRollIfReady();
    }

    private void OnDestroy()
    {
        UnsubscribeBattleEvents();
        if (_skipButton != null)
            _skipButton.clicked -= HandleSkipClicked;
    }

    private void SubscribeBattleEvents()
    {
        if (battleController == null)
            return;

        battleController.OnHumanTurnStarted += HandleHumanTurnStarted;
        battleController.OnHumanMoveApplied += HandleHumanMoveApplied;
        battleController.OnHumanRerollUsed += HandleHumanRerollUsed;
    }

    private void UnsubscribeBattleEvents()
    {
        if (battleController == null)
            return;

        battleController.OnHumanTurnStarted -= HandleHumanTurnStarted;
        battleController.OnHumanMoveApplied -= HandleHumanMoveApplied;
        battleController.OnHumanRerollUsed -= HandleHumanRerollUsed;
    }

    private void BuildHintPanel()
    {
        UIDocument doc = battleController.Hud != null ? battleController.Hud.Document : null;
        var root = doc != null ? doc.rootVisualElement : null;
        if (root == null)
            return;

        _panel = new VisualElement();
        _panel.style.position = Position.Absolute;
        _panel.style.left = 0;
        _panel.style.right = 0;
        _panel.style.bottom = 16;
        _panel.style.alignItems = Align.Center;
        _panel.style.justifyContent = Justify.Center;

        var card = new VisualElement();
        card.style.backgroundColor = new Color(0.1f, 0.11f, 0.17f, 0.92f);
        card.style.borderTopLeftRadius = 10;
        card.style.borderTopRightRadius = 10;
        card.style.borderBottomLeftRadius = 10;
        card.style.borderBottomRightRadius = 10;
        card.style.paddingTop = 10;
        card.style.paddingBottom = 10;
        card.style.paddingLeft = 14;
        card.style.paddingRight = 14;
        card.style.minWidth = 340;
        card.style.maxWidth = 560;
        card.style.alignItems = Align.Center;

        var title = new Label("Tutorial");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.color = Color.white;
        title.style.marginBottom = 6;

        _stepLabel = new Label(string.Empty);
        _stepLabel.style.color = Color.white;
        _stepLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _stepLabel.style.whiteSpace = WhiteSpace.Normal;
        _stepLabel.style.marginBottom = 8;

        _skipButton = new Button(HandleSkipClicked)
        {
            text = "Skip tutorial"
        };

        card.Add(title);
        card.Add(_stepLabel);
        card.Add(_skipButton);
        _panel.Add(card);
        root.Add(_panel);
    }

    private void HandleHumanTurnStarted()
    {
        if (_stepIndex == 0)
            AdvanceStep();
    }

    private void HandleHumanMoveApplied(MoveRecord record)
    {
        if (_stepIndex == 1)
            AdvanceStep();
    }

    private void HandleHumanRerollUsed()
    {
        if (_stepIndex != 2)
            return;

        AdvanceStep();
        FinishTutorial();
    }

    private void HandleSkipClicked()
    {
        if (!_isActive)
            return;

        TutorialFlow.CompleteTutorialAndExit();
    }

    private void SetStep(int stepIndex)
    {
        _stepIndex = Mathf.Clamp(stepIndex, 0, StepTexts.Length - 1);
        if (_stepLabel != null)
            _stepLabel.text = StepTexts[_stepIndex];
    }

    private void AdvanceStep()
    {
        int next = Mathf.Min(_stepIndex + 1, StepTexts.Length - 1);
        SetStep(next);
    }

    private void FinishTutorial()
    {
        TutorialFlow.CompleteTutorialAndExit();
    }
}
