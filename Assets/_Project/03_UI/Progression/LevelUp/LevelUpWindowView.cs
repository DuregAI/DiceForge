using System;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class LevelUpWindowView
{
    private readonly VisualElement _container;
    private readonly VisualElement _overlay;
    private readonly VisualElement _panel;
    private readonly VisualElement _backdropGlow;
    private readonly Image _fxBackLayer;
    private readonly Image _fxFrontLayer;
    private readonly Label _titleLabel;
    private readonly Label _subtitleLabel;
    private readonly VisualElement _levelShell;
    private readonly VisualElement _levelGlow;
    private readonly Label _levelValueLabel;
    private readonly Label _flavorLabel;
    private readonly VisualElement _unlockSection;
    private readonly VisualElement _unlockList;
    private readonly Button _continueButton;

    private IVisualElementScheduledItem _pulseSchedule;
    private float _pulseStartTime;
    private bool _interactionReady;

    public LevelUpWindowView(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _container = root;
        _overlay = root.name == "levelUpOverlay"
            ? root
            : root.Q<VisualElement>("levelUpOverlay");
        if (_overlay == null)
            throw new InvalidOperationException("[LevelUpWindowView] Missing root element 'levelUpOverlay'.");

        _panel = _overlay.Q<VisualElement>("levelUpPanel");
        _backdropGlow = _overlay.Q<VisualElement>("levelUpBackdropGlow");
        _fxBackLayer = _overlay.Q<Image>("levelUpFxBackLayer");
        _fxFrontLayer = _overlay.Q<Image>("levelUpFxFrontLayer");
        _titleLabel = _overlay.Q<Label>("levelUpTitle");
        _subtitleLabel = _overlay.Q<Label>("levelUpSubtitle");
        _levelShell = _overlay.Q<VisualElement>("levelUpLevelShell");
        _levelGlow = _overlay.Q<VisualElement>("levelUpLevelGlow");
        _levelValueLabel = _overlay.Q<Label>("levelUpLevelValue");
        _flavorLabel = _overlay.Q<Label>("levelUpFlavor");
        _unlockSection = _overlay.Q<VisualElement>("levelUpUnlockSection");
        _unlockList = _overlay.Q<VisualElement>("levelUpUnlockList");
        _continueButton = _overlay.Q<Button>("levelUpContinueButton");

        if (_continueButton != null)
            _continueButton.clicked += HandleContinueClicked;

        ConfigureFxLayer(_fxBackLayer);
        ConfigureFxLayer(_fxFrontLayer);

        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
        _container.pickingMode = PickingMode.Ignore;
    }

    public event Action ContinueRequested;

    public VisualElement Root => _overlay;
    public Image FxBackLayerImage => _fxBackLayer;
    public Image FxFrontLayerImage => _fxFrontLayer;
    public VisualElement LevelAnchorElement => _levelShell;
    public bool IsInteractionReady => _interactionReady;

    public void Bind(LevelUpPresentationData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        if (_titleLabel != null)
            _titleLabel.text = "LEVEL UP";
        if (_subtitleLabel != null)
            _subtitleLabel.text = $"You reached Level {data.NewLevel}";
        if (_levelValueLabel != null)
            _levelValueLabel.text = data.NewLevel.ToString();

        if (_flavorLabel != null)
        {
            bool hasFlavor = !string.IsNullOrWhiteSpace(data.FlavorText);
            _flavorLabel.text = hasFlavor ? data.FlavorText : string.Empty;
            _flavorLabel.style.display = hasFlavor ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_unlockSection != null)
            _unlockSection.style.display = data.HasUnlocks ? DisplayStyle.Flex : DisplayStyle.None;

        RebuildUnlockList(data);
    }

    public void PrepareForShow()
    {
        _interactionReady = false;
        _container.BringToFront();
        _overlay.style.display = DisplayStyle.Flex;
        _overlay.pickingMode = PickingMode.Position;
        _overlay.BringToFront();
        _overlay.RemoveFromClassList("is-open");
        _overlay.RemoveFromClassList("is-panel-visible");
        _overlay.RemoveFromClassList("is-primary-visible");
        _overlay.RemoveFromClassList("is-unlocks-visible");
        _overlay.RemoveFromClassList("is-ready");

        if (_continueButton != null)
            _continueButton.SetEnabled(false);

        StopLevelPulse();
    }

    public void ShowOverlay()
    {
        _overlay.AddToClassList("is-open");
    }

    public void ShowPanel()
    {
        _overlay.AddToClassList("is-panel-visible");
    }

    public void ShowPrimaryContent()
    {
        _overlay.AddToClassList("is-primary-visible");
    }

    public void ShowUnlocks()
    {
        if (_unlockSection != null && _unlockSection.style.display != DisplayStyle.None)
            _overlay.AddToClassList("is-unlocks-visible");
    }

    public void SetInteractionReady(bool ready)
    {
        _interactionReady = ready;

        if (_continueButton != null)
            _continueButton.SetEnabled(ready);

        if (ready)
            _overlay.AddToClassList("is-ready");
        else
            _overlay.RemoveFromClassList("is-ready");
    }

    public void StartLevelPulse()
    {
        if (_levelGlow == null)
            return;

        _pulseStartTime = Time.realtimeSinceStartup;
        _pulseSchedule ??= _levelGlow.schedule.Execute(UpdatePulse).Every(16);
        _pulseSchedule.Resume();
    }

    public void StopLevelPulse()
    {
        if (_pulseSchedule != null)
            _pulseSchedule.Pause();

        if (_levelGlow != null)
        {
            _levelGlow.style.opacity = 0.3f;
            _levelGlow.style.scale = new Scale(Vector3.one);
        }
    }

    public void Hide()
    {
        StopLevelPulse();
        _interactionReady = false;
        _overlay.RemoveFromClassList("is-ready");
        _overlay.RemoveFromClassList("is-unlocks-visible");
        _overlay.RemoveFromClassList("is-primary-visible");
        _overlay.RemoveFromClassList("is-panel-visible");
        _overlay.RemoveFromClassList("is-open");
        _overlay.pickingMode = PickingMode.Ignore;
        if (_fxBackLayer != null)
            _fxBackLayer.image = null;
        if (_fxFrontLayer != null)
            _fxFrontLayer.image = null;
        _overlay.style.display = DisplayStyle.None;
    }

    public void Dispose()
    {
        if (_continueButton != null)
            _continueButton.clicked -= HandleContinueClicked;

        StopLevelPulse();
    }

    private void RebuildUnlockList(LevelUpPresentationData data)
    {
        if (_unlockList == null)
            return;

        _unlockList.Clear();
        if (data == null || !data.HasUnlocks)
            return;

        for (int i = 0; i < data.Unlocks.Count; i++)
        {
            LevelUpUnlockInfo unlock = data.Unlocks[i];

            var row = new VisualElement();
            row.AddToClassList("level-up-unlock-row");

            var marker = new VisualElement();
            marker.AddToClassList("level-up-unlock-marker");

            var label = new Label(string.IsNullOrWhiteSpace(unlock.DisplayName) ? unlock.Id : unlock.DisplayName);
            label.AddToClassList("level-up-unlock-label");

            row.Add(marker);
            row.Add(label);
            _unlockList.Add(row);
        }
    }

    private void UpdatePulse()
    {
        if (_levelGlow == null)
            return;

        float elapsed = Time.realtimeSinceStartup - _pulseStartTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 3.6f);
        float scale = 0.96f + (pulse * 0.08f);
        _levelGlow.style.opacity = 0.24f + (pulse * 0.32f);
        _levelGlow.style.scale = new Scale(new Vector3(scale, scale, 1f));

        if (_backdropGlow != null)
            _backdropGlow.style.opacity = 0.18f + (pulse * 0.12f);
    }

    private void HandleContinueClicked()
    {
        if (_interactionReady)
            ContinueRequested?.Invoke();
    }

    private static void ConfigureFxLayer(Image fxLayer)
    {
        if (fxLayer == null)
            return;

        fxLayer.scaleMode = ScaleMode.StretchToFill;
        fxLayer.pickingMode = PickingMode.Ignore;
    }
}
