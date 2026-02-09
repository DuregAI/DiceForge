using System;
using System.Collections.Generic;
using UnityEngine;
using Diceforge.Progression;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    private const string VisibleClass = "is-visible";
    private const float TransitionSeconds = 0.2f;
    private const string MusicVolumeKey = "audio.musicVolume";
    private const string SfxVolumeKey = "audio.sfxVolume";
    private const float DefaultVolume = 1f;
    private const string SettingsStateClass = "state-settings";
    private const string BackStateClass = "state-back";

    [Header("Game Mode Presets")]
    [SerializeField] private GameModePreset longPreset;
    [SerializeField] private GameModePreset shortPreset;
    [SerializeField] private GameModePreset tutorialPreset;
    [SerializeField] private GameModePreset experimentalPreset;

    private UIDocument document;
    private VisualElement root;
    private Label buildInfoLabel;
    private Label aboutVersionLabel;
    private Slider musicSlider;
    private Slider sfxSlider;
    private Button settingsButton;
    private Button copyLogButton;
    private TextField playerNameField;
    private Button applyPlayerNameButton;
    private VisualElement copyLogTooltip;
    private readonly Dictionary<string, VisualElement> panels = new();
    private VisualElement currentPanel;
    private bool isSettingsOpen;
    private WalletPanelController walletPanelController;
    private UpgradeShopController upgradeShopController;
    private ChestOpenController chestOpenController;

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
        playerNameField = root.Q<TextField>("txtPlayerName");
        applyPlayerNameButton = root.Q<Button>("btnApplyPlayerName");

        RegisterPanel("MenuPanel");
        RegisterPanel("SettingsPanel");
        RegisterPanel("UpgradeShopPanel");
        RegisterPanel("ChestOpenPanel");

        if (panels.TryGetValue("MenuPanel", out var menuPanel))
        {
            currentPanel = menuPanel;
            menuPanel.style.display = DisplayStyle.Flex;
            menuPanel.AddToClassList(VisibleClass);
        }

        RegisterButton("btnSettings", ToggleSettingsPanel);

        RegisterButton("btnCopyLog", CopyLogToClipboard);

        RegisterButton("btnLong", () => SelectModeAndLoad(longPreset));
        RegisterButton("btnShort", () => SelectModeAndLoad(shortPreset));
        RegisterButton("btnTutorial", () => SelectModeAndLoad(tutorialPreset));
        RegisterButton("btnExperimental", () => SelectModeAndLoad(experimentalPreset));
        RegisterButton("btnUpgrades", OpenUpgradeShop);
        RegisterButton("btnCloseUpgrades", CloseUpgradeShop);
        RegisterButton("btnChests", OpenChestScreen);

        walletPanelController = GetComponent<WalletPanelController>();
        if (walletPanelController == null)
            walletPanelController = gameObject.AddComponent<WalletPanelController>();

        ProfileService.Load();

        upgradeShopController = GetComponent<UpgradeShopController>();
        if (upgradeShopController == null)
            upgradeShopController = gameObject.AddComponent<UpgradeShopController>();

        InitializeAboutSection();
        InitializeAudioSliders();
        InitializeCopyLogTooltip();
        InitializePlayerIdentity();
        UpdateSettingsButtonState(isSettingsOpen);
        walletPanelController.Initialize(root);
        walletPanelController.OpenChestScreenRequested -= OpenChestScreen;
        walletPanelController.OpenChestScreenRequested += OpenChestScreen;
        upgradeShopController.Initialize(root);

        chestOpenController = GetComponent<ChestOpenController>();
        if (chestOpenController == null)
            chestOpenController = gameObject.AddComponent<ChestOpenController>();

        chestOpenController.Initialize(root);
        chestOpenController.CloseRequested -= CloseChestScreen;
        chestOpenController.CloseRequested += CloseChestScreen;
    }

    private void OnDestroy()
    {
        if (chestOpenController != null)
            chestOpenController.CloseRequested -= CloseChestScreen;
        if (walletPanelController != null)
            walletPanelController.OpenChestScreenRequested -= OpenChestScreen;

        if (applyPlayerNameButton != null)
            applyPlayerNameButton.clicked -= ApplyPlayerName;
        if (playerNameField != null)
            playerNameField.UnregisterCallback<FocusOutEvent>(OnPlayerNameFocusOut);

        ProfileService.OnPlayerNameChanged -= HandlePlayerNameChanged;
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
        copyLogButton.RegisterCallback<MouseEnterEvent>(_ =>
        {
            copyLogTooltip.style.display = DisplayStyle.Flex;
        });
        copyLogButton.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            copyLogTooltip.style.display = DisplayStyle.None;
        });
    }

    private void InitializeAudioSliders()
    {
        var musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);
        var sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume);

        if (musicSlider != null)
        {
            musicSlider.value = musicVolume;
            musicSlider.RegisterValueChangedCallback(evt =>
            {
                PlayerPrefs.SetFloat(MusicVolumeKey, evt.newValue);
            });
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = sfxVolume;
            sfxSlider.RegisterValueChangedCallback(evt =>
            {
                PlayerPrefs.SetFloat(SfxVolumeKey, evt.newValue);
            });
        }
    }


    private void InitializePlayerIdentity()
    {
        if (playerNameField == null)
        {
            return;
        }

        playerNameField.value = ProfileService.Current.playerName;

        applyPlayerNameButton?.clicked -= ApplyPlayerName;
        applyPlayerNameButton?.clicked += ApplyPlayerName;
        playerNameField.UnregisterCallback<FocusOutEvent>(OnPlayerNameFocusOut);
        playerNameField.RegisterCallback<FocusOutEvent>(OnPlayerNameFocusOut);

        ProfileService.OnPlayerNameChanged -= HandlePlayerNameChanged;
        ProfileService.OnPlayerNameChanged += HandlePlayerNameChanged;
    }

    private void ApplyPlayerName()
    {
        if (playerNameField == null)
        {
            return;
        }

        ProfileService.SetPlayerName(playerNameField.value);
    }

    private void HandlePlayerNameChanged(string _)
    {
        if (playerNameField == null)
        {
            return;
        }

        var profileName = ProfileService.Current.playerName;
        if (playerNameField.value != profileName)
        {
            playerNameField.SetValueWithoutNotify(profileName);
        }
    }

    private void OnPlayerNameFocusOut(FocusOutEvent _)
    {
        ApplyPlayerName();
    }

    private void CopyLogToClipboard()
    {
        var buildInfo = buildInfoLabel != null ? buildInfoLabel.text : "Build: unknown";
        var logPayload = $"{buildInfo} | {DateTime.Now:O} | {Application.platform} | dummy log";
        GUIUtility.systemCopyBuffer = logPayload;
        Debug.Log("[MainMenu] Log copied");
    }

    private void OpenUpgradeShop()
    {
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
        ShowPanel("ChestOpenPanel");
        chestOpenController?.Show();
    }

    private void CloseChestScreen()
    {
        chestOpenController?.Hide();
        ShowPanel("MenuPanel");
    }

    private void SelectModeAndLoad(GameModePreset preset)
    {
        if (preset == null)
        {
            Debug.LogWarning("[MainMenu] Game mode preset is not assigned.");
        }
        else
        {
            GameModeSelection.SetSelected(preset);
        }

        SceneManager.LoadScene("Battle");
    }
}
