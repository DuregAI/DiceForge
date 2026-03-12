using System;
using Diceforge.Progression;
using Diceforge.Integrations.SpacetimeDb;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PlayerInfoController : MonoBehaviour
{
    public const int MaxPlayerNameLength = 23;

    private bool _initialized;

    private VisualElement _playerInfoModal;
    private VisualElement _renameConfirmModal;
    private VisualElement _renameModal;

    private Label _infoNameLabel;
    private Label _infoLevelLabel;
    private Label _infoXpLabel;

    private Button _closeInfoButton;
    private Button _infoAvatarButton;
    private Button _infoRenameButton;

    private Button _confirmYesButton;
    private Button _confirmCancelButton;

    private TextField _renameField;
    private Button _renameApplyButton;
    private Button _renameCancelButton;

    private AvatarSelectionController _avatarSelectionController;

    public void Initialize(VisualElement root)
    {
        if (_initialized || root == null)
            return;

        _playerInfoModal = root.Q<VisualElement>("PlayerInfoModal");
        _renameConfirmModal = root.Q<VisualElement>("RenameConfirmModal");
        _renameModal = root.Q<VisualElement>("RenamePlayerModal");

        _infoNameLabel = root.Q<Label>("playerInfoName");
        _infoLevelLabel = root.Q<Label>("playerInfoLevel");
        _infoXpLabel = root.Q<Label>("playerInfoXp");

        _closeInfoButton = root.Q<Button>("btnClosePlayerInfo");
        _infoAvatarButton = root.Q<Button>("btnPlayerInfoAvatar");
        _infoRenameButton = root.Q<Button>("btnPlayerInfoRename");

        _confirmYesButton = root.Q<Button>("btnRenameConfirmYes");
        _confirmCancelButton = root.Q<Button>("btnRenameConfirmCancel");

        _renameField = root.Q<TextField>("txtRenamePlayer");
        _renameApplyButton = root.Q<Button>("btnRenameApply");
        _renameCancelButton = root.Q<Button>("btnRenameCancel");

        _avatarSelectionController = GetComponent<AvatarSelectionController>() ?? gameObject.AddComponent<AvatarSelectionController>();
        _avatarSelectionController.Initialize(root);

        if (_renameField != null)
            _renameField.maxLength = MaxPlayerNameLength;

        if (_closeInfoButton != null)
            _closeInfoButton.clicked += CloseInfo;
        if (_infoAvatarButton != null)
            _infoAvatarButton.clicked += OpenAvatarSelection;
        if (_infoRenameButton != null)
            _infoRenameButton.clicked += OpenRenameConfirmation;

        if (_confirmYesButton != null)
            _confirmYesButton.clicked += HandleRenameConfirmYes;
        if (_confirmCancelButton != null)
            _confirmCancelButton.clicked += CloseRenameConfirmation;

        if (_renameApplyButton != null)
            _renameApplyButton.clicked += ApplyRename;
        if (_renameCancelButton != null)
            _renameCancelButton.clicked += CloseRenameModal;

        ProfileService.ProfileChanged -= Refresh;
        ProfileService.ProfileChanged += Refresh;
        ProfileService.OnPlayerNameChanged -= HandlePlayerNameChanged;
        ProfileService.OnPlayerNameChanged += HandlePlayerNameChanged;

        CloseAllModals();
        _initialized = true;
    }

    private void OnDestroy()
    {
        ProfileService.ProfileChanged -= Refresh;
        ProfileService.OnPlayerNameChanged -= HandlePlayerNameChanged;

        if (_closeInfoButton != null)
            _closeInfoButton.clicked -= CloseInfo;
        if (_infoAvatarButton != null)
            _infoAvatarButton.clicked -= OpenAvatarSelection;
        if (_infoRenameButton != null)
            _infoRenameButton.clicked -= OpenRenameConfirmation;

        if (_confirmYesButton != null)
            _confirmYesButton.clicked -= HandleRenameConfirmYes;
        if (_confirmCancelButton != null)
            _confirmCancelButton.clicked -= CloseRenameConfirmation;

        if (_renameApplyButton != null)
            _renameApplyButton.clicked -= ApplyRename;
        if (_renameCancelButton != null)
            _renameCancelButton.clicked -= CloseRenameModal;
    }

    public void Open()
    {
        Refresh();
        SetVisible(_playerInfoModal, true);
    }

    public void OpenRenameConfirmation()
    {
        Open();
        _avatarSelectionController?.CloseAll();
        SetVisible(_renameConfirmModal, true);
    }

    public static string ClampPlayerName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= MaxPlayerNameLength ? value : value.Substring(0, MaxPlayerNameLength);
    }

    private void OpenAvatarSelection()
    {
        Open();
        SetVisible(_renameConfirmModal, false);
        SetVisible(_renameModal, false);
        _avatarSelectionController?.Open();
    }

    private void CloseInfo()
    {
        SetVisible(_playerInfoModal, false);
        SetVisible(_renameConfirmModal, false);
        SetVisible(_renameModal, false);
        _avatarSelectionController?.CloseAll();
    }

    private void CloseRenameConfirmation()
    {
        SetVisible(_renameConfirmModal, false);
    }

    private void HandleRenameConfirmYes()
    {
        SetVisible(_renameConfirmModal, false);
        OpenRenameModal();
    }

    private void OpenRenameModal()
    {
        _avatarSelectionController?.CloseAll();

        if (_renameField != null)
        {
            _renameField.SetValueWithoutNotify(ClampPlayerName(ProfileService.Current.playerName));
        }

        SetVisible(_renameModal, true);

        if (_renameField != null)
        {
            _renameField.schedule.Execute(() =>
            {
                _renameField.Focus();
                _renameField.SelectAll();
            }).ExecuteLater(1);
        }
    }

    private void CloseRenameModal()
    {
        SetVisible(_renameModal, false);
    }

    private void ApplyRename()
    {
        var previousDisplayName = ClampPlayerName(ProfileService.GetDisplayName());
        var newName = ClampPlayerName(_renameField != null ? _renameField.value : string.Empty);
        ProfileService.SetPlayerName(newName);

        var newDisplayName = ClampPlayerName(ProfileService.GetDisplayName());
        if (!string.Equals(previousDisplayName, newDisplayName, StringComparison.Ordinal))
            SpacetimeDbLocalDevRuntime.SubmitPlayerNameChange(previousDisplayName, newDisplayName);

        CloseRenameModal();
    }

    private void HandlePlayerNameChanged(string _)
    {
        Refresh();
    }

    private void Refresh()
    {
        var xp = Mathf.Max(0, ProfileService.Current.hero.xp);
        var level = (xp / 100) + 1;
        var levelFloorXp = (level - 1) * 100;
        var displayName = ClampPlayerName(ProfileService.GetDisplayName());
        var avatar = AvatarService.GetSelectedAvatarSprite();

        if (_infoNameLabel != null)
            _infoNameLabel.text = displayName;
        if (_infoLevelLabel != null)
            _infoLevelLabel.text = $"Lv {level}";
        if (_infoXpLabel != null)
            _infoXpLabel.text = $"XP {xp} ({Mathf.Max(0, xp - levelFloorXp)}/{UiProgressionService.XpPerLevel} to Lv {level + 1})";
        if (_infoAvatarButton != null)
            _infoAvatarButton.style.backgroundImage = avatar == null ? StyleKeyword.None : new StyleBackground(avatar);
    }

    private void CloseAllModals()
    {
        SetVisible(_playerInfoModal, false);
        SetVisible(_renameConfirmModal, false);
        SetVisible(_renameModal, false);
        _avatarSelectionController?.CloseAll();
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
