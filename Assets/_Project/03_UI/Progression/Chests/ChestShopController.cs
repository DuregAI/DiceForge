using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChestShopController : MonoBehaviour
{
    private const string FxShaderName = "Hidden/Diceforge/ChestShopSweep";

    private VisualElement _panel;
    private VisualElement _fxElement;
    private Label _statusLabel;
    private Material _fxMaterial;
    private bool _isVisible;
    private bool _warnedAboutMissingShader;

    public void Initialize(VisualElement root)
    {
        if (root == null)
            return;

        _panel = root.Q<VisualElement>("ChestShopPanel");
        _fxElement = root.Q<VisualElement>("chestShopFx");
        _statusLabel = root.Q<Label>("chestShopStatus");

        if (_fxElement != null)
            _fxElement.pickingMode = PickingMode.Ignore;

        RegisterBuyButton(root, "btnBuySmallChest", ProgressionIds.SoftGold, 50, ProgressionIds.BasicChest, "Small Chest purchased.");
        RegisterBuyButton(root, "btnBuyMediumChest", ProgressionIds.SoftGold, 120, ProgressionIds.MediumChest, "Medium Chest purchased.");
        RegisterBuyButton(root, "btnBuyEssenceChest", ProgressionIds.Essence, 10, ProgressionIds.EssenceChest, "Essence Chest purchased.");
    }

    public void Show()
    {
        if (_statusLabel != null)
            _statusLabel.text = string.Empty;

        _isVisible = true;
        ApplyFxMaterial();
        _fxElement?.MarkDirtyRepaint();
    }

    public void Hide()
    {
        _isVisible = false;
    }

    private void Update()
    {
        if (!_isVisible || _fxElement == null || _fxMaterial == null)
            return;

        _fxElement.MarkDirtyRepaint();
    }

    private void OnDestroy()
    {
        if (_fxMaterial != null)
        {
            Destroy(_fxMaterial);
            _fxMaterial = null;
        }
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

    private void ApplyFxMaterial()
    {
        if (_fxElement == null)
            return;

        EnsureFxMaterial();
        if (_fxMaterial == null)
            return;

        _fxElement.style.unityMaterial = _fxMaterial;
    }

    private void EnsureFxMaterial()
    {
        if (_fxMaterial != null)
            return;

        var shader = Shader.Find(FxShaderName);
        if (shader == null)
        {
            if (!_warnedAboutMissingShader)
            {
                Debug.LogWarning($"[ChestShop] Shader '{FxShaderName}' not found.", this);
                _warnedAboutMissingShader = true;
            }

            return;
        }

        _fxMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}



