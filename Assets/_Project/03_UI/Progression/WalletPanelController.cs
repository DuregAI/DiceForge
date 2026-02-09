using System;
using System.Text;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class WalletPanelController : MonoBehaviour
{
    public event Action OpenChestScreenRequested;
    private bool _initialized;
    private Label _coinsLabel;
    private Label _essenceLabel;
    private Label _shardsLabel;
    private Label _xpLabel;
    private Label _chestsLabel;
    private Label _chestResultLabel;
    private Button _addCoinsButton;
    private Button _resetProfileButton;
    private Button _openChestButton;

    public void Initialize(VisualElement root)
    {
        if (root == null)
            return;

        if (_initialized)
            return;

        _coinsLabel = root.Q<Label>("walletCoins");
        _essenceLabel = root.Q<Label>("walletEssence");
        _shardsLabel = root.Q<Label>("walletShards");
        _xpLabel = root.Q<Label>("walletXp");
        _chestsLabel = root.Q<Label>("walletChests");
        _chestResultLabel = root.Q<Label>("walletChestResult");

        _addCoinsButton = root.Q<Button>("btnWalletAddCoins");
        _resetProfileButton = root.Q<Button>("btnWalletReset");
        _openChestButton = root.Q<Button>("btnWalletOpenChest");

        if (_addCoinsButton != null)
            _addCoinsButton.clicked += HandleAddCoinsClicked;
        if (_resetProfileButton != null)
            _resetProfileButton.clicked += HandleResetClicked;
        if (_openChestButton != null)
            _openChestButton.clicked += HandleOpenChestClicked;

        ProfileService.ProfileChanged -= Refresh;
        ProfileService.ProfileChanged += Refresh;
        Refresh();
        _initialized = true;
    }

    private void OnDestroy()
    {
        ProfileService.ProfileChanged -= Refresh;

        if (_addCoinsButton != null)
            _addCoinsButton.clicked -= HandleAddCoinsClicked;
        if (_resetProfileButton != null)
            _resetProfileButton.clicked -= HandleResetClicked;
        if (_openChestButton != null)
            _openChestButton.clicked -= HandleOpenChestClicked;
    }

    private void HandleAddCoinsClicked()
    {
        ProfileService.AddCurrency(ProgressionIds.SoftGold, 100);
    }

    private void HandleResetClicked()
    {
        ProfileService.ResetProfile();
    }

    private void HandleOpenChestClicked()
    {
        OpenChestScreenRequested?.Invoke();
    }

    private void Refresh()
    {
        if (_coinsLabel != null) _coinsLabel.text = $"Coins: {ProfileService.GetCurrency(ProgressionIds.SoftGold)}";
        if (_essenceLabel != null) _essenceLabel.text = $"Essence: {ProfileService.GetCurrency(ProgressionIds.Essence)}";
        if (_shardsLabel != null) _shardsLabel.text = $"Shards: {ProfileService.GetCurrency(ProgressionIds.Shards)}";
        if (_xpLabel != null) _xpLabel.text = $"Hero XP: {ProfileService.Current.hero.xp}";

        int chestCount = ProfileService.Current.chestQueue?.Count ?? 0;
        if (_chestsLabel != null) _chestsLabel.text = $"Chests: {chestCount}";

        if (_openChestButton != null)
            _openChestButton.SetEnabled(chestCount > 0);
    }

    private static string BuildRewardText(RewardBundle bundle)
    {
        if (bundle == null || bundle.IsEmpty)
            return "No rewards";

        var builder = new StringBuilder("Chest: ");
        bool hasAny = false;

        if (bundle.currencies != null)
        {
            foreach (var c in bundle.currencies)
            {
                if (c == null || c.amount <= 0)
                    continue;
                if (hasAny) builder.Append(", ");
                builder.Append($"+{c.amount} {c.id}");
                hasAny = true;
            }
        }

        if (bundle.items != null)
        {
            foreach (var i in bundle.items)
            {
                if (i == null || i.amount <= 0)
                    continue;
                if (hasAny) builder.Append(", ");
                builder.Append($"+{i.amount} {i.id}");
                hasAny = true;
            }
        }

        if (!hasAny)
            return "Chest opened";

        return builder.ToString();
    }
}
