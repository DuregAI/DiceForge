using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChestShopController : MonoBehaviour
{
    private VisualElement _panel;
    private Label _statusLabel;

    public void Initialize(VisualElement root)
    {
        if (root == null)
            return;

        _panel = root.Q<VisualElement>("ChestShopPanel");
        _statusLabel = root.Q<Label>("chestShopStatus");

        RegisterBuyButton(root, "btnBuySmallChest", ProgressionIds.SoftGold, 50, ProgressionIds.BasicChest, "Small Chest purchased");
        RegisterBuyButton(root, "btnBuyMediumChest", ProgressionIds.SoftGold, 120, ProgressionIds.BasicChest, "Medium Chest purchased");
        RegisterBuyButton(root, "btnBuyEssenceChest", ProgressionIds.Essence, 10, ProgressionIds.BasicChest, "Essence Chest purchased");
    }

    public void Show()
    {
        if (_statusLabel != null)
            _statusLabel.text = string.Empty;
    }

    public void Hide()
    {
    }

    private void RegisterBuyButton(VisualElement root, string buttonName, string currencyId, int cost, string chestId, string successText)
    {
        var button = root.Q<Button>(buttonName);
        if (button == null)
            return;

        button.clicked += () => TryBuyChest(currencyId, cost, chestId, successText);
    }

    private void TryBuyChest(string currencyId, int cost, string chestId, string successText)
    {
        if (!ProfileService.SpendCurrency(currencyId, cost))
        {
            SetStatus("Not enough currency.");
            return;
        }

        var chest = ChestService.CreateChestInstance(chestId);
        if (chest == null)
        {
            ProfileService.AddCurrency(currencyId, cost);
            SetStatus("Failed to create chest.");
            return;
        }

        ProfileService.AddChest(chest);
        SetStatus(successText);
    }

    private void SetStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.text = message;
    }
}
