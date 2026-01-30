using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    private const string VisibleClass = "is-visible";
    private const float TransitionSeconds = 0.2f;

    [Header("Game Mode Presets")]
    [SerializeField] private GameModePreset longPreset;
    [SerializeField] private GameModePreset shortPreset;
    [SerializeField] private GameModePreset tutorialPreset;
    [SerializeField] private GameModePreset experimentalPreset;

    private UIDocument document;
    private VisualElement root;
    private Label buildInfoLabel;
    private readonly Dictionary<string, VisualElement> panels = new();
    private VisualElement currentPanel;

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

        RegisterPanel("MenuPanel");
        RegisterPanel("SettingsPanel");
        RegisterPanel("ProfilePanel");
        RegisterPanel("UpgradesPanel");
        RegisterPanel("BuildingsPanel");
        RegisterPanel("AboutPanel");

        if (panels.TryGetValue("MenuPanel", out var menuPanel))
        {
            currentPanel = menuPanel;
            menuPanel.style.display = DisplayStyle.Flex;
            menuPanel.AddToClassList(VisibleClass);
        }

        RegisterButton("btnProfile", () => ShowPanel("ProfilePanel"));
        RegisterButton("btnUpgrades", () => ShowPanel("UpgradesPanel"));
        RegisterButton("btnBuildings", () => ShowPanel("BuildingsPanel"));
        RegisterButton("btnSettings", () => ShowPanel("SettingsPanel"));
        RegisterButton("btnAbout", () => ShowPanel("AboutPanel"));

        RegisterButton("btnSettingsBack", BackToMenu);
        RegisterButton("btnProfileBack", BackToMenu);
        RegisterButton("btnUpgradesBack", BackToMenu);
        RegisterButton("btnBuildingsBack", BackToMenu);
        RegisterButton("btnAboutBack", BackToMenu);

        RegisterButton("btnCopyLog", CopyLogToClipboard);

        RegisterButton("btnLong", () => SelectModeAndLoad(longPreset));
        RegisterButton("btnShort", () => SelectModeAndLoad(shortPreset));
        RegisterButton("btnTutorial", () => SelectModeAndLoad(tutorialPreset));
        RegisterButton("btnExperimental", () => SelectModeAndLoad(experimentalPreset));
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
    }

    public void BackToMenu()
    {
        ShowPanel("MenuPanel");
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

    private void CopyLogToClipboard()
    {
        var buildInfo = buildInfoLabel != null ? buildInfoLabel.text : "Build: unknown";
        var logPayload = $"{buildInfo} | {DateTime.Now:O} | {Application.platform} | dummy log";
        GUIUtility.systemCopyBuffer = logPayload;
        Debug.Log("[MainMenu] Log copied");
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
