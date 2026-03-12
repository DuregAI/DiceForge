using Diceforge.Dialogue;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class PlayerPanelController : MonoBehaviour
{
    [SerializeField] private TutorialPortraitLibrary tutorialPortraitLibrary;

    private bool _initialized;
    private Label _playerNameLabel;
    private Label _playerLevelLabel;
    private Button _avatarButton;
    private Button _renameButton;
    private PlayerInfoController _playerInfoController;

    public void SetPortraitLibrary(TutorialPortraitLibrary portraitLibrary)
    {
        tutorialPortraitLibrary = portraitLibrary;
    }

    public void Initialize(VisualElement root)
    {
        if (_initialized || root == null)
            return;

        _playerNameLabel = root.Q<Label>("playerPanelName");
        _playerLevelLabel = root.Q<Label>("playerPanelLevel");
        _avatarButton = root.Q<Button>("btnPlayerAvatar");
        _renameButton = root.Q<Button>("btnPlayerRename");

        _playerInfoController = GetComponent<PlayerInfoController>() ?? gameObject.AddComponent<PlayerInfoController>();
        _playerInfoController.Initialize(root);

        if (_avatarButton != null)
            _avatarButton.clicked += HandleAvatarClicked;
        if (_renameButton != null)
            _renameButton.clicked += HandleRenameClicked;

        ProfileService.OnPlayerNameChanged -= HandlePlayerNameChanged;
        ProfileService.OnPlayerNameChanged += HandlePlayerNameChanged;
        ProfileService.ProfileChanged -= Refresh;
        ProfileService.ProfileChanged += Refresh;

        Refresh();
        _initialized = true;
    }

    private void OnDestroy()
    {
        ProfileService.OnPlayerNameChanged -= HandlePlayerNameChanged;
        ProfileService.ProfileChanged -= Refresh;

        if (_avatarButton != null)
            _avatarButton.clicked -= HandleAvatarClicked;
        if (_renameButton != null)
            _renameButton.clicked -= HandleRenameClicked;
    }

    private void HandleAvatarClicked()
    {
        _playerInfoController?.Open();
    }

    private void HandleRenameClicked()
    {
        _playerInfoController?.OpenRenameConfirmation();
    }

    private void HandlePlayerNameChanged(string _)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_playerNameLabel != null)
            _playerNameLabel.text = PlayerInfoController.ClampPlayerName(ProfileService.GetDisplayName());

        if (_playerLevelLabel != null)
            _playerLevelLabel.text = $"Lv {UiProgressionService.GetPlayerLevel()}";

        if (_avatarButton != null)
        {
            var avatar = AvatarService.GetSelectedAvatarSprite();
            _avatarButton.style.backgroundImage = avatar == null ? StyleKeyword.None : new StyleBackground(avatar);
        }
    }
}
