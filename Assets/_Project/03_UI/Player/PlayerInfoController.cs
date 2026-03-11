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
    private Button _infoNameButton;
    private Button _infoRenameButton;

    private Button _confirmYesButton;
    private Button _confirmCancelButton;

    private TextField _renameField;
    private Button _renameApplyButton;
    private Button _renameCancelButton;

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
        _infoNameButton = root.Q<Button>("btnPlayerInfoName");
        _infoRenameButton = root.Q<Button>("btnPlayerInfoRename");

        _confirmYesButton = root.Q<Button>("btnRenameConfirmYes");
        _confirmCancelButton = root.Q<Button>("btnRenameConfirmCancel");

        _renameField = root.Q<TextField>("txtRenamePlayer");
        _renameApplyButton = root.Q<Button>("btnRenameApply");
        _renameCancelButton = root.Q<Button>("btnRenameCancel");

        if (_renameField != null)
            _renameField.maxLength = MaxPlayerNameLength;

        _closeInfoButton.clicked += CloseInfo;
        _infoAvatarButton.clicked += OpenRenameConfirmation;
        _infoNameButton.clicked += OpenRenameConfirmation;
        _infoRenameButton.clicked += OpenRenameConfirmation;

        _confirmYesButton.clicked += HandleRenameConfirmYes;
        _confirmCancelButton.clicked += CloseRenameConfirmation;

        _renameApplyButton.clicked += ApplyRename;
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

        _closeInfoButton.clicked -= CloseInfo;
        _infoAvatarButton.clicked -= OpenRenameConfirmation;
        _infoNameButton.clicked -= OpenRenameConfirmation;
        _infoRenameButton.clicked -= OpenRenameConfirmation;

        _confirmYesButton.clicked -= HandleRenameConfirmYes;
        _confirmCancelButton.clicked -= CloseRenameConfirmation;

        _renameApplyButton.clicked -= ApplyRename;
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
        SetVisible(_renameConfirmModal, true);
    }

    public static string ClampPlayerName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= MaxPlayerNameLength ? value : value.Substring(0, MaxPlayerNameLength);
    }

    private void CloseInfo()
    {
        SetVisible(_playerInfoModal, false);
        SetVisible(_renameConfirmModal, false);
        SetVisible(_renameModal, false);
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

        if (_infoNameLabel != null)
            _infoNameLabel.text = displayName;
        if (_infoNameButton != null)
            _infoNameButton.text = displayName;
        if (_infoLevelLabel != null)
            _infoLevelLabel.text = $"Lv {level}";
        if (_infoXpLabel != null)
            _infoXpLabel.text = $"XP {xp} ({Mathf.Max(0, xp - levelFloorXp)}/{UiProgressionService.XpPerLevel} to Lv {level + 1})";
    }

    private void CloseAllModals()
    {
        SetVisible(_playerInfoModal, false);
        SetVisible(_renameConfirmModal, false);
        SetVisible(_renameModal, false);
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        if (element != null)
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}




