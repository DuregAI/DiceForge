using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TutorialStepsView
{
    private readonly VisualElement _parent;
    private readonly VisualElement _overlay;
    private readonly VisualElement _root;
    private readonly Label _stepText;
    private readonly Label _progressText;
    private readonly Toggle _doubleDiceToggle;
    private readonly Button _skipStepButton;
    private readonly Button _skipTutorialButton;
    private readonly VisualElement _highlightRing;
    private bool _eventsBound;

    public event Action SkipStepClicked;
    public event Action SkipTutorialClicked;
    public event Action<bool> DoubleDiceModeChanged;

    public TutorialStepsView(VisualElement parent, VisualTreeAsset layout)
    {
        if (parent == null || layout == null)
            return;

        _parent = parent;
        _overlay = new VisualElement
        {
            pickingMode = PickingMode.Ignore
        };
        _overlay.style.position = Position.Absolute;
        _overlay.style.left = 0;
        _overlay.style.right = 0;
        _overlay.style.top = 0;
        _overlay.style.bottom = 0;

        _parent.Add(_overlay);

        _root = layout.CloneTree();
        _root.style.flexGrow = 1;
        _overlay.Add(_root);

        _stepText = _root.Q<Label>(className: "tutorial-steps-text");
        _progressText = _root.Q<Label>(className: "tutorial-steps-progress");
        _doubleDiceToggle = _root.Q<Toggle>(className: "tutorial-steps-double-toggle");
        _skipStepButton = _root.Q<Button>(className: "tutorial-steps-skip-step");
        _skipTutorialButton = _root.Q<Button>(className: "tutorial-steps-skip-tutorial");
        _highlightRing = _root.Q<VisualElement>(className: "tutorial-steps-highlight-ring");

        BindEvents();

        SetDoubleDiceToggleVisible(false);
        SetHighlightVisible(false);
    }

    public void Dispose()
    {
        UnbindEvents();
        _overlay?.RemoveFromHierarchy();
    }

    public void SetVisible(bool visible)
    {
        if (_overlay == null)
            return;

        if (visible)
        {
            if (_overlay.parent == null)
                _parent?.Add(_overlay);

            BindEvents();
            _overlay.style.display = DisplayStyle.Flex;
            return;
        }

        UnbindEvents();
        _overlay.RemoveFromHierarchy();
    }

    public void SetStepText(string text)
    {
        if (_stepText != null)
            _stepText.text = text;
    }

    public void SetProgress(int currentStep, int totalSteps)
    {
        if (_progressText == null)
            return;

        _progressText.text = $"Step {Mathf.Clamp(currentStep, 1, totalSteps)}/{Mathf.Max(1, totalSteps)}";
    }

    public void SetSkipStepEnabled(bool enabled)
    {
        if (_skipStepButton != null)
            _skipStepButton.SetEnabled(enabled);
    }

    public void SetDoubleDiceToggleVisible(bool visible)
    {
        if (_doubleDiceToggle == null)
            return;

        _doubleDiceToggle.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public void SetDoubleDiceToggleValue(bool value)
    {
        _doubleDiceToggle?.SetValueWithoutNotify(value);
    }

    public void SetHighlightVisible(bool visible)
    {
        if (_highlightRing == null)
            return;

        _highlightRing.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _highlightRing.style.opacity = visible ? 1f : 0f;
    }

    private void HandleSkipStepClicked()
    {
        SkipStepClicked?.Invoke();
    }

    private void HandleSkipTutorialClicked()
    {
        SkipTutorialClicked?.Invoke();
    }

    private void HandleDoubleToggleChanged(ChangeEvent<bool> evt)
    {
        DoubleDiceModeChanged?.Invoke(evt.newValue);
    }

    private void BindEvents()
    {
        if (_eventsBound)
            return;

        if (_skipStepButton != null)
            _skipStepButton.clicked += HandleSkipStepClicked;

        if (_skipTutorialButton != null)
            _skipTutorialButton.clicked += HandleSkipTutorialClicked;

        if (_doubleDiceToggle != null)
            _doubleDiceToggle.RegisterValueChangedCallback(HandleDoubleToggleChanged);

        _eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!_eventsBound)
            return;

        if (_skipStepButton != null)
            _skipStepButton.clicked -= HandleSkipStepClicked;

        if (_skipTutorialButton != null)
            _skipTutorialButton.clicked -= HandleSkipTutorialClicked;

        if (_doubleDiceToggle != null)
            _doubleDiceToggle.UnregisterValueChangedCallback(HandleDoubleToggleChanged);

        _eventsBound = false;
    }
}
