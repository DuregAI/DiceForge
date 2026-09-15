using System;
using Diceforge.Transitions;
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
    private MenuAmbientView ambientView;
    private MenuLocalization localization;
    private JoTutorialView joTutorial;
    private string feedbackStatusSource = FeedbackDefaultStatus;
    private bool feedbackStatusError;
    private string T(string source) => localization?.T(source) ?? source;
    private readonly List<IDisposable> modalBindings = new();
    private readonly Dictionary<VisualElement, IVisualElementScheduledItem> panelHideJobs = new();
    [Header("Menu atmosphere")]
    [SerializeField] private MenuAmbientSettings atmosphere = new MenuAmbientSettings();
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
    private readonly Dictionary<string, VisualElement> panels = new();
    private VisualElement currentPanel;
    private bool isSettingsOpen;
    private WalletPanelController walletPanelController;
    private UpgradeShopController upgradeShopController;
    private ChestOpenController chestOpenController;
    private ChestShopController chestShopController;
    private PlayerPanelController playerPanelController;
    private LevelUpWindowPresenter levelUpWindowPresenter;
    private ChestRewardWindowPresenter chestRewardWindowPresenter;
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
        RegisterPanel("LegalPanel");
        RegisterPanel("JoTutorialPanel");
        RegisterPanel("UpgradeShopPanel");
        RegisterPanel("ChestOpenPanel");
        RegisterPanel("ChestShopPanel");
        RegisterButton("btnMyGoblins", OpenMenuProfile);
        RegisterButton("btnMenuProfile", OpenMenuProfile);
        RegisterButton("btnInventory", () => root.Q("LeftSidebar")?.ToggleInClassList("inventory-open"));
        RegisterButton("btnCloseInventory", CloseInventory);
        RegisterButton("btnMenuAudio", ToggleMenuAudio);
        RegisterButton("btnSettingsMute", ToggleMenuAudio);
        RegisterButton("btnCloseSettings", CloseSettings);
        RegisterButton("btnSettingsDone", CloseSettings);
        BindModal("SettingsPanel", "btnCloseSettings");
        BindModal("LegalPanel", "btnCloseLegal");
        BindModal("FeedbackModal", "btnFeedbackCancel");
        BindModal("PlayerInfoModal", "btnClosePlayerInfo");
        BindModal("AvatarSelectionModal", "btnCloseAvatarSelection");
        BindModal("AvatarShopPlaceholderModal", "btnCloseAvatarShopPlaceholder");
        BindModal("TutorialReplayConfirmModal", "btnTutorialReplayCancel");
        BindModal("RenameConfirmModal", "btnRenameConfirmCancel");
        BindModal("RenamePlayerModal", "btnRenameCancel");
        BindModal("UpgradeShopPanel", "btnCloseUpgrades");
        BindModal("ChestShopPanel", "btnCloseChestShop");
        RegisterButton("btnLegal", () => ShowPanel("LegalPanel"));
        RegisterButton("btnCloseLegal", () => ShowPanel("MenuPanel"));
        RegisterButton("btnLegalDone", () => ShowPanel("MenuPanel"));

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
        MenuButtonIcon.Attach(root.Q<Button>("btnLong"), false);
        MenuButtonIcon.Attach(root.Q<Button>("btnTutorial"), true);
        localization = new MenuLocalization(root, RefreshLanguage);
        joTutorial = new JoTutorialView(root.Q("JoTutorialPanel"), T, () => ShowPanel("MenuPanel"));
        RegisterButton("btnExperimental", () => SelectModeAndLoad(experimentalPreset));
        RegisterButton("btnUpgrades", OpenUpgradeShop);
        RegisterButton("btnCloseUpgrades", CloseUpgradeShop);
        RegisterButton("btnCloseChestShop", CloseChestShop);
        root.RegisterCallback<KeyDownEvent>(HandleRootKeyDown, TrickleDown.TrickleDown);

        walletPanelController = GetComponent<WalletPanelController>() ?? gameObject.AddComponent<WalletPanelController>();
        playerPanelController = GetComponent<PlayerPanelController>() ?? gameObject.AddComponent<PlayerPanelController>();
        levelUpWindowPresenter = GetComponent<LevelUpWindowPresenter>() ?? gameObject.AddComponent<LevelUpWindowPresenter>();
        chestRewardWindowPresenter = GetComponent<ChestRewardWindowPresenter>() ?? gameObject.AddComponent<ChestRewardWindowPresenter>();

        ProfileService.Load();

        upgradeShopController = GetComponent<UpgradeShopController>() ?? gameObject.AddComponent<UpgradeShopController>();

        InitializeAboutSection();
        InitializeAudioSliders();
        InitializeFeedbackForm();
        InitializeTutorialReplayConfirmation();
        UpdateSettingsButtonState(isSettingsOpen);
        levelUpWindowPresenter.Initialize(root);
        chestRewardWindowPresenter.Initialize(root);

        playerPanelController.SetPortraitLibrary(tutorialPortraitLibrary);
        playerPanelController.Initialize(root);

        walletPanelController.SetLevelUpPresenter(levelUpWindowPresenter);
        walletPanelController.SetChestRewardPresenter(chestRewardWindowPresenter);
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
        mapFlowOrchestrator.SetChestRewardPresenter(chestRewardWindowPresenter);
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
        if (root != null) ambientView = new MenuAmbientView(root, atmosphere);
        audioManager = AudioManager.Instance != null
            ? AudioManager.Instance
            : FindAnyObjectByType<AudioManager>();

        RefreshAudioSlidersFromManager();

        audioManager?.EnsureMusicForContext(MusicContext.Menu);
        if (audioManager != null)
        {
            audioManager.OnVolumesChanged -= HandleAudioVolumesChanged;
            audioManager.OnVolumesChanged += HandleAudioVolumesChanged;
            audioManager.OnMuteChanged += HandleMuteChanged;
        }

        if (Diceforge.Map.MapFlowRuntime.HasPendingBattleResult || Diceforge.Map.MapFlowRuntime.ConsumeReturnToMapRequest())
            OpenMapChapterImmediately();
    }

    private void OnDestroy()
    {
        joTutorial?.Dispose();
        localization?.Dispose();
        foreach (var binding in modalBindings) binding.Dispose();
        foreach (var job in panelHideJobs.Values) job.Pause();
        ambientView?.Dispose();
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
        {
            audioManager.OnVolumesChanged -= HandleAudioVolumesChanged;
            audioManager.OnMuteChanged -= HandleMuteChanged;
        }

        if (musicSlider != null)
            musicSlider.UnregisterValueChangedCallback(OnMusicSliderChanged);

        if (sfxSlider != null)
            sfxSlider.UnregisterValueChangedCallback(OnSfxSliderChanged);

        if (root != null)
            root.UnregisterCallback<KeyDownEvent>(HandleRootKeyDown, TrickleDown.TrickleDown);
    }

    public void ShowPanel(string panelName)
    {
        bool modal = panelName == "SettingsPanel" || panelName == "LegalPanel"
            || currentPanel?.name == "SettingsPanel" || currentPanel?.name == "LegalPanel";
        if (modal) ShowPanelImmediately(panelName);
        else TransitionToScreen(() => ShowPanelImmediately(panelName));
    }

    private void ShowPanelImmediately(string panelName)
    {
        CloseInventory();
        if (!panels.TryGetValue(panelName, out var targetPanel))
        {
            Debug.LogWarning($"[MainMenu] Panel not found: {panelName}");
            return;
        }

        if (targetPanel == currentPanel)
        {
            return;
        }

        if (currentPanel?.name == "JoTutorialPanel") joTutorial?.CancelAnimation();
        if (settingsButton != null) settingsButton.style.visibility = panelName == "JoTutorialPanel" ? Visibility.Hidden : Visibility.Visible;
        bool openingSettings = panelName == "SettingsPanel";
        bool openingMenuOverlay = openingSettings || panelName == "LegalPanel";
        if (currentPanel != null && !(openingMenuOverlay && currentPanel.name == "MenuPanel"))
        {
            HidePanel(currentPanel);
        }

        currentPanel = targetPanel;
        if (panelHideJobs.TryGetValue(targetPanel, out var hideJob))
        {
            hideJob.Pause();
            panelHideJobs.Remove(targetPanel);
        }
        targetPanel.style.display = DisplayStyle.Flex;
        targetPanel.pickingMode = PickingMode.Position;
        if (!targetPanel.ClassListContains(VisibleClass))
        {
            targetPanel.schedule.Execute(() =>
            {
                if (currentPanel == targetPanel) targetPanel.AddToClassList(VisibleClass);
            }).ExecuteLater(20);
        }
        if (panels.TryGetValue("MenuPanel", out var menuPanel))
        {
            menuPanel.SetEnabled(!openingMenuOverlay);
            if (openingMenuOverlay)
            {
                menuPanel.style.display = DisplayStyle.Flex;
                menuPanel.AddToClassList(VisibleClass);
            }
            else if (panelName != "MenuPanel")
                HidePanel(menuPanel);
        }
        isSettingsOpen = currentPanel.name == "SettingsPanel";
        UpdateSettingsButtonState(isSettingsOpen);
        if (isSettingsOpen) root.Q<Button>("btnCloseSettings")?.Focus();
        else if (panelName == "LegalPanel") root.Q<Button>("btnCloseLegal")?.Focus();
        else if (panelName == "MenuPanel") settingsButton?.Focus();
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

    private void OpenMenuProfile()
    {
        CloseInventory();
        GetComponent<PlayerInfoController>()?.Open();
    }

    private void CloseInventory()
    {
        root?.Q("LeftSidebar")?.RemoveFromClassList("inventory-open");
    }

    private void ToggleMenuAudio()
    {
        if (audioManager == null) return;
        audioManager.SetMuted(!audioManager.IsMuted);
    }

    private void HandleMuteChanged(bool muted) => RefreshMenuAudio();

    private void RefreshMenuAudio()
    {
        var button = root?.Q<Button>("btnMenuAudio");
        if (button == null || audioManager == null) return;
        bool muted = audioManager.IsMuted;
        button.EnableInClassList("is-muted", muted);
        button.tooltip = T(muted ? "Unmute audio" : "Mute audio");
        var settingsMute = root.Q<Button>("btnSettingsMute");
        if (settingsMute != null)
        {
            settingsMute.text = string.Empty;
            settingsMute.EnableInClassList("is-muted", muted);
            settingsMute.tooltip = button.tooltip;
        }
        root.Q("SettingsAudioControls")?.EnableInClassList("is-muted", muted);
        var soundHint = root.Q<Label>("settingsSoundHint");
        if (soundHint != null)
            soundHint.text = T(muted ? "Sound is muted. Your volume levels are saved." : "A little woodland ambience.");
    }

    public void HidePanel(VisualElement panel)
    {
        if (panelHideJobs.TryGetValue(panel, out var previousJob)) previousJob.Pause();
        panel.RemoveFromClassList(VisibleClass);
        panelHideJobs[panel] = panel.schedule.Execute(() =>
        {
            if (panel != currentPanel)
            {
                panel.style.display = DisplayStyle.None;
            }
        });
        panelHideJobs[panel].ExecuteLater(Mathf.RoundToInt(TransitionSeconds * 1000f));
    }

    private void BindModal(string overlay, string button)
    {
        modalBindings.Add(Diceforge.UI.ModalDismiss.BindButton(root, overlay, button));
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
        if (ScreenTransition.IsBusy) { evt?.StopImmediatePropagation(); return; }
        if (evt == null)
            return;

        if (evt.keyCode == KeyCode.Escape && currentPanel?.name == "JoTutorialPanel")
        {
            joTutorial.Exit();
            evt.StopPropagation();
            return;
        }

        if (evt.keyCode == KeyCode.Escape && currentPanel?.name == "LegalPanel")
        {
            ShowPanel("MenuPanel");
            root.Q<Button>("btnLegal")?.Focus();
            evt.StopPropagation();
            return;
        }

        if (evt.keyCode == KeyCode.Escape && isSettingsOpen)
        {
            if (feedbackModal != null && feedbackModal.resolvedStyle.display != DisplayStyle.None)
                CloseFeedbackForm();
            else
                CloseSettings();
            evt.StopPropagation();
            return;
        }

        bool isDevToggle = (evt.ctrlKey || evt.commandKey) && !evt.altKey && evt.keyCode == KeyCode.D;
        if (!isDevToggle)
            return;

        areDevActionsVisible = !areDevActionsVisible;
        walletPanelController?.SetDevActionsVisible(areDevActionsVisible);
        evt.StopPropagation();
    }

    private void RefreshProgressiveUi()
    {
        var menuLevel = root?.Q<Label>("menuLevel");
        var menuCoins = root?.Q<Label>("menuCoins");
        if (menuLevel != null) menuLevel.text = $"Lv {UiProgressionService.GetPlayerLevel()}";
        if (menuCoins != null) menuCoins.text = ProfileService.GetCurrency(ProgressionIds.SoftGold).ToString();
        var menuAvatar = root?.Q(className: "gh-avatar");
        var selectedAvatar = AvatarService.GetSelectedAvatarSprite();
        if (menuAvatar != null && selectedAvatar != null)
            menuAvatar.style.backgroundImage = new StyleBackground(selectedAvatar);
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
            aboutVersionLabel.text = $"{T("Version")} {Application.version}";
        }
    }

    private void InitializeFeedbackForm()
    {
        if (feedbackModal != null)
            feedbackModal.style.display = DisplayStyle.None;

        if (feedbackCategoryField != null)
        {
            feedbackCategoryField.choices = FeedbackCategories;
            feedbackCategoryField.formatListItemCallback = value => T(value);
            feedbackCategoryField.formatSelectedValueCallback = value => T(value);
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
        openFeedbackButton?.Focus();
    }

    private void RefreshLanguage()
    {
        RefreshMenuAudio();
        InitializeAboutSection();
        SetFeedbackStatus(feedbackStatusSource, feedbackStatusError);
        if (feedbackCategoryField != null)
            feedbackCategoryField.SetValueWithoutNotify(feedbackCategoryField.value);
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
            SetFeedbackStatus("Feedback is limited to {0} characters.", true);
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
        feedbackStatusSource = message;
        feedbackStatusError = isError;
        if (feedbackStatusLabel == null)
            return;

        feedbackStatusLabel.text = string.Format(T(message) ?? string.Empty, SpacetimeDbFeedbackSink.MaxMessageLength);
        feedbackStatusLabel.style.color = isError
            ? new StyleColor(new Color(0.65f, 0.16f, 0.10f, 1f))
            : new StyleColor(new Color(0.35f, 0.27f, 0.16f, 1f));
    }

    private void InitializeAudioSliders()
    {
        if (musicSlider != null)
        {
            musicSlider.RegisterValueChangedCallback(OnMusicSliderChanged);
            InitializeVolumeFill(musicSlider);
        }

        if (sfxSlider != null)
        {
            sfxSlider.RegisterValueChangedCallback(OnSfxSliderChanged);
            InitializeVolumeFill(sfxSlider);
        }

        RefreshAudioSlidersFromManager();
    }

    private void OnMusicSliderChanged(ChangeEvent<float> evt)
    {
        audioManager ??= AudioManager.Instance != null
            ? AudioManager.Instance
            : FindAnyObjectByType<AudioManager>();

        UnmuteFromSlider();
        audioManager?.SetMusicVolume(evt.newValue);
        UpdateVolumeFill(musicSlider, evt.newValue);
    }

    private void OnSfxSliderChanged(ChangeEvent<float> evt)
    {
        audioManager ??= AudioManager.Instance != null
            ? AudioManager.Instance
            : FindAnyObjectByType<AudioManager>();

        UnmuteFromSlider();
        audioManager?.SetSfxVolume(evt.newValue);
        UpdateVolumeFill(sfxSlider, evt.newValue);
    }

    private void HandleAudioVolumesChanged(float musicVolume, float sfxVolume)
    {
        RefreshMenuAudio();
        RefreshSettingsVolumeLabels(musicVolume, sfxVolume);
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

        RefreshMenuAudio();
        RefreshSettingsVolumeLabels(audioManager.MusicVolume, audioManager.SfxVolume);

        if (musicSlider != null)
            musicSlider.SetValueWithoutNotify(audioManager.MusicVolume);

        if (sfxSlider != null)
            sfxSlider.SetValueWithoutNotify(audioManager.SfxVolume);
    }

    private void RefreshSettingsVolumeLabels(float music, float sfx)
    {
        UpdateVolumeFill(musicSlider, music);
        UpdateVolumeFill(sfxSlider, sfx);
    }

    private void InitializeVolumeFill(Slider slider)
    {
        var tracker = slider.Q(className: "unity-base-slider__tracker");
        if (tracker != null && tracker.Q("VolumeFill") == null)
        {
            var fill = new VisualElement { name = "VolumeFill", pickingMode = PickingMode.Ignore };
            fill.AddToClassList("gh-volume-fill");
            tracker.Add(fill);
        }
        slider.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button == 0) UnmuteFromSlider();
        }, TrickleDown.TrickleDown);
        UpdateVolumeFill(slider, slider.value);
    }

    private void UnmuteFromSlider()
    {
        audioManager ??= AudioManager.Instance;
        if (audioManager != null && audioManager.IsMuted) audioManager.SetMuted(false);
    }

    private static void UpdateVolumeFill(Slider slider, float value)
    {
        var fill = slider?.Q("VolumeFill");
        if (fill != null) fill.style.width = Length.Percent(Mathf.InverseLerp(slider.lowValue, slider.highValue, value) * 100f);
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

        TransitionToScreen(() => { ShowPanelImmediately("UpgradeShopPanel"); upgradeShopController?.Show(); });
    }

    private void CloseUpgradeShop()
    {
        TransitionToScreen(() => { upgradeShopController?.Hide(); ShowPanelImmediately("MenuPanel"); });
    }

    private void OpenChestScreen()
    {
        if (!UiProgressionService.IsChestSectionUnlocked())
            return;

        TransitionToScreen(() => { ShowPanelImmediately("ChestOpenPanel"); chestOpenController?.Show(); });
    }

    private void CloseChestScreen()
    {
        TransitionToScreen(() => { chestOpenController?.Hide(); ShowPanelImmediately("MenuPanel"); });
    }

    private void OpenChestShop()
    {
        if (!UiProgressionService.IsChestSectionUnlocked())
            return;

        TransitionToScreen(() => { ShowPanelImmediately("ChestShopPanel"); chestShopController?.Show(); });
    }

    private void CloseChestShop()
    {
        TransitionToScreen(() => { chestShopController?.Hide(); ShowPanelImmediately("MenuPanel"); });
    }

    private void HandleTutorialSelected()
    {
        joTutorial.Reset();
        ShowPanel("JoTutorialPanel");
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

        MapFlowRuntime.StartStandaloneBattle();
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

        MapFlowRuntime.StartStandaloneBattle();
        Debug.Log($"[MainMenu] Starting NEW battle pipeline with preset: {preset.name} ({preset.modeId}) mapOverride={mapOverride.name}");
        BattleLauncher.Start(new BattleStartRequest(preset, mapOverride));
    }

    private void OpenMapChapter()
    {
        if (mapFlowOrchestrator == null) return;
        TransitionToScreen(OpenMapChapterImmediately);
    }

    private void OpenMapChapterImmediately()
    {
        ShowPanelImmediately("MenuPanel");
        mapFlowOrchestrator?.StartChapter(defaultChapterId);
    }

    private void TransitionToScreen(Action switchScreen)
    {
        if (root != null && isActiveAndEnabled) ScreenTransition.Switch(switchScreen);
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
        if (mapController != null) TransitionToScreen(mapController.Hide);
    }
}
