using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChestShopController : MonoBehaviour
{
    private const int FxTickIntervalMs = 16;
    private const float FxSpeed = 1.05f;
    private const float PrimarySweepWidth = 248f;
    private const float PrimarySweepHeightMultiplier = 2.18f;
    private const float AccentSweepWidth = 204f;
    private const float AccentSweepHeightMultiplier = 1.92f;
    private const float GlowWidthRatio = 0.68f;
    private const float GlowHeightRatio = 0.82f;
    private const float PrimarySweepAngleDegrees = -20f;
    private const float AccentSweepAngleDegrees = 16f;

    private VisualElement _fxElement;
    private VisualElement _fxGlowElement;
    private VisualElement _fxPrimarySweepElement;
    private VisualElement _fxAccentSweepElement;
    private Label _statusLabel;
    private IVisualElementScheduledItem _fxTicker;
    private float _fxTime;
    private bool _isVisible;

    public void Initialize(VisualElement root)
    {
        if (root == null)
            return;

        _fxElement = root.Q<VisualElement>("chestShopFx");
        _fxGlowElement = root.Q<VisualElement>("chestShopFxGlow");
        _fxPrimarySweepElement = root.Q<VisualElement>("chestShopFxSweepPrimary");
        _fxAccentSweepElement = root.Q<VisualElement>("chestShopFxSweepAccent");
        _statusLabel = root.Q<Label>("chestShopStatus");

        ConfigureFxElement(_fxElement, 0f);
        ConfigureFxElement(_fxGlowElement, 0f);
        ConfigureFxElement(_fxPrimarySweepElement, PrimarySweepAngleDegrees);
        ConfigureFxElement(_fxAccentSweepElement, AccentSweepAngleDegrees);
        ResetFxVisuals();

        RegisterBuyButton(root, "btnBuySmallChest", ProgressionIds.SoftGold, 50, ProgressionIds.BasicChest, "Small Chest purchased.");
        RegisterBuyButton(root, "btnBuyMediumChest", ProgressionIds.SoftGold, 120, ProgressionIds.MediumChest, "Medium Chest purchased.");
        RegisterBuyButton(root, "btnBuyEssenceChest", ProgressionIds.Essence, 10, ProgressionIds.EssenceChest, "Essence Chest purchased.");
    }

    public void Show()
    {
        if (_statusLabel != null)
            _statusLabel.text = string.Empty;

        _isVisible = true;
        StartFxTicker();
    }

    public void Hide()
    {
        _isVisible = false;
        StopFxTicker();
    }

    private void OnDestroy()
    {
        _fxTicker?.Pause();
    }

    private void RegisterBuyButton(VisualElement root, string buttonName, string currencyId, int cost, string chestId, string successText)
    {
        var button = root.Q<Button>(buttonName);
        if (button == null)
            return;

        button.clicked += () => TryBuyChest(currencyId, cost, chestId, successText);
    }

    private void TryBuyChest(string currencyId, int cost, string chestId, string successText)
    {
        if (!ProfileService.SpendCurrency(currencyId, cost))
        {
            SetStatus("Not enough currency.");
            return;
        }

        var chest = ChestService.CreateChestInstance(chestId);
        if (chest == null)
        {
            ProfileService.AddCurrency(currencyId, cost);
            SetStatus("Failed to create chest.");
            return;
        }

        ProfileService.AddChest(chest);
        SetStatus(successText);
    }

    private void SetStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.text = message;
    }

    private void ConfigureFxElement(VisualElement element, float rotationDegrees)
    {
        if (element == null)
            return;

        element.pickingMode = PickingMode.Ignore;
        element.style.rotate = new Rotate(new Angle(rotationDegrees, AngleUnit.Degree));
    }

    private void StartFxTicker()
    {
        if (_fxElement == null)
            return;

        _fxTime = 0f;
        UpdateFxVisuals();

        if (_fxTicker == null)
            _fxTicker = _fxElement.schedule.Execute(TickFx).Every(FxTickIntervalMs);
        else
            _fxTicker.Resume();
    }

    private void StopFxTicker()
    {
        _fxTicker?.Pause();
        ResetFxVisuals();
    }

    private void TickFx()
    {
        if (!_isVisible)
            return;

        _fxTime += Time.unscaledDeltaTime;
        UpdateFxVisuals();
    }

    private void UpdateFxVisuals()
    {
        if (_fxElement == null)
            return;

        float width = _fxElement.resolvedStyle.width;
        float height = _fxElement.resolvedStyle.height;
        if (width <= 0f || height <= 0f)
            return;

        float basePhase = _fxTime * FxSpeed;
        float primaryPhase = EasedOscillation(basePhase);
        float accentPhase = EasedOscillation(basePhase + Mathf.PI * 0.5f);
        float glowPhase = EasedOscillation(basePhase * 0.72f + 0.35f);

        ApplyGlow(width, height, glowPhase);
        ApplySweep(_fxPrimarySweepElement, width, height, PrimarySweepWidth, PrimarySweepHeightMultiplier, primaryPhase, Mathf.Lerp(0.07f, 0.23f, primaryPhase));
        ApplySweep(_fxAccentSweepElement, width, height, AccentSweepWidth, AccentSweepHeightMultiplier, accentPhase, Mathf.Lerp(0.05f, 0.18f, accentPhase));
    }

    private void ApplyGlow(float width, float height, float pulse)
    {
        if (_fxGlowElement == null)
            return;

        _fxGlowElement.style.width = width * GlowWidthRatio;
        _fxGlowElement.style.height = height * GlowHeightRatio;
        _fxGlowElement.style.opacity = Mathf.Lerp(0.05f, 0.16f, pulse);
    }

    private void ApplySweep(VisualElement sweepElement, float width, float height, float sweepWidth, float heightMultiplier, float phase, float opacity)
    {
        if (sweepElement == null)
            return;

        float sweepHeight = height * heightMultiplier;
        float sweepLeft = Mathf.Lerp(-sweepWidth * 1.2f, width - sweepWidth * 0.15f, phase);

        sweepElement.style.width = sweepWidth;
        sweepElement.style.height = sweepHeight;
        sweepElement.style.left = sweepLeft;
        sweepElement.style.opacity = opacity;
    }

    private void ResetFxVisuals()
    {
        if (_fxGlowElement != null)
            _fxGlowElement.style.opacity = 0f;

        ResetSweep(_fxPrimarySweepElement, PrimarySweepWidth);
        ResetSweep(_fxAccentSweepElement, AccentSweepWidth);
    }

    private void ResetSweep(VisualElement sweepElement, float sweepWidth)
    {
        if (sweepElement == null)
            return;

        sweepElement.style.left = -sweepWidth * 1.5f;
        sweepElement.style.opacity = 0f;
    }

    private static float EasedOscillation(float phase)
    {
        float wave = 0.5f + 0.5f * Mathf.Sin(phase);
        return wave * wave * (3f - 2f * wave);
    }
}