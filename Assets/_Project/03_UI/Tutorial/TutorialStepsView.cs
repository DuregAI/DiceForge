using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TutorialStepsView
{
    private readonly VisualElement _root;
    private readonly Label _stepText;
    private readonly Label _progressText;
    private readonly Toggle _doubleDiceToggle;
    private readonly Button _skipButton;

    public event Action SkipClicked;
    public event Action<bool> DoubleDiceModeChanged;

    public TutorialStepsView(VisualElement parent, VisualTreeAsset layout)
    {
        if (parent == null || layout == null)
            return;

        _root = layout.CloneTree();
        parent.Add(_root);

        _stepText = _root.Q<Label>(className: "tutorial-steps-text");
        _progressText = _root.Q<Label>(className: "tutorial-steps-progress");
        _doubleDiceToggle = _root.Q<Toggle>(className: "tutorial-steps-double-toggle");
        _skipButton = _root.Q<Button>(className: "tutorial-steps-skip");

        if (_skipButton != null)
            _skipButton.clicked += HandleSkipClicked;

        if (_doubleDiceToggle != null)
            _doubleDiceToggle.RegisterValueChangedCallback(HandleDoubleToggleChanged);

        SetDoubleDiceToggleVisible(false);
    }

    public void Dispose()
    {
        if (_skipButton != null)
            _skipButton.clicked -= HandleSkipClicked;

        if (_doubleDiceToggle != null)
            _doubleDiceToggle.UnregisterValueChangedCallback(HandleDoubleToggleChanged);

        _root?.RemoveFromHierarchy();
    }

    public void SetVisible(bool visible)
    {
        if (_root == null)
            return;

        _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

    private void HandleSkipClicked()
    {
        SkipClicked?.Invoke();
    }

    private void HandleDoubleToggleChanged(ChangeEvent<bool> evt)
    {
        DoubleDiceModeChanged?.Invoke(evt.newValue);
    }
}
