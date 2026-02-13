using Diceforge.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI.Audio
{
    public sealed class NowPlayingController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private AudioManager audioManager;

        private VisualElement _root;
        private Label _trackNameLabel;
        private Button _likeButton;
        private Button _dislikeButton;
        private Button _clearButton;

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
            _likeButton = _root.Q<Button>("LikeButton");
            _dislikeButton = _root.Q<Button>("DislikeButton");
            _clearButton = _root.Q<Button>("ClearButton");

            if (_likeButton != null)
                _likeButton.clicked += HandleLikeClicked;
            if (_dislikeButton != null)
                _dislikeButton.clicked += HandleDislikeClicked;
            if (_clearButton != null)
                _clearButton.clicked += HandleClearClicked;

            audioManager.OnTrackChanged += HandleTrackChanged;
            audioManager.OnVoteChanged += HandleVoteChanged;

            RefreshFromCurrentState();
        }

        private void OnDisable()
        {
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
            }
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
            if (_trackNameLabel != null)
                _trackNameLabel.text = $"Now playing: {displayName}";

            TrackVote vote = audioManager != null ? audioManager.GetVote(trackId) : TrackVote.Neutral;
            ApplyVoteState(vote);
        }

        private void HandleVoteChanged(string trackId, TrackVote vote)
        {
            if (audioManager == null)
                return;

            if (trackId == audioManager.CurrentTrackId)
                ApplyVoteState(vote);
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

            ApplyVoteState(audioManager.GetVote(currentTrackId));
        }

        private void ApplyVoteState(TrackVote vote)
        {
            if (_root == null)
                return;

            _root.RemoveFromClassList("vote-like");
            _root.RemoveFromClassList("vote-dislike");
            _root.RemoveFromClassList("vote-neutral");

            switch (vote)
            {
                case TrackVote.Like:
                    _root.AddToClassList("vote-like");
                    break;
                case TrackVote.Dislike:
                    _root.AddToClassList("vote-dislike");
                    break;
                default:
                    _root.AddToClassList("vote-neutral");
                    break;
            }
        }
    }
}
