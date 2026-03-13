using System;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class WalletPanelController : MonoBehaviour
{
    private const int DevXpAmount = 50;
    private static readonly LevelUpUnlockInfo[] DebugMultiUnlocks =
    {
        new("chest_shop", "Chest Shop"),
        new("upgrades", "Upgrades"),
        new("milestone_rewards", "Milestone Rewards")
    };

    public event Action OpenChestScreenRequested;
    public event Action OpenChestShopRequested;

    private bool _initialized;
    private LevelUpWindowPresenter _levelUpPresenter;
    private Label _coinsLabel;
    private Label _essenceLabel;
    private Label _shardsLabel;
    private Label _xpProgressLabel;
    private Label _chestsLabel;
    private Label _chestResultLabel;
    private VisualElement _xpFillElement;
    private VisualElement _chestSectionElement;
    private VisualElement _devActionsElement;
    private Button _addXpButton;
    private Button _addCoinsButton;
    private Button _resetProfileButton;
    private Button _openChestButton;
    private Button _debugLevelUpButton;
    private Button _debugLevelUpUnlockButton;
    private Button _debugLevelUpMultiButton;

    public bool AreDevActionsVisible => _devActionsElement != null && _devActionsElement.style.display != DisplayStyle.None;

    public void SetLevelUpPresenter(LevelUpWindowPresenter presenter)
    {
        _levelUpPresenter = presenter;
    }

    public void Initialize(VisualElement root)
    {
        if (root == null || _initialized)
            return;

        _coinsLabel = root.Q<Label>("walletCoins");
        _essenceLabel = root.Q<Label>("walletEssence");
        _shardsLabel = root.Q<Label>("walletShards");
        _xpProgressLabel = root.Q<Label>("walletXpProgress");
        _xpFillElement = root.Q<VisualElement>("walletXpFill");
        _chestsLabel = root.Q<Label>("walletChests");
        _chestResultLabel = root.Q<Label>("walletChestResult");
        _chestSectionElement = root.Q<VisualElement>("walletChestSection");
        _devActionsElement = root.Q<VisualElement>("walletDevActions");

        _addXpButton = root.Q<Button>("btnWalletAddXp");
        _addCoinsButton = root.Q<Button>("btnWalletAddCoins");
        _resetProfileButton = root.Q<Button>("btnWalletReset");
        _openChestButton = root.Q<Button>("btnWalletOpenChest");
        _debugLevelUpButton = root.Q<Button>("btnWalletDebugLevelUp");
        _debugLevelUpUnlockButton = root.Q<Button>("btnWalletDebugLevelUnlock");
        _debugLevelUpMultiButton = root.Q<Button>("btnWalletDebugLevelMulti");

        if (_addXpButton != null)
            _addXpButton.clicked += HandleAddXpClicked;
        if (_addCoinsButton != null)
            _addCoinsButton.clicked += HandleAddCoinsClicked;
        if (_resetProfileButton != null)
            _resetProfileButton.clicked += HandleResetClicked;
        if (_openChestButton != null)
            _openChestButton.clicked += HandleOpenChestClicked;
        if (_debugLevelUpButton != null)
            _debugLevelUpButton.clicked += HandleDebugLevelUpClicked;
        if (_debugLevelUpUnlockButton != null)
            _debugLevelUpUnlockButton.clicked += HandleDebugLevelUpUnlockClicked;
        if (_debugLevelUpMultiButton != null)
            _debugLevelUpMultiButton.clicked += HandleDebugLevelUpMultiClicked;

        SetDevActionsVisible(false);

        ProfileService.ProfileChanged -= Refresh;
        ProfileService.ProfileChanged += Refresh;
        Refresh();
        _initialized = true;
    }

    public void SetDevActionsVisible(bool visible)
    {
        if (_devActionsElement == null)
            return;

        _devActionsElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnDestroy()
    {
        ProfileService.ProfileChanged -= Refresh;

        if (_addXpButton != null)
            _addXpButton.clicked -= HandleAddXpClicked;
        if (_addCoinsButton != null)
            _addCoinsButton.clicked -= HandleAddCoinsClicked;
        if (_resetProfileButton != null)
            _resetProfileButton.clicked -= HandleResetClicked;
        if (_openChestButton != null)
            _openChestButton.clicked -= HandleOpenChestClicked;
        if (_debugLevelUpButton != null)
            _debugLevelUpButton.clicked -= HandleDebugLevelUpClicked;
        if (_debugLevelUpUnlockButton != null)
            _debugLevelUpUnlockButton.clicked -= HandleDebugLevelUpUnlockClicked;
        if (_debugLevelUpMultiButton != null)
            _debugLevelUpMultiButton.clicked -= HandleDebugLevelUpMultiClicked;
    }

    private void HandleAddXpClicked()
    {
        LevelUpPresentationData levelUpData = ProfileService.AddXp(DevXpAmount, LevelUpSourceContexts.Debug);
        ShowLevelUp(levelUpData);
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
        int chestCount = ProfileService.Current.chestQueue?.Count ?? 0;
        if (chestCount > 0)
        {
            OpenChestScreenRequested?.Invoke();
            return;
        }

        OpenChestShopRequested?.Invoke();
    }

    private void HandleDebugLevelUpClicked()
    {
        ShowLevelUp(LevelUpProgressionService.CreateDebugSample(
            previousLevel: 2,
            newLevel: 3,
            unlocks: Array.Empty<LevelUpUnlockInfo>(),
            flavorText: "Your forge burns brighter."));
    }

    private void HandleDebugLevelUpUnlockClicked()
    {
        ShowLevelUp(LevelUpProgressionService.CreateDebugSample(
            previousLevel: 2,
            newLevel: 3,
            unlocks: new[] { new LevelUpUnlockInfo("chest_shop", "Chest Shop") },
            flavorText: "A new path opens through the embers."));
    }

    private void HandleDebugLevelUpMultiClicked()
    {
        ShowLevelUp(LevelUpProgressionService.CreateDebugSample(
            previousLevel: 3,
            newLevel: 5,
            unlocks: DebugMultiUnlocks,
            flavorText: "The forge answers with a brighter chorus."));
    }

    private void ShowLevelUp(LevelUpPresentationData data)
    {
        if (data == null)
            return;

        _levelUpPresenter?.Show(data);
    }

    private void Refresh()
    {
        if (_coinsLabel != null)
            _coinsLabel.text = ProfileService.GetCurrency(ProgressionIds.SoftGold).ToString();
        if (_essenceLabel != null)
            _essenceLabel.text = ProfileService.GetCurrency(ProgressionIds.Essence).ToString();
        if (_shardsLabel != null)
            _shardsLabel.text = ProfileService.GetCurrency(ProgressionIds.Shards).ToString();

        int xp = Mathf.Max(0, ProfileService.Current.hero.xp);
        int levelXp = xp % UiProgressionService.XpPerLevel;
        float xpProgress = Mathf.Clamp01(levelXp / (float)UiProgressionService.XpPerLevel);

        if (_xpProgressLabel != null)
            _xpProgressLabel.text = $"{levelXp}/{UiProgressionService.XpPerLevel}";
        if (_xpFillElement != null)
            _xpFillElement.style.width = Length.Percent(xpProgress * 100f);

        bool chestSectionUnlocked = UiProgressionService.IsChestSectionUnlocked();
        if (_chestSectionElement != null)
            _chestSectionElement.style.display = chestSectionUnlocked ? DisplayStyle.Flex : DisplayStyle.None;

        int chestCount = ProfileService.Current.chestQueue?.Count ?? 0;
        if (_chestsLabel != null)
            _chestsLabel.text = chestCount > 0 ? chestCount.ToString() : "+";

        if (_chestResultLabel != null)
            _chestResultLabel.style.display = DisplayStyle.None;
    }
}
