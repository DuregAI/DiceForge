using System;
using System.Collections.Generic;
using Diceforge.Audio;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public sealed class ChestOpenController : MonoBehaviour
{
    private const string DatabasePath = "Progression/ProgressionDatabase";
    private static readonly string[] VariantNames = { "basic", "medium", "essence" };

    private sealed class ChestGroupViewData
    {
        public string ChestTypeId;
        public int Count;
        public ChestDefinition Definition;
    }

    private ProgressionDatabase _database;
    private VisualElement _panel;
    private VisualElement _previewIcon;
    private VisualElement _previewHalo;
    private VisualElement _burstFx;
    private Label _chestInfoLabel;
    private Label _chestInfoHintLabel;
    private Label _previewCountLabel;
    private Label _queueSummaryLabel;
    private Label _rewardBurstLabel;
    private VisualElement _rewardsContainer;
    private ScrollView _rewardsList;
    private ScrollView _inventoryList;
    private Button _openButton;
    private Button _closeButton;
    private string _selectedChestTypeId;
    private AudioManager _audioManager;

    public event Action CloseRequested;

    public void Initialize(VisualElement root)
    {
        if (root == null)
            return;

        _panel = root.Q<VisualElement>("ChestOpenPanel");
        _previewIcon = root.Q<VisualElement>("chestPreviewIcon");
        _previewHalo = root.Q<VisualElement>("chestPreviewHalo");
        _burstFx = root.Q<VisualElement>("chestOpenFx");
        _chestInfoLabel = root.Q<Label>("chestInfoLabel");
        _chestInfoHintLabel = root.Q<Label>("chestInfoHint");
        _previewCountLabel = root.Q<Label>("chestPreviewCount");
        _queueSummaryLabel = root.Q<Label>("chestQueueSummary");
        _rewardBurstLabel = root.Q<Label>("chestRewardBurstLabel");
        _rewardsContainer = root.Q<VisualElement>("chestRewardsContainer");
        _rewardsList = root.Q<ScrollView>("chestRewardsList");
        _inventoryList = root.Q<ScrollView>("chestInventoryList");
        _openButton = root.Q<Button>("btnOpenChestScreen");
        _closeButton = root.Q<Button>("btnCloseChestScreen");

        if (_burstFx != null)
            _burstFx.pickingMode = PickingMode.Ignore;

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
        var queue = ProfileService.Current != null ? ProfileService.Current.chestQueue : null;
        var groups = BuildChestGroups(queue);
        int totalCount = queue != null ? queue.Count : 0;

        if (_queueSummaryLabel != null)
            _queueSummaryLabel.text = totalCount > 0 ? $"{totalCount} ready" : "0 ready";

        if (groups.Count == 0)
        {
            _selectedChestTypeId = null;
            RebuildInventory(groups);
            UpdateEmptyPreview();
            _openButton?.SetEnabled(false);
            if (_closeButton != null)
                _closeButton.text = "Close";
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedChestTypeId) || !ContainsChestType(groups, _selectedChestTypeId))
            _selectedChestTypeId = groups[0].ChestTypeId;

        RebuildInventory(groups);
        UpdatePreview(GetSelectedGroup(groups));
        _openButton?.SetEnabled(true);
        if (_closeButton != null && string.IsNullOrWhiteSpace(_closeButton.text))
            _closeButton.text = "Close";
    }

    private List<ChestGroupViewData> BuildChestGroups(List<ChestInstance> queue)
    {
        var result = new List<ChestGroupViewData>();
        if (queue == null || queue.Count == 0)
            return result;

        for (int i = 0; i < queue.Count; i++)
        {
            var chest = queue[i];
            if (chest == null || string.IsNullOrWhiteSpace(chest.chestTypeId))
                continue;

            var existing = result.Find(g => g.ChestTypeId == chest.chestTypeId);
            if (existing != null)
            {
                existing.Count++;
                continue;
            }

            result.Add(new ChestGroupViewData
            {
                ChestTypeId = chest.chestTypeId,
                Count = 1,
                Definition = FindChestDefinition(chest.chestTypeId)
            });
        }

        return result;
    }

    private bool ContainsChestType(List<ChestGroupViewData> groups, string chestTypeId)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].ChestTypeId == chestTypeId)
                return true;
        }

        return false;
    }

    private ChestGroupViewData GetSelectedGroup(List<ChestGroupViewData> groups)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].ChestTypeId == _selectedChestTypeId)
                return groups[i];
        }

        return groups.Count > 0 ? groups[0] : null;
    }

    private void UpdateEmptyPreview()
    {
        ApplyVariantToPreview("basic");

        if (_chestInfoLabel != null)
            _chestInfoLabel.text = "No chests waiting";

        if (_chestInfoHintLabel != null)
            _chestInfoHintLabel.text = "Win battles or buy chests in the shop to stock this stash.";

        if (_previewCountLabel != null)
            _previewCountLabel.text = "x0";
    }

    private void UpdatePreview(ChestGroupViewData group)
    {
        if (group == null)
        {
            UpdateEmptyPreview();
            return;
        }

        string chestName = !string.IsNullOrWhiteSpace(group.Definition != null ? group.Definition.displayName : null)
            ? group.Definition.displayName
            : group.ChestTypeId;

        if (_chestInfoLabel != null)
            _chestInfoLabel.text = chestName;

        if (_chestInfoHintLabel != null)
        {
            _chestInfoHintLabel.text = group.Count > 1
                ? $"{group.Count} in this stack. Tap the big chest or any stash card to crack one open."
                : "One chest ready. Tap the big chest or the stash card to crack it open.";
        }

        if (_previewCountLabel != null)
            _previewCountLabel.text = $"x{group.Count}";

        ApplyVariantToPreview(ResolveChestVariant(group.ChestTypeId));
    }

    private void RebuildInventory(List<ChestGroupViewData> groups)
    {
        if (_inventoryList == null)
            return;

        _inventoryList.Clear();

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            string variant = ResolveChestVariant(group.ChestTypeId);
            bool isSelected = group.ChestTypeId == _selectedChestTypeId;
            string chestName = !string.IsNullOrWhiteSpace(group.Definition != null ? group.Definition.displayName : null)
                ? group.Definition.displayName
                : group.ChestTypeId;

            var card = new Button(() => HandleInventoryChestClicked(group.ChestTypeId))
            {
                text = string.Empty
            };
            card.AddToClassList("chest-inventory-card");
            card.AddToClassList($"chest-inventory-card--{variant}");
            if (isSelected)
                card.AddToClassList("is-selected");

            var left = new VisualElement();
            left.AddToClassList("chest-inventory-left");

            var iconWrap = new VisualElement();
            iconWrap.AddToClassList("chest-inventory-icon-wrap");

            var halo = new VisualElement();
            halo.AddToClassList("chest-inventory-halo");
            halo.AddToClassList($"chest-inventory-halo--{variant}");

            var icon = new VisualElement();
            icon.AddToClassList("chest-inventory-icon");
            icon.AddToClassList($"chest-inventory-icon--{variant}");

            iconWrap.Add(halo);
            iconWrap.Add(icon);

            var copy = new VisualElement();
            copy.AddToClassList("chest-inventory-copy");

            var title = new Label(chestName);
            title.AddToClassList("chest-inventory-title");

            var subtitle = new Label(group.Count > 1 ? "Tap to open one from this stack" : "Tap to open now");
            subtitle.AddToClassList("chest-inventory-subtitle");

            copy.Add(title);
            copy.Add(subtitle);

            left.Add(iconWrap);
            left.Add(copy);

            var count = new Label($"x{group.Count}");
            count.AddToClassList("chest-inventory-count");
            count.AddToClassList($"chest-inventory-count--{variant}");

            card.Add(left);
            card.Add(count);
            _inventoryList.Add(card);
        }
    }

    private void HandleInventoryChestClicked(string chestTypeId)
    {
        if (string.IsNullOrWhiteSpace(chestTypeId))
            return;

        _selectedChestTypeId = chestTypeId;
        HandleOpenClicked();
    }

    private void HandleOpenClicked()
    {
        var queue = ProfileService.Current != null ? ProfileService.Current.chestQueue : null;
        if (queue == null || queue.Count == 0)
        {
            Refresh();
            return;
        }

        var chestToOpen = FindChestInstance(queue, _selectedChestTypeId) ?? queue[0];
        if (chestToOpen == null)
        {
            Refresh();
            return;
        }

        _selectedChestTypeId = chestToOpen.chestTypeId;

        var result = ChestService.OpenChest(chestToOpen.instanceId);
        PopulateRewards(result);
        SetRewardsVisible(true);
        TriggerBurst();
        PlayOpenSound();

        if (_closeButton != null)
            _closeButton.text = "Done";

        if (_rewardBurstLabel != null)
            _rewardBurstLabel.text = BuildBurstSummary(result);
    }

    private ChestInstance FindChestInstance(List<ChestInstance> queue, string chestTypeId)
    {
        if (queue == null || queue.Count == 0)
            return null;

        for (int i = 0; i < queue.Count; i++)
        {
            var chest = queue[i];
            if (chest != null && chest.chestTypeId == chestTypeId)
                return chest;
        }

        return null;
    }

    private void TriggerBurst()
    {
        if (_burstFx != null)
        {
            _burstFx.EnableInClassList("is-bursting", false);
            _burstFx.schedule.Execute(() => _burstFx.EnableInClassList("is-bursting", true)).ExecuteLater(1);
            _burstFx.schedule.Execute(() => _burstFx.EnableInClassList("is-bursting", false)).ExecuteLater(320);
        }

        if (_openButton != null)
        {
            _openButton.EnableInClassList("is-bursting", true);
            _openButton.schedule.Execute(() => _openButton.EnableInClassList("is-bursting", false)).ExecuteLater(260);
        }
    }

    private void PlayOpenSound()
    {
        _audioManager ??= AudioManager.Instance != null ? AudioManager.Instance : FindAnyObjectByType<AudioManager>();
        _audioManager?.PlayUiClick();
    }

    private string BuildBurstSummary(RewardBundle bundle)
    {
        if (bundle == null || bundle.IsEmpty)
            return "A quiet puff of dust.";

        int rewardCount = 0;
        if (bundle.currencies != null)
        {
            for (int i = 0; i < bundle.currencies.Count; i++)
            {
                if (bundle.currencies[i] != null && bundle.currencies[i].amount > 0)
                    rewardCount++;
            }
        }

        if (bundle.items != null)
        {
            for (int i = 0; i < bundle.items.Count; i++)
            {
                if (bundle.items[i] != null && bundle.items[i].amount > 0)
                    rewardCount++;
            }
        }

        return rewardCount > 0 ? $"Loot burst: {rewardCount} reward{(rewardCount == 1 ? string.Empty : "s")}" : "Treasure burst";
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

        if (_rewardBurstLabel != null)
            _rewardBurstLabel.text = string.Empty;

        SetRewardsVisible(false);

        if (_burstFx != null)
            _burstFx.EnableInClassList("is-bursting", false);

        if (_closeButton != null)
            _closeButton.text = "Close";

        if (_openButton != null)
            _openButton.EnableInClassList("is-bursting", false);
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
            for (int i = 0; i < bundle.currencies.Count; i++)
            {
                var currency = bundle.currencies[i];
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
            for (int i = 0; i < bundle.items.Count; i++)
            {
                var item = bundle.items[i];
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

    private void ApplyVariantToPreview(string variant)
    {
        ApplyVariantClasses(_previewIcon, "chest-preview-icon", variant);
        ApplyVariantClasses(_previewHalo, "chest-preview-halo", variant);
    }

    private void ApplyVariantClasses(VisualElement element, string baseClass, string variant)
    {
        if (element == null)
            return;

        for (int i = 0; i < VariantNames.Length; i++)
            element.RemoveFromClassList($"{baseClass}--{VariantNames[i]}");

        element.AddToClassList($"{baseClass}--{variant}");
    }

    private string ResolveChestVariant(string chestTypeId)
    {
        if (string.IsNullOrWhiteSpace(chestTypeId))
            return "basic";

        string normalized = chestTypeId.ToUpperInvariant();
        if (normalized.Contains("ESSENCE"))
            return "essence";
        if (normalized.Contains("MEDIUM") || normalized.Contains("RARE") || normalized.Contains("BIG"))
            return "medium";
        return "basic";
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





