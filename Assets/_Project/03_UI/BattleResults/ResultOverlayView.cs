using Diceforge.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    public sealed class ResultOverlayView : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private BattleDebugController battleController;

        private VisualElement _overlayRoot;
        private Label _resultLabel;
        private Button _restartButton;
        private Button _backToMenuButton;
        private bool _isVisible;

        private void OnEnable()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            if (battleController == null)
                battleController = FindAnyObjectByType<BattleDebugController>();

            var root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return;

            _overlayRoot = root.Q<VisualElement>("resultOverlayRoot");
            _resultLabel = root.Q<Label>("resultLabel");
            _restartButton = root.Q<Button>("restartButton");
            _backToMenuButton = root.Q<Button>("backToMenuButton");

            if (_restartButton != null)
                _restartButton.clicked += HandleRestartClicked;
            if (_backToMenuButton != null)
                _backToMenuButton.clicked += HandleBackToMenuClicked;

            HideOverlay();

            if (battleController != null)
                battleController.OnMatchEnded += HandleMatchEnded;
        }

        private void OnDisable()
        {
            if (battleController != null)
                battleController.OnMatchEnded -= HandleMatchEnded;

            if (_restartButton != null)
                _restartButton.clicked -= HandleRestartClicked;
            if (_backToMenuButton != null)
                _backToMenuButton.clicked -= HandleBackToMenuClicked;
        }

        private void HandleMatchEnded(MatchResult result)
        {
            if (_isVisible)
                return;

            bool won = battleController != null && battleController.LocalPlayer == result.Winner;
            if (_resultLabel != null)
                _resultLabel.text = won ? "Victory" : "Defeat";

            if (_overlayRoot != null)
                _overlayRoot.style.display = DisplayStyle.Flex;

            _isVisible = true;
        }

        private void HandleRestartClicked()
        {
            battleController?.RestartMatch();
            HideOverlay();
        }

        private void HandleBackToMenuClicked()
        {
            SceneManager.LoadScene("MainMenu");
        }

        private void HideOverlay()
        {
            if (_overlayRoot != null)
                _overlayRoot.style.display = DisplayStyle.None;

            _isVisible = false;
        }
    }
}
