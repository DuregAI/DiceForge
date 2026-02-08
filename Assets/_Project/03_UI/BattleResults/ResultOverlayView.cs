using System.Text;
using Diceforge.Core;
using Diceforge.Progression;
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
        private Label _rewardBreakdownLabel;
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
            _rewardBreakdownLabel = root.Q<Label>("rewardBreakdownLabel");
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

            var rewards = RewardService.CalculateMatchRewards(result, string.Empty);
            ProfileService.ApplyReward(rewards);

            if (_rewardBreakdownLabel != null)
                _rewardBreakdownLabel.text = BuildRewardBreakdown(rewards);

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

        private static string BuildRewardBreakdown(RewardBundle bundle)
        {
            if (bundle == null || bundle.IsEmpty)
                return "No rewards";

            var sb = new StringBuilder();

            if (bundle.currencies != null)
            {
                foreach (var currency in bundle.currencies)
                {
                    if (currency == null || currency.amount <= 0)
                        continue;

                    if (sb.Length > 0)
                        sb.Append('\n');
                    sb.Append($"+{currency.amount} {currency.id}");
                }
            }

            if (bundle.items != null)
            {
                foreach (var item in bundle.items)
                {
                    if (item == null || item.amount <= 0)
                        continue;

                    if (sb.Length > 0)
                        sb.Append('\n');
                    sb.Append($"+{item.amount} {item.id}");
                }
            }

            if (bundle.xp > 0)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append($"+{bundle.xp} XP");
            }

            if (bundle.chests != null && bundle.chests.Count > 0)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append($"+Chest x{bundle.chests.Count}");
            }

            return sb.Length == 0 ? "No rewards" : sb.ToString();
        }
    }
}
