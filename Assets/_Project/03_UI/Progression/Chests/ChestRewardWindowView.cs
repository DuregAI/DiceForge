using System;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChestRewardWindowView
{
    private readonly VisualElement _container;
    private readonly VisualElement _overlay;
    private readonly VisualElement _panel;
    private readonly VisualElement _backdropGlow;
    private readonly Image _fxBackLayer;
    private readonly Image _fxFrontLayer;
    private readonly Label _titleLabel;
    private readonly VisualElement _chestShell;
    private readonly VisualElement _chestGlow;
    private readonly Image _chestIcon;
    private readonly Label _countBadge;
    private readonly Label _nameLabel;
    private readonly Label _rarityLabel;
    private readonly Label _flavorLabel;
    private readonly VisualElement _detailsSection;
    private readonly VisualElement _detailsList;
    private readonly Button _continueButton;

    private IVisualElementScheduledItem _pulseSchedule;
    private float _pulseStartTime;
    private bool _interactionReady;

    public ChestRewardWindowView(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _container = root;
        _overlay = root.name == "chestRewardOverlay"
            ? root
            : root.Q<VisualElement>("chestRewardOverlay");
        if (_overlay == null)
            throw new InvalidOperationException("[ChestRewardWindowView] Missing root element 'chestRewardOverlay'.");

        _panel = _overlay.Q<VisualElement>("chestRewardPanel");
        _backdropGlow = _overlay.Q<VisualElement>("chestRewardBackdropGlow");
        _fxBackLayer = _overlay.Q<Image>("chestRewardFxBackLayer");
        _fxFrontLayer = _overlay.Q<Image>("chestRewardFxFrontLayer");
        _titleLabel = _overlay.Q<Label>("chestRewardTitle");
        _chestShell = _overlay.Q<VisualElement>("chestRewardChestShell");
        _chestGlow = _overlay.Q<VisualElement>("chestRewardChestGlow");
        _chestIcon = _overlay.Q<Image>("chestRewardChestIcon");
        _countBadge = _overlay.Q<Label>("chestRewardCountBadge");
        _nameLabel = _overlay.Q<Label>("chestRewardName");
        _rarityLabel = _overlay.Q<Label>("chestRewardRarity");
        _flavorLabel = _overlay.Q<Label>("chestRewardFlavor");
        _detailsSection = _overlay.Q<VisualElement>("chestRewardDetailsSection");
        _detailsList = _overlay.Q<VisualElement>("chestRewardDetailsList");
        _continueButton = _overlay.Q<Button>("chestRewardContinueButton");

        if (_continueButton != null)
            _continueButton.clicked += HandleContinueClicked;

        ConfigureFxLayer(_fxBackLayer);
        ConfigureFxLayer(_fxFrontLayer);

        if (_chestIcon != null)
        {
            _chestIcon.scaleMode = ScaleMode.ScaleToFit;
            _chestIcon.pickingMode = PickingMode.Ignore;
        }

        _overlay.style.display = DisplayStyle.None;
        _overlay.pickingMode = PickingMode.Ignore;
        _container.pickingMode = PickingMode.Ignore;
    }

    public event Action ContinueRequested;

    public VisualElement Root => _overlay;
    public Image FxBackLayerImage => _fxBackLayer;
    public Image FxFrontLayerImage => _fxFrontLayer;
    public VisualElement ChestAnchorElement => _chestShell;
    public bool IsInteractionReady => _interactionReady;

    public void Bind(ChestRewardPresentationData data)
    {
        if (data == null || !data.HasEntries)
            throw new ArgumentNullException(nameof(data));

        ChestRewardPresentationEntry primaryEntry = data.PrimaryEntry;

        if (_titleLabel != null)
            _titleLabel.text = "REWARD RECEIVED";

        if (_chestIcon != null)
        {
            _chestIcon.sprite = primaryEntry.Icon;
            if (primaryEntry.Icon == null)
                _chestIcon.AddToClassList("is-empty");
            else
                _chestIcon.RemoveFromClassList("is-empty");
        }

        if (_countBadge != null)
        {
            bool showCountBadge = primaryEntry.Count > 1;
            _countBadge.style.display = showCountBadge ? DisplayStyle.Flex : DisplayStyle.None;
            _countBadge.text = showCountBadge ? $"x{primaryEntry.Count}" : string.Empty;
        }

        if (_nameLabel != null)
            _nameLabel.text = primaryEntry.DisplayName;

        if (_rarityLabel != null)
        {
            _rarityLabel.text = primaryEntry.HasRarityLabel ? primaryEntry.RarityLabel : string.Empty;
            _rarityLabel.style.display = primaryEntry.HasRarityLabel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_flavorLabel != null)
        {
            bool hasFlavor = !string.IsNullOrWhiteSpace(data.FlavorText);
            _flavorLabel.text = hasFlavor ? data.FlavorText : string.Empty;
            _flavorLabel.style.display = hasFlavor ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (_detailsSection != null)
            _detailsSection.style.display = data.HasAdditionalEntries ? DisplayStyle.Flex : DisplayStyle.None;

        RebuildDetails(data);
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
        _overlay.RemoveFromClassList("is-details-visible");
        _overlay.RemoveFromClassList("is-ready");

        if (_continueButton != null)
            _continueButton.SetEnabled(false);

        StopChestPulse();
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

    public void ShowDetails()
    {
        if (_detailsSection != null && _detailsSection.style.display != DisplayStyle.None)
            _overlay.AddToClassList("is-details-visible");
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

    public void StartChestPulse()
    {
        if (_chestGlow == null)
            return;

        _pulseStartTime = Time.realtimeSinceStartup;
        _pulseSchedule ??= _chestGlow.schedule.Execute(UpdatePulse).Every(16);
        _pulseSchedule.Resume();
    }

    public void StopChestPulse()
    {
        if (_pulseSchedule != null)
            _pulseSchedule.Pause();

        if (_chestGlow != null)
        {
            _chestGlow.style.opacity = 0.28f;
            _chestGlow.style.scale = new Scale(Vector3.one);
        }

        if (_chestShell != null)
            _chestShell.style.scale = new Scale(Vector3.one);
    }

    public void Hide()
    {
        StopChestPulse();
        _interactionReady = false;
        _overlay.RemoveFromClassList("is-ready");
        _overlay.RemoveFromClassList("is-details-visible");
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

        StopChestPulse();
    }

    private void RebuildDetails(ChestRewardPresentationData data)
    {
        if (_detailsList == null)
            return;

        _detailsList.Clear();
        if (data == null || !data.HasAdditionalEntries)
            return;

        for (int i = 0; i < data.Entries.Count; i++)
        {
            ChestRewardPresentationEntry entry = data.Entries[i];

            var row = new VisualElement();
            row.AddToClassList("chest-reward-detail-row");

            var icon = new Image();
            icon.AddToClassList("chest-reward-detail-icon");
            icon.scaleMode = ScaleMode.ScaleToFit;
            icon.sprite = entry.Icon;

            var name = new Label(entry.DisplayName);
            name.AddToClassList("chest-reward-detail-name");

            var count = new Label($"x{entry.Count}");
            count.AddToClassList("chest-reward-detail-count");

            row.Add(icon);
            row.Add(name);
            row.Add(count);
            _detailsList.Add(row);
        }
    }

    private void UpdatePulse()
    {
        if (_chestGlow == null)
            return;

        float elapsed = Time.realtimeSinceStartup - _pulseStartTime;
        float pulse = 0.5f + (0.5f * Mathf.Sin(elapsed * 4.4f));
        float glowScale = 0.94f + (pulse * 0.14f);
        _chestGlow.style.opacity = 0.2f + (pulse * 0.32f);
        _chestGlow.style.scale = new Scale(new Vector3(glowScale, glowScale, 1f));

        if (_chestShell != null)
        {
            float shellScale = 0.985f + (pulse * 0.05f);
            _chestShell.style.scale = new Scale(new Vector3(shellScale, shellScale, 1f));
        }

        if (_backdropGlow != null)
            _backdropGlow.style.opacity = 0.14f + (pulse * 0.1f);
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
