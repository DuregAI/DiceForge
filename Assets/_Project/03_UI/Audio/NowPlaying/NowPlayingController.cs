using Diceforge.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI.Audio
{
    public sealed class NowPlayingController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private bool startPanelOpen;
        [SerializeField] private bool autoOpenOnTrackChange = true;

        private VisualElement _root;
        private VisualElement _panel;
        private Label _trackNameLabel;
        private Label _likesCountLabel;
        private Button _panelToggleButton;
        private Button _prevButton;
        private Button _nextButton;
        private Button _likeButton;
        private Button _dislikeButton;
        private Button _clearButton;
        private bool _isPanelOpen;

        private void OnEnable()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (audioManager == null)
                audioManager = AudioManager.Instance != null ? AudioManager.Instance : FindAnyObjectByType<AudioManager>();

            _root = uiDocument != null ? uiDocument.rootVisualElement : null;
            if (_root == null || audioManager == null)
                return;

            _trackNameLabel = _root.Q<Label>("TrackNameLabel");
            _panel = _root.Q<VisualElement>("NowPlayingPanel");
            _likesCountLabel = _root.Q<Label>("LikesCountLabel");
            _panelToggleButton = _root.Q<Button>("PanelToggleButton");
            _prevButton = _root.Q<Button>("PrevButton");
            _nextButton = _root.Q<Button>("NextButton");
            _likeButton = _root.Q<Button>("LikeButton");
            _dislikeButton = _root.Q<Button>("DislikeButton");
            _clearButton = _root.Q<Button>("ClearButton");

            _isPanelOpen = startPanelOpen;
            UpdatePanelState();

            if (_panelToggleButton != null)
                _panelToggleButton.clicked += HandleTogglePanelClicked;
            if (_prevButton != null)
                _prevButton.clicked += HandlePrevClicked;
            if (_nextButton != null)
                _nextButton.clicked += HandleNextClicked;

            if (_likeButton != null)
                _likeButton.clicked += HandleLikeClicked;
            if (_dislikeButton != null)
                _dislikeButton.clicked += HandleDislikeClicked;
            if (_clearButton != null)
                _clearButton.clicked += HandleClearClicked;

            audioManager.OnTrackChanged += HandleTrackChanged;
            audioManager.OnVoteChanged += HandleVoteChanged;
            audioManager.OnStatsChanged += HandleStatsChanged;

            RefreshFromCurrentState();
        }

        private void OnDisable()
        {
            if (_panelToggleButton != null)
                _panelToggleButton.clicked -= HandleTogglePanelClicked;
            if (_prevButton != null)
                _prevButton.clicked -= HandlePrevClicked;
            if (_nextButton != null)
                _nextButton.clicked -= HandleNextClicked;
            if (_likeButton != null)
                _likeButton.clicked -= HandleLikeClicked;
            if (_dislikeButton != null)
                _dislikeButton.clicked -= HandleDislikeClicked;
            if (_clearButton != null)
                _clearButton.clicked -= HandleClearClicked;

            if (audioManager != null)
            {
                audioManager.OnTrackChanged -= HandleTrackChanged;
                audioManager.OnVoteChanged -= HandleVoteChanged;
                audioManager.OnStatsChanged -= HandleStatsChanged;
            }
        }

        private void HandleTogglePanelClicked()
        {
            _isPanelOpen = !_isPanelOpen;
            UpdatePanelState();
        }

        private void HandlePrevClicked()
        {
            audioManager?.TryPlayPrev();
        }

        private void HandleNextClicked()
        {
            audioManager?.TryPlayNext();
        }

        private void HandleLikeClicked()
        {
            string trackId = audioManager != null ? audioManager.CurrentTrackId : null;
            if (!string.IsNullOrWhiteSpace(trackId))
                audioManager.SetVote(trackId, TrackVote.Like);
        }

        private void HandleDislikeClicked()
        {
            string trackId = audioManager != null ? audioManager.CurrentTrackId : null;
            if (!string.IsNullOrWhiteSpace(trackId))
                audioManager.SetVote(trackId, TrackVote.Dislike);
        }

        private void HandleClearClicked()
        {
            string trackId = audioManager != null ? audioManager.CurrentTrackId : null;
            if (!string.IsNullOrWhiteSpace(trackId))
                audioManager.SetVote(trackId, TrackVote.Neutral);
        }

        private void HandleTrackChanged(string trackId, string displayName)
        {
            if (autoOpenOnTrackChange)
                EnsurePanelOpen();

            if (_trackNameLabel != null)
                _trackNameLabel.text = $"Now playing: {displayName}";

            TrackVote vote = audioManager != null ? audioManager.GetVote(trackId) : TrackVote.Neutral;
            ApplyVoteState(vote);
            RefreshLikesCount();
        }

        private void HandleVoteChanged(string trackId, TrackVote vote)
        {
            if (audioManager == null)
                return;

            if (trackId == audioManager.CurrentTrackId)
                ApplyVoteState(vote);

            RefreshLikesCount();
        }

        private void HandleStatsChanged()
        {
            RefreshLikesCount();
        }

        private void RefreshFromCurrentState()
        {
            if (audioManager == null)
                return;

            string currentTrackId = audioManager.CurrentTrackId;
            string displayName = audioManager.CurrentTrackDisplayName;

            if (_trackNameLabel != null)
            {
                _trackNameLabel.text = string.IsNullOrWhiteSpace(currentTrackId)
                    ? "Now playing: --"
                    : $"Now playing: {displayName}";
            }

            if (autoOpenOnTrackChange && !string.IsNullOrWhiteSpace(currentTrackId))
                EnsurePanelOpen();

            ApplyVoteState(audioManager.GetVote(currentTrackId));
            RefreshLikesCount();
        }

        private void ApplyVoteState(TrackVote vote)
        {
            if (_panel == null)
                return;

            _panel.RemoveFromClassList("vote-like");
            _panel.RemoveFromClassList("vote-dislike");
            _panel.RemoveFromClassList("vote-neutral");

            switch (vote)
            {
                case TrackVote.Like:
                    _panel.AddToClassList("vote-like");
                    break;
                case TrackVote.Dislike:
                    _panel.AddToClassList("vote-dislike");
                    break;
                default:
                    _panel.AddToClassList("vote-neutral");
                    break;
            }
        }

        private void RefreshLikesCount()
        {
            if (_likesCountLabel == null)
                return;

            int likesCount = audioManager != null ? audioManager.GetTotalLikedTracksCount() : 0;
            _likesCountLabel.text = $"Likes: {likesCount}";
        }

        private void UpdatePanelState()
        {
            if (_panel == null)
                return;

            _panel.EnableInClassList("is-open", _isPanelOpen);
            _panel.EnableInClassList("is-closed", !_isPanelOpen);

            if (_panelToggleButton != null)
                _panelToggleButton.text = _isPanelOpen ? "▶" : "◀";
        }

        private void EnsurePanelOpen()
        {
            if (_panel == null)
                return;

            if (_isPanelOpen || _panel.ClassListContains("is-open"))
                return;

            _isPanelOpen = true;
            UpdatePanelState();
        }
    }
}
