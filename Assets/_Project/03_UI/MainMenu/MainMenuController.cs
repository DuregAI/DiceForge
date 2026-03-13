using System;
using System.Collections.Generic;
using Diceforge.Audio;
using Diceforge.Battle;
using Diceforge.Dialogue;
using Diceforge.Diagnostics;
using Diceforge.Integrations.SpacetimeDb;
using Diceforge.Map;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    private const string VisibleClass = "is-visible";
    private const float TransitionSeconds = 0.2f;
    private const string SettingsStateClass = "state-settings";
    private const string BackStateClass = "state-back";
    private const string FeedbackDefaultStatus = "Tell us what happened.";
    private static readonly List<string> FeedbackCategories = new() { "bug", "balance", "ui", "audio", "other" };

    [Header("Game Mode Presets")]
    [SerializeField] private GameModePreset longPreset;
    [SerializeField] private GameModePreset shortPreset;
    [SerializeField] private GameModePreset tutorialPreset;
    [SerializeField] private GameModePreset experimentalPreset;
    [SerializeField] private TutorialPortraitLibrary tutorialPortraitLibrary;

    private UIDocument document;
    private VisualElement root;
    private Label buildInfoLabel;
    private Label aboutVersionLabel;
    private Slider musicSlider;
    private Slider sfxSlider;
    private Button settingsButton;
    private Button copyLogButton;
    private Button openFeedbackButton;
    private Button upgradesButton;
    private VisualElement feedbackModal;
    private DropdownField feedbackCategoryField;
    private TextField feedbackMessageField;
    private Label feedbackStatusLabel;
    private Button feedbackSubmitButton;
    private Button feedbackCancelButton;
    private VisualElement copyLogTooltip;
    private readonly Dictionary<string, VisualElement> panels = new();
    private VisualElement currentPanel;
    private bool isSettingsOpen;
    private WalletPanelController walletPanelController;
    private UpgradeShopController upgradeShopController;
    private ChestOpenController chestOpenController;
    private ChestShopController chestShopController;
    private PlayerPanelController playerPanelController;
    private LevelUpWindowPresenter levelUpWindowPresenter;
    private VisualElement tutorialReplayConfirmModal;
    private Label tutorialReplayConfirmText;
    private Button tutorialReplayConfirmYesButton;
    private Button tutorialReplayConfirmCancelButton;
    private AudioManager audioManager;
    private MapFlowOrchestrator mapFlowOrchestrator;
    private bool areDevActionsVisible;

    [Header("Map")]
    [SerializeField] private string defaultChapterId = "Chapter1";

    private void Awake()
    {
        document = GetComponent<UIDocument>();
        if (document == null)
        {
            Debug.LogWarning("[MainMenu] UIDocument not found.");
            return;
        }

        root = document.rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("[MainMenu] Root VisualElement not found.");
            return;
        }

        buildInfoLabel = root.Q<Label>("lblBuildInfo");
        aboutVersionLabel = root.Q<Label>("lblAboutVersion");
        musicSlider = root.Q<Slider>("sliderMusicVolume");
        sfxSlider = root.Q<Slider>("sliderSfxVolume");
        settingsButton = root.Q<Button>("btnSettings");
        copyLogButton = root.Q<Button>("btnCopyLog");
        copyLogTooltip = root.Q<VisualElement>("copyLogTooltip");
        openFeedbackButton = root.Q<Button>("btnOpenFeedback");
        upgradesButton = root.Q<Button>("btnUpgrades");
        feedbackModal = root.Q<VisualElement>("FeedbackModal");
        feedbackCategoryField = root.Q<DropdownField>("feedbackCategoryField");
        feedbackMessageField = root.Q<TextField>("feedbackMessageField");
        feedbackStatusLabel = root.Q<Label>("feedbackStatusLabel");
        feedbackSubmitButton = root.Q<Button>("btnFeedbackSubmit");
        feedbackCancelButton = root.Q<Button>("btnFeedbackCancel");

        tutorialReplayConfirmModal = root.Q<VisualElement>("TutorialReplayConfirmModal");
        tutorialReplayConfirmText = root.Q<Label>("TutorialReplayConfirmText");
        tutorialReplayConfirmYesButton = root.Q<Button>("btnTutorialReplayYes");
        tutorialReplayConfirmCancelButton = root.Q<Button>("btnTutorialReplayCancel");

        RegisterPanel("MenuPanel");
        RegisterPanel("SettingsPanel");
        RegisterPanel("UpgradeShopPanel");
        RegisterPanel("ChestOpenPanel");
        RegisterPanel("ChestShopPanel");

        if (panels.TryGetValue("MenuPanel", out var menuPanel))
        {
            currentPanel = menuPanel;
            menuPanel.style.display = DisplayStyle.Flex;
            menuPanel.AddToClassList(VisibleClass);
        }

        RegisterButton("btnSettings", ToggleSettingsPanel);
        RegisterButton("btnCopyLog", CopyLogToClipboard);
        RegisterButton("btnLong", OpenMapChapter);
        RegisterButton("btnShort", () => SelectModeAndLoad(shortPreset));
        RegisterButton("btnTutorial", HandleTutorialSelected);
        RegisterButton("btnExperimental", () => SelectModeAndLoad(experimentalPreset));
        RegisterButton("btnUpgrades", OpenUpgradeShop);
        RegisterButton("btnCloseUpgrades", CloseUpgradeShop);
        RegisterButton("btnCloseChestShop", CloseChestShop);
        root.RegisterCallback<KeyDownEvent>(HandleRootKeyDown, TrickleDown.TrickleDown);

        walletPanelController = GetComponent<WalletPanelController>() ?? gameObject.AddComponent<WalletPanelController>();
        playerPanelController = GetComponent<PlayerPanelController>() ?? gameObject.AddComponent<PlayerPanelController>();
        levelUpWindowPresenter = GetComponent<LevelUpWindowPresenter>() ?? gameObject.AddComponent<LevelUpWindowPresenter>();

        ProfileService.Load();

        upgradeShopController = GetComponent<UpgradeShopController>() ?? gameObject.AddComponent<UpgradeShopController>();

        InitializeAboutSection();
        InitializeAudioSliders();
        InitializeCopyLogTooltip();
        InitializeFeedbackForm();
        InitializeTutorialReplayConfirmation();
        UpdateSettingsButtonState(isSettingsOpen);
        levelUpWindowPresenter.Initialize(root);

        playerPanelController.SetPortraitLibrary(tutorialPortraitLibrary);
        playerPanelController.Initialize(root);

        walletPanelController.SetLevelUpPresenter(levelUpWindowPresenter);
        walletPanelController.Initialize(root);
        walletPanelController.SetDevActionsVisible(false);
        areDevActionsVisible = false;
        walletPanelController.OpenChestScreenRequested -= OpenChestScreen;
        walletPanelController.OpenChestScreenRequested += OpenChestScreen;
        walletPanelController.OpenChestShopRequested -= OpenChestShop;
        walletPanelController.OpenChestShopRequested += OpenChestShop;

        upgradeShopController.Initialize(root);

        chestOpenController = GetComponent<ChestOpenController>() ?? gameObject.AddComponent<ChestOpenController>();
        chestOpenController.Initialize(root);
        chestOpenController.CloseRequested -= CloseChestScreen;
        chestOpenController.CloseRequested += CloseChestScreen;

        chestShopController = GetComponent<ChestShopController>() ?? gameObject.AddComponent<ChestShopController>();
        chestShopController.Initialize(root);

        ProfileService.ProfileChanged -= RefreshProgressiveUi;
        ProfileService.ProfileChanged += RefreshProgressiveUi;

        mapFlowOrchestrator = GetComponent<MapFlowOrchestrator>() ?? gameObject.AddComponent<MapFlowOrchestrator>();
        mapFlowOrchestrator.SetLevelUpPresenter(levelUpWindowPresenter);
        var mapController = GetComponent<MapController>() ?? gameObject.AddComponent<MapController>();
        mapController.ResetRunRequested -= HandleMapResetRequested;
        mapController.ResetRunRequested += HandleMapResetRequested;
        mapController.UnlockAllRequested -= HandleMapUnlockAllRequested;
        mapController.UnlockAllRequested += HandleMapUnlockAllRequested;
        mapController.BackRequested -= HandleMapBackRequested;
        mapController.BackRequested += HandleMapBackRequested;

        var shortButton = root.Q<Button>("btnShort");
        var experimentalButton = root.Q<Button>("btnExperimental");
        if (mapFlowOrchestrator != null && !mapFlowOrchestrator.IsDevMode)
        {
            if (shortButton != null)
                shortButton.style.display = DisplayStyle.None;
            if (experimentalButton != null)
                experimentalButton.style.display = DisplayStyle.None;
        }

        RefreshProgressiveUi();
    }

    private void Start()
    {
        audioManager = AudioManager.Instance != null
            ? AudioManager.Instance
            : FindAnyObjectByType<AudioManager>();

        RefreshAudioSlidersFromManager();

        audioManager?.EnsureMusicForContext(MusicContext.Menu);
        if (audioManager != null)
        {
            audioManager.OnVolumesChanged -= HandleAudioVolumesChanged;
            audioManager.OnVolumesChanged += HandleAudioVolumesChanged;
        }

        if (Diceforge.Map.MapFlowRuntime.HasPendingBattleResult || Diceforge.Map.MapFlowRuntime.ConsumeReturnToMapRequest())
            OpenMapChapter();
    }

    private void OnDestroy()
    {
        ProfileService.ProfileChanged -= RefreshProgressiveUi;

        if (chestOpenController != null)
            chestOpenController.CloseRequested -= CloseChestScreen;
        if (walletPanelController != null)
        {
            walletPanelController.OpenChestScreenRequested -= OpenChestScreen;
            walletPanelController.OpenChestShopRequested -= OpenChestShop;
        }

        if (tutorialReplayConfirmYesButton != null)
        {
            tutorialReplayConfirmYesButton.clicked -= ConfirmTutorialReplay;
        }

        if (tutorialReplayConfirmCancelButton != null)
        {
            tutorialReplayConfirmCancelButton.clicked -= CloseTutorialReplayConfirmation;
        }

        if (openFeedbackButton != null)
            openFeedbackButton.clicked -= OpenFeedbackForm;

        if (feedbackSubmitButton != null)
            feedbackSubmitButton.clicked -= SubmitFeedback;

        if (feedbackCancelButton != null)
            feedbackCancelButton.clicked -= CloseFeedbackForm;

        if (audioManager != null)
            audioManager.OnVolumesChanged -= HandleAudioVolumesChanged;

        if (musicSlider != null)
            musicSlider.UnregisterValueChangedCallback(OnMusicSliderChanged);

        if (sfxSlider != null)
            sfxSlider.UnregisterValueChangedCallback(OnSfxSliderChanged);

        if (root != null)
            root.UnregisterCallback<KeyDownEvent>(HandleRootKeyDown, TrickleDown.TrickleDown);
    }

    public void ShowPanel(string panelName)
    {
        if (!panels.TryGetValue(panelName, out var targetPanel))
        {
            Debug.LogWarning($"[MainMenu] Panel not found: {panelName}");
            return;
        }

        if (targetPanel == currentPanel)
        {
            return;
        }

        if (currentPanel != null)
        {
            HidePanel(currentPanel);
        }

        currentPanel = targetPanel;
        targetPanel.style.display = DisplayStyle.Flex;
        targetPanel.RemoveFromClassList(VisibleClass);
        targetPanel.schedule.Execute(() => targetPanel.AddToClassList(VisibleClass)).ExecuteLater(1);
        isSettingsOpen = currentPanel.name == "SettingsPanel";
        UpdateSettingsButtonState(isSettingsOpen);
    }

    public void CloseSettings()
    {
        ShowPanel("MenuPanel");
    }

    public void OpenSettings()
    {
        ShowPanel("SettingsPanel");
    }

    private void ToggleSettingsPanel()
    {
        if (isSettingsOpen)
        {
            CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }

    public void HidePanel(VisualElement panel)
    {
        panel.RemoveFromClassList(VisibleClass);
        panel.schedule.Execute(() =>
        {
            if (panel != currentPanel)
            {
                panel.style.display = DisplayStyle.None;
            }
        }).ExecuteLater(Mathf.RoundToInt(TransitionSeconds * 1000f));
    }

    private void RegisterPanel(string name)
    {
        var panel = root.Q<VisualElement>(name);
        if (panel == null)
        {
            return;
        }

        panel.style.display = DisplayStyle.None;
        panel.RemoveFromClassList(VisibleClass);
        panels[name] = panel;
    }

    private void RegisterButton(string name, Action action)
    {
        var button = root.Q<Button>(name);
        if (button == null)
        {
            return;
        }

        button.clicked += () => action?.Invoke();
    }

    private void HandleRootKeyDown(KeyDownEvent evt)
    {
        if (evt == null)
            return;

        bool isDevToggle = (evt.ctrlKey || evt.commandKey) && !evt.altKey && evt.keyCode == KeyCode.D;
        if (!isDevToggle)
            return;

        areDevActionsVisible = !areDevActionsVisible;
        walletPanelController?.SetDevActionsVisible(areDevActionsVisible);
        evt.StopPropagation();
    }

    private void RefreshProgressiveUi()
    {
        bool upgradesUnlocked = UiProgressionService.IsUpgradesUnlocked();
        if (upgradesButton != null)
            upgradesButton.style.display = upgradesUnlocked ? DisplayStyle.Flex : DisplayStyle.None;

        if (!upgradesUnlocked && currentPanel != null && currentPanel.name == "UpgradeShopPanel")
            CloseUpgradeShop();
    }

    private void UpdateSettingsButtonState(bool settingsOpen)
    {
        if (settingsButton == null)
        {
            return;
        }

        settingsButton.EnableInClassList(SettingsStateClass, !settingsOpen);
        settingsButton.EnableInClassList(BackStateClass, settingsOpen);
    }

    private void InitializeAboutSection()
    {
        if (aboutVersionLabel != null)
        {
            aboutVersionLabel.text = $"Version {Application.version}";
        }
    }

    private void InitializeCopyLogTooltip()
    {
        if (copyLogButton == null || copyLogTooltip == null)
        {
            return;
        }

        copyLogTooltip.style.display = DisplayStyle.None;
        copyLogButton.RegisterCallback<MouseEnterEvent>(_ => copyLogTooltip.style.display = DisplayStyle.Flex);
        copyLogButton.RegisterCallback<MouseLeaveEvent>(_ => copyLogTooltip.style.display = DisplayStyle.None);
    }

    private void InitializeFeedbackForm()
    {
        if (feedbackModal != null)
            feedbackModal.style.display = DisplayStyle.None;

        if (feedbackCategoryField != null)
        {
            feedbackCategoryField.choices = FeedbackCategories;
            feedbackCategoryField.index = 0;
        }

        if (feedbackMessageField != null)
        {
            feedbackMessageField.multiline = true;
            feedbackMessageField.maxLength = SpacetimeDbFeedbackSink.MaxMessageLength;
            feedbackMessageField.SetValueWithoutNotify(string.Empty);
        }

        SetFeedbackStatus(FeedbackDefaultStatus, false);

        if (openFeedbackButton != null)
            openFeedbackButton.clicked += OpenFeedbackForm;

        if (feedbackSubmitButton != null)
            feedbackSubmitButton.clicked += SubmitFeedback;

        if (feedbackCancelButton != null)
            feedbackCancelButton.clicked += CloseFeedbackForm;
    }

    private void OpenFeedbackForm()
    {
        if (feedbackModal == null)
            return;

        if (feedbackCategoryField != null)
            feedbackCategoryField.index = 0;

        if (feedbackMessageField != null)
            feedbackMessageField.SetValueWithoutNotify(string.Empty);

        SetFeedbackStatus(FeedbackDefaultStatus, false);
        feedbackModal.style.display = DisplayStyle.Flex;
        feedbackMessageField?.Focus();
    }

    private void CloseFeedbackForm()
    {
        if (feedbackModal != null)
            feedbackModal.style.display = DisplayStyle.None;
    }

    private void SubmitFeedback()
    {
        string category = feedbackCategoryField != null ? feedbackCategoryField.value : string.Empty;
        string message = feedbackMessageField != null ? feedbackMessageField.value : string.Empty;
        string trimmedMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();

        if (string.IsNullOrWhiteSpace(category))
        {
            SetFeedbackStatus("Choose a category.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(trimmedMessage))
        {
            SetFeedbackStatus("Enter feedback before submitting.", true);
            return;
        }

        if (trimmedMessage.Length > SpacetimeDbFeedbackSink.MaxMessageLength)
        {
            SetFeedbackStatus($"Feedback is limited to {SpacetimeDbFeedbackSink.MaxMessageLength} characters.", true);
            return;
        }

        SpacetimeDbLocalDevRuntime.SubmitFeedback(
            category,
            trimmedMessage,
            Application.version,
            SceneManager.GetActiveScene().name);

        if (feedbackMessageField != null)
            feedbackMessageField.SetValueWithoutNotify(string.Empty);

        SetFeedbackStatus("Feedback submitted.", false);
        Debug.Log($"[MainMenu] Feedback submitted category={category}", this);
    }

    private void SetFeedbackStatus(string message, bool isError)
    {
        if (feedbackStatusLabel == null)
            return;

        feedbackStatusLabel.text = message ?? string.Empty;
        feedbackStatusLabel.style.color = isError
            ? new StyleColor(new Color(1f, 0.68f, 0.68f, 1f))
            : new StyleColor(new Color(0.96f, 0.92f, 0.69f, 1f));
    }

    private void InitializeAudioSliders()
    {
        if (musicSlider != null)
        {
            musicSlider.RegisterValueChangedCallback(OnMusicSliderChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.RegisterValueChangedCallback(OnSfxSliderChanged);
        }

        RefreshAudioSlidersFromManager();
    }

    private void OnMusicSliderChanged(ChangeEvent<float> evt)
    {
        audioManager ??= AudioManager.Instance != null
            ? AudioManager.Instance
            : FindAnyObjectByType<AudioManager>();

        audioManager?.SetMusicVolume(evt.newValue);
    }

    private void OnSfxSliderChanged(ChangeEvent<float> evt)
    {
        audioManager ??= AudioManager.Instance != null
            ? AudioManager.Instance
            : FindAnyObjectByType<AudioManager>();

        audioManager?.SetSfxVolume(evt.newValue);
    }

    private void HandleAudioVolumesChanged(float musicVolume, float sfxVolume)
    {
        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(musicVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(sfxVolume);
    }

    private void RefreshAudioSlidersFromManager()
    {
        audioManager ??= AudioManager.Instance != null
            ? AudioManager.Instance
            : FindAnyObjectByType<AudioManager>();

        if (audioManager == null)
            return;

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(audioManager.MusicVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(audioManager.SfxVolume);
    }

    private void CopyLogToClipboard()
    {
        var buildInfo = buildInfoLabel != null ? buildInfoLabel.text : "Build: unknown";
        var logPayload = ClientDiagnostics.BuildSupportLog(buildInfo);
        GUIUtility.systemCopyBuffer = logPayload;
        Debug.Log("[MainMenu] Log copied", this);
    }

    private void OpenUpgradeShop()
    {
        if (!UiProgressionService.IsUpgradesUnlocked())
            return;

        ShowPanel("UpgradeShopPanel");
        upgradeShopController?.Show();
    }

    private void CloseUpgradeShop()
    {
        upgradeShopController?.Hide();
        ShowPanel("MenuPanel");
    }

    private void OpenChestScreen()
    {
        if (!UiProgressionService.IsChestSectionUnlocked())
            return;

        ShowPanel("ChestOpenPanel");
        chestOpenController?.Show();
    }

    private void CloseChestScreen()
    {
        chestOpenController?.Hide();
        ShowPanel("MenuPanel");
    }

    private void OpenChestShop()
    {
        if (!UiProgressionService.IsChestSectionUnlocked())
            return;

        ShowPanel("ChestShopPanel");
        chestShopController?.Show();
    }

    private void CloseChestShop()
    {
        chestShopController?.Hide();
        ShowPanel("MenuPanel");
    }

    private void HandleTutorialSelected()
    {
        if (!TutorialFlow.RequiresReplayConfirmation())
        {
            TutorialFlow.EnterTutorial(tutorialPreset);
            return;
        }

        OpenTutorialReplayConfirmation();
    }

    private void InitializeTutorialReplayConfirmation()
    {
        if (tutorialReplayConfirmModal == null)
        {
            return;
        }

        tutorialReplayConfirmModal.style.display = DisplayStyle.None;
        if (tutorialReplayConfirmText != null)
        {
            tutorialReplayConfirmText.text = TutorialFlow.ReplayConfirmationText;
        }

        if (tutorialReplayConfirmYesButton != null)
        {
            tutorialReplayConfirmYesButton.clicked += ConfirmTutorialReplay;
        }

        if (tutorialReplayConfirmCancelButton != null)
        {
            tutorialReplayConfirmCancelButton.clicked += CloseTutorialReplayConfirmation;
        }
    }

    private void OpenTutorialReplayConfirmation()
    {
        if (tutorialReplayConfirmModal == null)
        {
            TutorialFlow.EnterTutorial(tutorialPreset);
            return;
        }

        tutorialReplayConfirmModal.style.display = DisplayStyle.Flex;
    }

    private void CloseTutorialReplayConfirmation()
    {
        if (tutorialReplayConfirmModal != null)
        {
            tutorialReplayConfirmModal.style.display = DisplayStyle.None;
        }
    }

    private void ConfirmTutorialReplay()
    {
        CloseTutorialReplayConfirmation();
        TutorialFlow.EnterTutorial(tutorialPreset);
    }

    private void SelectModeAndLoad(GameModePreset preset)
    {
        if (preset == null)
            throw new System.InvalidOperationException("[MainMenu] Legacy start failed: preset is not assigned.");

        if (preset.mapConfig == null)
            throw new System.InvalidOperationException($"[MainMenu] Legacy start failed: map override is not assigned for preset '{preset.name}' modeId='{preset.modeId}'.");

        Debug.Log($"[MainMenu] Starting LEGACY button through BattleLauncher preset={preset.name} map={preset.mapConfig.name}");
        BattleLauncher.Start(new BattleStartRequest(preset, preset.mapConfig));
    }

    private void StartNewBattle(GameModePreset preset, Diceforge.MapSystem.BattleMapConfig mapOverride)
    {
        if (preset == null)
            throw new System.InvalidOperationException("[MainMenu] New start failed: preset is not assigned.");

        if (mapOverride == null)
        {
            throw new System.InvalidOperationException($"[MainMenu] New start failed: map override is not assigned for preset '{preset.name}' modeId='{preset.modeId}'.");
        }

        Debug.Log($"[MainMenu] Starting NEW battle pipeline with preset: {preset.name} ({preset.modeId}) mapOverride={mapOverride.name}");
        BattleLauncher.Start(new BattleStartRequest(preset, mapOverride));
    }

    private void OpenMapChapter()
    {
        ShowPanel("MenuPanel");
        mapFlowOrchestrator?.StartChapter(defaultChapterId);
    }

    private void HandleMapResetRequested()
    {
        mapFlowOrchestrator?.ResetRun();
    }

    private void HandleMapUnlockAllRequested()
    {
        mapFlowOrchestrator?.UnlockAll();
    }

    private void HandleMapBackRequested()
    {
        var mapController = GetComponent<MapController>();
        mapController?.Hide();
    }
}
