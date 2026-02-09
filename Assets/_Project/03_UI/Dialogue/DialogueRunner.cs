using System;
using Diceforge.Dialogue;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI.Dialogue
{
    public sealed class DialogueRunner : MonoBehaviour
    {
        [SerializeField] private UIDocument dialogueDocument;

        private DialogueView _view;
        private bool _isRunning;
        private DialogueSequence _activeSequence;
        private int _lineIndex;
        private Action _onFinished;

        private void Awake()
        {
            if (dialogueDocument == null)
            {
                dialogueDocument = GetComponent<UIDocument>();
            }

            if (dialogueDocument == null)
            {
                Debug.LogWarning("[DialogueRunner] UIDocument is missing.");
                return;
            }

            _view = new DialogueView(dialogueDocument.rootVisualElement);
            _view.SetVisible(false);
            _view.NextClicked += Advance;
            _view.SkipClicked += Finish;
        }

        private void OnDestroy()
        {
            if (_view == null)
            {
                return;
            }

            _view.NextClicked -= Advance;
            _view.SkipClicked -= Finish;
        }

        public bool StartDialogue(DialogueSequence sequence, Action onFinished = null)
        {
            if (_isRunning || _view == null || sequence == null || sequence.lines == null || sequence.lines.Count == 0)
            {
                return false;
            }

            _isRunning = true;
            _activeSequence = sequence;
            _lineIndex = 0;
            _onFinished = onFinished;
            _view.SetVisible(true);
            ShowCurrentLine();
            return true;
        }

        private void Advance()
        {
            if (!_isRunning)
            {
                return;
            }

            _lineIndex++;
            if (_lineIndex >= _activeSequence.lines.Count)
            {
                Finish();
                return;
            }

            ShowCurrentLine();
        }

        private void ShowCurrentLine()
        {
            var line = _activeSequence.lines[_lineIndex];
            var text = line.text ?? string.Empty;
            text = text.Replace("{playerName}", ProfileService.GetDisplayName(), StringComparison.Ordinal);
            var speaker = string.IsNullOrWhiteSpace(line.speakerId) ? "Narrator" : line.speakerId;
            _view.SetLine(speaker, text, line.portrait);
        }

        private void Finish()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            _activeSequence = null;
            _lineIndex = 0;
            _view.SetVisible(false);
            var callback = _onFinished;
            _onFinished = null;
            callback?.Invoke();
        }
    }
}
