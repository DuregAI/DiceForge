using System;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public sealed class ChestOpenController : MonoBehaviour
{
    private const string DatabasePath = "Progression/ProgressionDatabase";

    private ProgressionDatabase _database;
    private VisualElement _panel;
    private VisualElement _chestIcon;
    private Label _chestInfoLabel;
    private VisualElement _rewardsContainer;
    private ScrollView _rewardsList;
    private Button _openButton;
    private Button _closeButton;

    public event Action CloseRequested;

    public void Initialize(VisualElement root)
    {
        if (root == null)
            return;

        _panel = root.Q<VisualElement>("ChestOpenPanel");
        _chestIcon = root.Q<VisualElement>("chestIcon");
        _chestInfoLabel = root.Q<Label>("chestInfoLabel");
        _rewardsContainer = root.Q<VisualElement>("chestRewardsContainer");
        _rewardsList = root.Q<ScrollView>("chestRewardsList");
        _openButton = root.Q<Button>("btnOpenChestScreen");
        _closeButton = root.Q<Button>("btnCloseChestScreen");

        if (_openButton != null)
            _openButton.clicked += HandleOpenClicked;
        if (_closeButton != null)
            _closeButton.clicked += HandleCloseClicked;

        ProfileService.ProfileChanged -= Refresh;
        ProfileService.ProfileChanged += Refresh;

        Hide();
    }

    private void OnDestroy()
    {
        ProfileService.ProfileChanged -= Refresh;

        if (_openButton != null)
            _openButton.clicked -= HandleOpenClicked;
        if (_closeButton != null)
            _closeButton.clicked -= HandleCloseClicked;
    }

    public void Show()
    {
        if (_panel == null)
            return;

        _panel.style.display = DisplayStyle.Flex;
        Refresh();
    }

    public void Hide()
    {
        if (_panel == null)
            return;

        ResetVisualState();
        _panel.style.display = DisplayStyle.None;
    }

    private void Refresh()
    {
        var queue = ProfileService.Current.chestQueue;
        bool hasChest = queue != null && queue.Count > 0;

        if (!hasChest)
        {
            if (_chestInfoLabel != null)
                _chestInfoLabel.text = "No chests available";

            SetIcon(null);
            SetRewardsVisible(false);
            _openButton?.SetEnabled(false);
            if (_closeButton != null)
                _closeButton.text = "Close";
            return;
        }

        var chest = queue[0];
        var definition = FindChestDefinition(chest.chestTypeId);
        var chestName = !string.IsNullOrWhiteSpace(definition != null ? definition.displayName : null)
            ? definition.displayName
            : chest.chestTypeId;

        if (_chestInfoLabel != null)
            _chestInfoLabel.text = $"{chestName} ({chest.chestTypeId})";

        SetIcon(definition != null ? definition.icon : null);
        _openButton?.SetEnabled(true);
        if (_closeButton != null)
            _closeButton.text = "Close";
    }

    private void HandleOpenClicked()
    {
        var queue = ProfileService.Current.chestQueue;
        if (queue == null || queue.Count == 0)
        {
            Refresh();
            return;
        }

        var result = ChestService.OpenChest(queue[0].instanceId);
        PopulateRewards(result);

        SetRewardsVisible(true);
        _openButton?.SetEnabled(false);
        if (_closeButton != null)
            _closeButton.text = "OK";
    }

    private void HandleCloseClicked()
    {
        Hide();

        if (CloseRequested != null)
        {
            CloseRequested.Invoke();
            return;
        }

        SceneManager.LoadScene("MainMenu");
    }

    private void ResetVisualState()
    {
        if (_rewardsList != null)
            _rewardsList.Clear();

        SetRewardsVisible(false);

        if (_closeButton != null)
            _closeButton.text = "Close";
    }

    private void SetRewardsVisible(bool visible)
    {
        if (_rewardsContainer == null)
            return;

        _rewardsContainer.EnableInClassList("reward-hidden", !visible);
        _rewardsContainer.EnableInClassList("reward-visible", visible);
        _rewardsContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void PopulateRewards(RewardBundle bundle)
    {
        if (_rewardsList == null)
            return;

        _rewardsList.Clear();

        if (bundle == null || bundle.IsEmpty)
        {
            _rewardsList.Add(new Label("No rewards"));
            return;
        }

        if (bundle.currencies != null)
        {
            foreach (var currency in bundle.currencies)
            {
                if (currency == null || currency.amount <= 0)
                    continue;

                var definition = FindCurrency(currency.id);
                var name = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName
                    : $"Unknown ({currency.id})";
                AddRewardRow(definition != null ? definition.icon : null, $"+{currency.amount}", name);
            }
        }

        if (bundle.items != null)
        {
            foreach (var item in bundle.items)
            {
                if (item == null || item.amount <= 0)
                    continue;

                var definition = FindItem(item.id);
                var name = definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName
                    : $"Unknown ({item.id})";
                AddRewardRow(definition != null ? definition.icon : null, $"x{item.amount}", name);
            }
        }
    }

    private void AddRewardRow(Sprite icon, string amount, string name)
    {
        var row = new VisualElement();
        row.AddToClassList("chest-reward-row");

        var iconElement = new VisualElement();
        iconElement.AddToClassList("chest-reward-icon");
        if (icon != null)
            iconElement.style.backgroundImage = new StyleBackground(icon);

        var amountLabel = new Label(amount);
        amountLabel.AddToClassList("chest-reward-amount");

        var nameLabel = new Label(name);
        nameLabel.AddToClassList("chest-reward-name");

        row.Add(iconElement);
        row.Add(amountLabel);
        row.Add(nameLabel);
        _rewardsList.Add(row);
    }

    private void SetIcon(Sprite icon)
    {
        if (_chestIcon == null)
            return;

        if (icon != null)
            _chestIcon.style.backgroundImage = new StyleBackground(icon);
        else
            _chestIcon.style.backgroundImage = new StyleBackground((Texture2D)null);
    }

    private ChestDefinition FindChestDefinition(string chestTypeId)
    {
        var db = GetDatabase();
        if (db == null || db.chestCatalog == null || db.chestCatalog.chests == null)
            return null;

        return db.chestCatalog.chests.Find(c => c != null && c.id == chestTypeId);
    }

    private CurrencyDefinition FindCurrency(string currencyId)
    {
        var db = GetDatabase();
        if (db == null || db.currencyCatalog == null || db.currencyCatalog.currencies == null)
            return null;

        return db.currencyCatalog.currencies.Find(c => c != null && c.id == currencyId);
    }

    private ItemDefinition FindItem(string itemId)
    {
        var db = GetDatabase();
        if (db == null || db.itemCatalog == null || db.itemCatalog.items == null)
            return null;

        return db.itemCatalog.items.Find(i => i != null && i.id == itemId);
    }

    private ProgressionDatabase GetDatabase()
    {
        _database ??= Resources.Load<ProgressionDatabase>(DatabasePath);
        return _database;
    }
}
