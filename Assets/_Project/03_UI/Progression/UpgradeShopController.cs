using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class UpgradeShopController : MonoBehaviour
{
    private readonly struct UpgradeVisualProfile
    {
        public readonly string Variant;
        public readonly string BadgeText;

        public UpgradeVisualProfile(string variant, string badgeText = null)
        {
            Variant = variant;
            BadgeText = badgeText;
        }
    }

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
            _coinsLabel.text = ProfileService.GetCurrency(ProgressionIds.SoftGold).ToString();

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

            _list.Add(BuildUpgradeCard(definition));
        }
    }

    private VisualElement BuildUpgradeCard(UpgradeDefinition definition)
    {
        int level = UpgradeService.GetLevel(definition.upgradeId);
        int nextLevel = Mathf.Min(level + 1, definition.maxLevel);
        int nextPrice = UpgradeService.GetNextPrice(definition.upgradeId);
        bool canBuy = UpgradeService.CanBuy(definition.upgradeId);
        bool isMax = level >= definition.maxLevel;
        var visual = GetVisualProfile(definition.effectType);

        var card = new VisualElement();
        card.AddToClassList("upgrade-card");
        card.AddToClassList($"upgrade-card--{visual.Variant}");

        var left = new VisualElement();
        left.AddToClassList("upgrade-card-left");

        var iconWrap = new VisualElement();
        iconWrap.AddToClassList("upgrade-card-icon-wrap");

        var halo = new VisualElement();
        halo.AddToClassList("upgrade-card-icon-halo");
        halo.AddToClassList($"upgrade-card-icon-halo--{visual.Variant}");

        var icon = new VisualElement();
        icon.AddToClassList("upgrade-card-icon");
        icon.AddToClassList($"upgrade-card-icon--{visual.Variant}");

        iconWrap.Add(halo);
        iconWrap.Add(icon);

        if (!string.IsNullOrWhiteSpace(visual.BadgeText))
        {
            var badge = new Label(visual.BadgeText);
            badge.AddToClassList("upgrade-card-mode-badge");
            iconWrap.Add(badge);
        }

        var copy = new VisualElement();
        copy.AddToClassList("upgrade-card-copy");

        var title = new Label(definition.displayName);
        title.AddToClassList("upgrade-card-title");

        var description = new Label(definition.description);
        description.AddToClassList("upgrade-card-description");

        var chips = new VisualElement();
        chips.AddToClassList("upgrade-card-chip-row");
        chips.Add(BuildChip($"Lv {level}/{definition.maxLevel}", "upgrade-card-chip upgrade-card-chip-level"));
        chips.Add(BuildChip(isMax ? $"Current {FormatValue(definition, level)}" : $"Next {FormatValue(definition, nextLevel)}", "upgrade-card-chip upgrade-card-chip-value"));

        copy.Add(title);
        copy.Add(description);
        copy.Add(chips);

        left.Add(iconWrap);
        left.Add(copy);

        var right = new VisualElement();
        right.AddToClassList("upgrade-card-right");

        var priceRow = new VisualElement();
        priceRow.AddToClassList("upgrade-card-price");

        var priceIcon = new VisualElement();
        priceIcon.AddToClassList("upgrade-card-price-icon");

        var priceLabel = new Label(isMax ? "MAX" : nextPrice.ToString());
        priceLabel.AddToClassList("upgrade-card-price-label");
        if (isMax)
            priceLabel.AddToClassList("is-max");

        priceRow.Add(priceIcon);
        priceRow.Add(priceLabel);

        var button = new Button(() =>
        {
            if (UpgradeService.Buy(definition.upgradeId) == BuyResult.Success)
                Refresh();
        })
        {
            text = isMax ? "Maxed" : "Buy"
        };

        button.AddToClassList("upgrade-card-buy-button");
        button.SetEnabled(canBuy);

        right.Add(priceRow);
        right.Add(button);

        card.Add(left);
        card.Add(right);
        return card;
    }

    private VisualElement BuildChip(string text, string classNames)
    {
        var chip = new Label(text);
        foreach (var className in classNames.Split(' '))
        {
            if (!string.IsNullOrWhiteSpace(className))
                chip.AddToClassList(className);
        }

        return chip;
    }

    private static UpgradeVisualProfile GetVisualProfile(UpgradeEffectType effectType)
    {
        return effectType switch
        {
            UpgradeEffectType.AddGoldOnWin => new UpgradeVisualProfile("gold-win"),
            UpgradeEffectType.AddGoldOnLoss => new UpgradeVisualProfile("gold-loss"),
            UpgradeEffectType.AddEssencePerMatch => new UpgradeVisualProfile("essence"),
            UpgradeEffectType.AddShardsOnWin => new UpgradeVisualProfile("shards"),
            UpgradeEffectType.AddChestChanceOnWin => new UpgradeVisualProfile("chest"),
            UpgradeEffectType.MultiplyXp => new UpgradeVisualProfile("xp"),
            UpgradeEffectType.MultiplyGoldIfLong => new UpgradeVisualProfile("gold-long", "LONG"),
            UpgradeEffectType.MultiplyGoldIfShort => new UpgradeVisualProfile("gold-short", "SHORT"),
            _ => new UpgradeVisualProfile("gold-win")
        };
    }

    private static string FormatValue(UpgradeDefinition definition, int level)
    {
        if (definition == null || level <= 0)
            return "+0";

        float value = definition.GetValueForLevel(level);
        return definition.effectType switch
        {
            UpgradeEffectType.AddGoldOnWin => $"+{Mathf.RoundToInt(value)} gold",
            UpgradeEffectType.AddGoldOnLoss => $"+{Mathf.RoundToInt(value)} gold",
            UpgradeEffectType.AddEssencePerMatch => $"+{Mathf.RoundToInt(value)} essence",
            UpgradeEffectType.AddShardsOnWin => $"+{Mathf.RoundToInt(value)} shards",
            UpgradeEffectType.AddChestChanceOnWin => $"+{Mathf.RoundToInt(value * 100f)}% drop",
            UpgradeEffectType.MultiplyXp => $"+{Mathf.RoundToInt(value * 100f)}% XP",
            UpgradeEffectType.MultiplyGoldIfLong => $"+{Mathf.RoundToInt(value * 100f)}% long",
            UpgradeEffectType.MultiplyGoldIfShort => $"+{Mathf.RoundToInt(value * 100f)}% short",
            _ => $"+{value:0.#}"
        };
    }
}
