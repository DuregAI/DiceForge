using System;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class AvatarSelectionController : MonoBehaviour
{
    private bool _initialized;
    private VisualElement _selectionModal;
    private VisualElement _shopPlaceholderModal;
    private VisualElement _avatarGrid;
    private Label _statusLabel;
    private Button _shopButton;
    private Button _closeSelectionButton;
    private Button _closeShopPlaceholderButton;

    public void Initialize(VisualElement root)
    {
        if (_initialized || root == null)
            return;

        _selectionModal = root.Q<VisualElement>("AvatarSelectionModal");
        _shopPlaceholderModal = root.Q<VisualElement>("AvatarShopPlaceholderModal");
        _avatarGrid = root.Q<VisualElement>("avatarSelectionGrid");
        _statusLabel = root.Q<Label>("avatarSelectionStatus");
        _shopButton = root.Q<Button>("btnAvatarShop");
        _closeSelectionButton = root.Q<Button>("btnCloseAvatarSelection");
        _closeShopPlaceholderButton = root.Q<Button>("btnCloseAvatarShopPlaceholder");

        if (_shopButton != null)
            _shopButton.clicked += OpenShopPlaceholder;
        if (_closeSelectionButton != null)
            _closeSelectionButton.clicked += CloseSelection;
        if (_closeShopPlaceholderButton != null)
            _closeShopPlaceholderButton.clicked += CloseShopPlaceholder;

        ProfileService.ProfileChanged -= Refresh;
        ProfileService.ProfileChanged += Refresh;

        CloseAll();
        _initialized = true;
    }

    private void OnDestroy()
    {
        ProfileService.ProfileChanged -= Refresh;

        if (_shopButton != null)
            _shopButton.clicked -= OpenShopPlaceholder;
        if (_closeSelectionButton != null)
            _closeSelectionButton.clicked -= CloseSelection;
        if (_closeShopPlaceholderButton != null)
            _closeShopPlaceholderButton.clicked -= CloseShopPlaceholder;
    }

    public void Open()
    {
        CloseShopPlaceholder();
        Refresh();
        SetVisible(_selectionModal, true);
    }

    public void CloseSelection()
    {
        SetVisible(_selectionModal, false);
    }

    public void OpenShopPlaceholder()
    {
        SetVisible(_shopPlaceholderModal, true);
    }

    public void CloseShopPlaceholder()
    {
        SetVisible(_shopPlaceholderModal, false);
    }

    public void CloseAll()
    {
        CloseSelection();
        CloseShopPlaceholder();
    }

    private void Refresh()
    {
        UpdateStatusLabel();
        RebuildAvatarGrid();
    }

    private void UpdateStatusLabel()
    {
        if (_statusLabel == null)
            return;

        ItemDefinition selected = AvatarService.GetSelectedAvatarDefinition();
        if (selected == null)
        {
            _statusLabel.text = "No avatar catalog is available.";
            return;
        }

        _statusLabel.text = $"Selected: {selected.displayName}";
    }

    private void RebuildAvatarGrid()
    {
        if (_avatarGrid == null)
            return;

        _avatarGrid.Clear();

        var definitions = AvatarService.GetAvatarDefinitions();
        for (int i = 0; i < definitions.Count; i++)
        {
            ItemDefinition definition = definitions[i];
            if (definition == null)
                continue;

            _avatarGrid.Add(BuildAvatarTile(definition));
        }
    }

    private VisualElement BuildAvatarTile(ItemDefinition definition)
    {
        bool isUnlocked = AvatarService.IsAvatarUnlocked(definition);
        bool isSelected = string.Equals(definition.id, ProfileService.GetSelectedAvatarId(), StringComparison.Ordinal);

        var tile = new VisualElement();
        tile.AddToClassList("avatar-selection-tile");
        if (isSelected)
            tile.AddToClassList("is-selected");
        if (!isUnlocked)
            tile.AddToClassList("is-locked");

        var previewButton = new Button(() => HandlePreviewPressed(definition, isUnlocked))
        {
            text = string.Empty
        };
        previewButton.AddToClassList("avatar-selection-preview-button");

        var preview = new VisualElement();
        preview.AddToClassList("avatar-selection-preview");
        if (definition.icon != null)
            preview.style.backgroundImage = new StyleBackground(definition.icon);
        previewButton.Add(preview);

        var nameLabel = new Label(string.IsNullOrWhiteSpace(definition.displayName) ? definition.id : definition.displayName);
        nameLabel.AddToClassList("avatar-selection-name");

        var stateLabel = new Label(BuildStateLabel(isUnlocked, isSelected));
        stateLabel.AddToClassList("avatar-selection-state");
        if (!isUnlocked)
            stateLabel.AddToClassList("is-locked");
        else if (isSelected)
            stateLabel.AddToClassList("is-selected");

        Button actionButton;
        if (isUnlocked)
        {
            actionButton = new Button(() => HandleAvatarSelected(definition.id))
            {
                text = isSelected ? "Selected" : "Use Avatar"
            };
            actionButton.SetEnabled(!isSelected);
        }
        else
        {
            actionButton = new Button(OpenShopPlaceholder)
            {
                text = "Get Avatar"
            };
            actionButton.AddToClassList("avatar-selection-action-shop");
        }

        actionButton.AddToClassList("df-button");
        actionButton.AddToClassList("avatar-selection-action");

        tile.Add(previewButton);
        tile.Add(nameLabel);
        tile.Add(stateLabel);
        tile.Add(actionButton);
        return tile;
    }

    private void HandlePreviewPressed(ItemDefinition definition, bool isUnlocked)
    {
        if (definition == null)
            return;

        if (!isUnlocked)
        {
            if (_statusLabel != null)
                _statusLabel.text = "Locked avatar. Visit Avatar Shop to unlock it.";
            return;
        }

        HandleAvatarSelected(definition.id);
    }

    private void HandleAvatarSelected(string avatarId)
    {
        if (!AvatarService.TrySelectAvatar(avatarId))
        {
            if (_statusLabel != null)
                _statusLabel.text = "Unable to equip this avatar.";
            return;
        }

        CloseSelection();
    }

    private static string BuildStateLabel(bool isUnlocked, bool isSelected)
    {
        if (!isUnlocked)
            return "Locked";

        return isSelected ? "Selected" : "Unlocked";
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
