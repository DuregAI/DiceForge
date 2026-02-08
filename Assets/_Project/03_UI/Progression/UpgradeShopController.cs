using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UpgradeShopController : MonoBehaviour
{
    private bool _initialized;
    private VisualElement _panel;
    private Label _coinsLabel;
    private ScrollView _list;

    public void Initialize(VisualElement root)
    {
        if (_initialized || root == null)
            return;

        _panel = root.Q<VisualElement>("UpgradeShopPanel");
        _coinsLabel = root.Q<Label>("upgradeShopCoins");
        _list = root.Q<ScrollView>("upgradeList");

        var closeButton = root.Q<Button>("btnCloseUpgrades");
        if (closeButton != null)
            closeButton.clicked += Hide;

        ProfileService.ProfileChanged -= Refresh;
        ProfileService.ProfileChanged += Refresh;
        Refresh();
        Hide();
        _initialized = true;
    }

    private void OnDestroy()
    {
        ProfileService.ProfileChanged -= Refresh;
    }

    public void Show()
    {
        if (_panel == null)
            return;

        _panel.style.display = DisplayStyle.Flex;
        _panel.RemoveFromClassList("is-visible");
        _panel.schedule.Execute(() => _panel.AddToClassList("is-visible")).ExecuteLater(1);
        Refresh();
    }

    public void Hide()
    {
        if (_panel == null)
            return;

        _panel.RemoveFromClassList("is-visible");
        _panel.style.display = DisplayStyle.None;
    }

    public void Refresh()
    {
        if (_coinsLabel != null)
            _coinsLabel.text = $"Coins: {ProfileService.GetCurrency(ProgressionIds.SoftGold)}";

        if (_list == null)
            return;

        _list.Clear();
        var catalog = UpgradeService.GetCatalog();
        if (catalog == null || catalog.upgrades == null)
            return;

        foreach (var definition in catalog.upgrades)
        {
            if (definition == null)
                continue;

            int level = UpgradeService.GetLevel(definition.upgradeId);
            int nextPrice = UpgradeService.GetNextPrice(definition.upgradeId);
            bool canBuy = UpgradeService.CanBuy(definition.upgradeId);
            bool isMax = level >= definition.maxLevel;

            var row = new VisualElement();
            row.AddToClassList("upgrade-row");

            var nameLabel = new Label(definition.displayName);
            nameLabel.AddToClassList("upgrade-name");
            row.Add(nameLabel);

            var descLabel = new Label(definition.description);
            descLabel.AddToClassList("upgrade-description");
            row.Add(descLabel);

            var metaLabel = new Label($"Level: {level}/{definition.maxLevel} | Next price: {(isMax ? "MAX" : nextPrice.ToString())}");
            metaLabel.AddToClassList("upgrade-meta");
            row.Add(metaLabel);

            var buyButton = new Button(() =>
            {
                if (UpgradeService.Buy(definition.upgradeId) == BuyResult.Success)
                    Refresh();
            })
            {
                text = isMax ? "Max Level" : "Buy"
            };

            buyButton.AddToClassList("upgrade-buy-button");
            buyButton.SetEnabled(canBuy);
            row.Add(buyButton);

            _list.Add(row);
        }
    }
}
