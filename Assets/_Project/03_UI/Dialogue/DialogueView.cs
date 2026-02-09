using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI.Dialogue
{
    public sealed class DialogueView
    {
        private readonly VisualElement _panel;
        private readonly VisualElement _portraitSlot;
        private readonly Label _speakerLabel;
        private readonly Label _textLabel;
        private readonly Button _nextButton;
        private readonly Button _skipButton;

        public event Action NextClicked;
        public event Action SkipClicked;

        public DialogueView(VisualElement root)
        {
            _panel = root.Q<VisualElement>("DialoguePanel");
            _portraitSlot = root.Q<VisualElement>("DialoguePortrait");
            _speakerLabel = root.Q<Label>("DialogueSpeaker");
            _textLabel = root.Q<Label>("DialogueText");

            _nextButton = root.Q<Button>("DialogueNext");
            _skipButton = root.Q<Button>("DialogueSkip");

            if (_nextButton != null)
            {
                _nextButton.clicked += HandleNextClicked;
            }

            if (_skipButton != null)
            {
                _skipButton.clicked += HandleSkipClicked;
            }
        }

        public void Dispose()
        {
            if (_nextButton != null)
            {
                _nextButton.clicked -= HandleNextClicked;
            }

            if (_skipButton != null)
            {
                _skipButton.clicked -= HandleSkipClicked;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetLine(string speaker, string text, Sprite portrait)
        {
            if (_speakerLabel != null)
            {
                _speakerLabel.text = speaker;
            }

            if (_textLabel != null)
            {
                _textLabel.text = text;
            }

            if (_portraitSlot != null)
            {
                _portraitSlot.style.backgroundImage = portrait == null ? StyleKeyword.None : new StyleBackground(portrait);
            }
        }

        private void HandleNextClicked()
        {
            NextClicked?.Invoke();
        }

        private void HandleSkipClicked()
        {
            SkipClicked?.Invoke();
        }
    }
}
