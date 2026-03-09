using System;
using System.Collections.Generic;
using Diceforge.Audio;
using Diceforge.Diagnostics;
using Diceforge.Integrations.SpacetimeDb;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Diceforge.UI.Audio
{
    public sealed class NowPlayingController : MonoBehaviour
    {
        private const string VisibleClass = "is-visible";
        private const float TransitionSeconds = 0.2f;
        private const string SettingsStateClass = "state-settings";
        private const string BackStateClass = "state-back";
        private const string FeedbackDefaultStatus = "Tell us what happened.";
        private static readonly List<string> FeedbackCategories = new() { "bug", "balance", "ui", "audio", "other" };

        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private bool startPanelOpen;
        [SerializeField] private bool autoOpenOnTrackChange = true;

        private VisualElement _root;
        private VisualElement _nowPlayingRoot;
        private VisualElement _panel;
        private VisualElement _settingsPanel;
        private VisualElement _feedbackModal;
        private VisualElement _corners;
        private VisualElement _copyLogTooltip;
        private Label _trackNameLabel;
        private Label _likesCountLabel;
        private Label _aboutVersionLabel;
        private Label _buildInfoLabel;
        private Label _feedbackStatusLabel;
        private Button _panelToggleButton;
        private Button _prevButton;
        private Button _nextButton;
        private Button _likeButton;
        private Button _dislikeButton;
        private Button _clearButton;
        private Button _settingsButton;
        private Button _copyLogButton;
        private Button _openFeedbackButton;
        private Button _feedbackSubmitButton;
        private Button _feedbackCancelButton;
        private Slider _musicSlider;
        private Slider _sfxSlider;
        private DropdownField _feedbackCategoryField;
        private TextField _feedbackMessageField;
        private bool _isPanelOpen;
        private bool _isSettingsOpen;
        private bool _isFeedbackOpen;

        private void OnEnable()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>();

            if (audioManager == null)
                audioManager = AudioManager.Instance != null ? AudioManager.Instance : FindAnyObjectByType<AudioManager>();

            _root = uiDocument != null ? uiDocument.rootVisualElement : null;
            if (_root == null || audioManager == null)
                return;

            // The document root spans the whole screen, so let only visible UI panels receive pointer input.
            _root.pickingMode = PickingMode.Ignore;


            _nowPlayingRoot = _root.Q<VisualElement>("NowPlayingRoot");
            _trackNameLabel = _root.Q<Label>("TrackNameLabel");
            _panel = _root.Q<VisualElement>("NowPlayingPanel");
            _settingsPanel = _root.Q<VisualElement>("SettingsPanel");
            _feedbackModal = _root.Q<VisualElement>("FeedbackModal");
            _corners = _root.Q<VisualElement>("Corners");
            _copyLogTooltip = _root.Q<VisualElement>("copyLogTooltip");
            _likesCountLabel = _root.Q<Label>("LikesCountLabel");
            _aboutVersionLabel = _root.Q<Label>("lblAboutVersion");
            _buildInfoLabel = _root.Q<Label>("lblBuildInfo");
            _feedbackStatusLabel = _root.Q<Label>("feedbackStatusLabel");
            _panelToggleButton = _root.Q<Button>("PanelToggleButton");
            _prevButton = _root.Q<Button>("PrevButton");
            _nextButton = _root.Q<Button>("NextButton");
            _likeButton = _root.Q<Button>("LikeButton");
            _dislikeButton = _root.Q<Button>("DislikeButton");
            _clearButton = _root.Q<Button>("ClearButton");
            _settingsButton = _root.Q<Button>("btnSettings");
            _copyLogButton = _root.Q<Button>("btnCopyLog");
            _openFeedbackButton = _root.Q<Button>("btnOpenFeedback");
            _feedbackSubmitButton = _root.Q<Button>("btnFeedbackSubmit");
            _feedbackCancelButton = _root.Q<Button>("btnFeedbackCancel");
            _musicSlider = _root.Q<Slider>("sliderMusicVolume");
            _sfxSlider = _root.Q<Slider>("sliderSfxVolume");
            _feedbackCategoryField = _root.Q<DropdownField>("feedbackCategoryField");
            _feedbackMessageField = _root.Q<TextField>("feedbackMessageField");

            _isPanelOpen = startPanelOpen;
            _isSettingsOpen = false;
            _isFeedbackOpen = false;

            ConfigureHitTesting();

            UpdatePanelState();
            InitializeSettingsPanel();
            InitializeAboutSection();
            InitializeAudioSliders();
            InitializeCopyLogTooltip();
            InitializeFeedbackForm();
            UpdateSettingsButtonState(false);

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
            if (_settingsButton != null)
                _settingsButton.clicked += ToggleSettingsPanel;
            if (_copyLogButton != null)
                _copyLogButton.clicked += CopyLogToClipboard;
            if (_openFeedbackButton != null)
                _openFeedbackButton.clicked += OpenFeedbackForm;
            if (_feedbackSubmitButton != null)
                _feedbackSubmitButton.clicked += SubmitFeedback;
            if (_feedbackCancelButton != null)
                _feedbackCancelButton.clicked += CloseFeedbackForm;

            audioManager.OnTrackChanged += HandleTrackChanged;
            audioManager.OnVoteChanged += HandleVoteChanged;
            audioManager.OnStatsChanged += HandleStatsChanged;
            audioManager.OnVolumesChanged += HandleAudioVolumesChanged;

            RefreshAudioSlidersFromManager();
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
            if (_settingsButton != null)
                _settingsButton.clicked -= ToggleSettingsPanel;
            if (_copyLogButton != null)
                _copyLogButton.clicked -= CopyLogToClipboard;
            if (_openFeedbackButton != null)
                _openFeedbackButton.clicked -= OpenFeedbackForm;
            if (_feedbackSubmitButton != null)
                _feedbackSubmitButton.clicked -= SubmitFeedback;
            if (_feedbackCancelButton != null)
                _feedbackCancelButton.clicked -= CloseFeedbackForm;

            if (_musicSlider != null)
                _musicSlider.UnregisterValueChangedCallback(OnMusicSliderChanged);
            if (_sfxSlider != null)
                _sfxSlider.UnregisterValueChangedCallback(OnSfxSliderChanged);

            if (audioManager != null)
            {
                audioManager.OnTrackChanged -= HandleTrackChanged;
                audioManager.OnVoteChanged -= HandleVoteChanged;
                audioManager.OnStatsChanged -= HandleStatsChanged;
                audioManager.OnVolumesChanged -= HandleAudioVolumesChanged;
            }
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            if (_isFeedbackOpen)
            {
                CloseFeedbackForm();
                return;
            }

            ToggleSettingsPanel();
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
            if (string.IsNullOrWhiteSpace(trackId))
                return;

            audioManager.SetVote(trackId, TrackVote.Like);
            SpacetimeDbLocalDevRuntime.SubmitMusicTrackLike(trackId);
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

        private void HandleAudioVolumesChanged(float musicVolume, float sfxVolume)
        {
            if (_musicSlider != null)
                _musicSlider.SetValueWithoutNotify(musicVolume);

            if (_sfxSlider != null)
                _sfxSlider.SetValueWithoutNotify(sfxVolume);
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
                _panelToggleButton.text = _isPanelOpen ? "Close" : "Open";
        }

        private void ConfigureHitTesting()
        {
            if (_root != null)
                _root.pickingMode = PickingMode.Ignore;

            if (_nowPlayingRoot != null)
                _nowPlayingRoot.pickingMode = PickingMode.Ignore;

            if (_corners != null)
                _corners.pickingMode = PickingMode.Ignore;

            if (_panel != null)
                _panel.pickingMode = PickingMode.Position;

            if (_settingsPanel != null)
                _settingsPanel.pickingMode = _isSettingsOpen ? PickingMode.Position : PickingMode.Ignore;

            if (_feedbackModal != null)
                _feedbackModal.pickingMode = _isFeedbackOpen ? PickingMode.Position : PickingMode.Ignore;
        }

        private void InitializeSettingsPanel()
        {
            if (_settingsPanel == null)
                return;

            _settingsPanel.style.display = DisplayStyle.None;
            _settingsPanel.RemoveFromClassList(VisibleClass);
            ConfigureHitTesting();
        }

        private void ToggleSettingsPanel()
        {
            SetSettingsOpen(!_isSettingsOpen);
        }

        private void SetSettingsOpen(bool isOpen)
        {
            _isSettingsOpen = isOpen;
            UpdateSettingsButtonState(_isSettingsOpen);

            if (_settingsPanel == null)
                return;

            if (_isSettingsOpen)
            {
                _settingsPanel.style.display = DisplayStyle.Flex;
                ConfigureHitTesting();
                _settingsPanel.RemoveFromClassList(VisibleClass);
                _settingsPanel.schedule.Execute(() =>
                {
                    if (_isSettingsOpen)
                        _settingsPanel.AddToClassList(VisibleClass);
                }).ExecuteLater(1);
                return;
            }

            CloseFeedbackForm();
            _settingsPanel.RemoveFromClassList(VisibleClass);
            _settingsPanel.schedule.Execute(() =>
            {
                if (!_isSettingsOpen)
                    _settingsPanel.style.display = DisplayStyle.None;
            }).ExecuteLater(Mathf.RoundToInt(TransitionSeconds * 1000f));
        }

        private void UpdateSettingsButtonState(bool isSettingsOpen)
        {
            if (_settingsButton == null)
                return;

            _settingsButton.EnableInClassList(SettingsStateClass, !isSettingsOpen);
            _settingsButton.EnableInClassList(BackStateClass, isSettingsOpen);
        }

        private void InitializeAboutSection()
        {
            if (_aboutVersionLabel != null)
                _aboutVersionLabel.text = $"Version {Application.version}";
        }

        private void InitializeAudioSliders()
        {
            if (_musicSlider != null)
                _musicSlider.RegisterValueChangedCallback(OnMusicSliderChanged);

            if (_sfxSlider != null)
                _sfxSlider.RegisterValueChangedCallback(OnSfxSliderChanged);
        }

        private void OnMusicSliderChanged(ChangeEvent<float> evt)
        {
            audioManager ??= AudioManager.Instance != null
                ? AudioManager.Instance
                : FindAnyObjectByType<AudioManager>();

            audioManager?.SetMusicVolume(evt.newValue);
        }

        private void OnSfxSliderChanged(ChangeEvent<float> evt)
        {
            audioManager ??= AudioManager.Instance != null
                ? AudioManager.Instance
                : FindAnyObjectByType<AudioManager>();

            audioManager?.SetSfxVolume(evt.newValue);
        }

        private void RefreshAudioSlidersFromManager()
        {
            audioManager ??= AudioManager.Instance != null
                ? AudioManager.Instance
                : FindAnyObjectByType<AudioManager>();

            if (audioManager == null)
                return;

            if (_musicSlider != null)
                _musicSlider.SetValueWithoutNotify(audioManager.MusicVolume);

            if (_sfxSlider != null)
                _sfxSlider.SetValueWithoutNotify(audioManager.SfxVolume);
        }

        private void InitializeCopyLogTooltip()
        {
            if (_copyLogButton == null || _copyLogTooltip == null)
                return;

            _copyLogTooltip.style.display = DisplayStyle.None;
            _copyLogButton.RegisterCallback<MouseEnterEvent>(_ => _copyLogTooltip.style.display = DisplayStyle.Flex);
            _copyLogButton.RegisterCallback<MouseLeaveEvent>(_ => _copyLogTooltip.style.display = DisplayStyle.None);
        }

        private void CopyLogToClipboard()
        {
            string buildInfo = _buildInfoLabel != null ? _buildInfoLabel.text : "Build: unknown";
            string logPayload = ClientDiagnostics.BuildSupportLog(buildInfo);
            GUIUtility.systemCopyBuffer = logPayload;
            Debug.Log("[NowPlaying] Log copied", this);
        }

        private void InitializeFeedbackForm()
        {
            if (_feedbackModal != null)
                _feedbackModal.style.display = DisplayStyle.None;

            ConfigureHitTesting();

            if (_feedbackCategoryField != null)
            {
                _feedbackCategoryField.choices = FeedbackCategories;
                _feedbackCategoryField.index = 0;
            }

            if (_feedbackMessageField != null)
            {
                _feedbackMessageField.multiline = true;
                _feedbackMessageField.maxLength = SpacetimeDbFeedbackSink.MaxMessageLength;
                _feedbackMessageField.SetValueWithoutNotify(string.Empty);
            }

            SetFeedbackStatus(FeedbackDefaultStatus, false);
        }

        private void OpenFeedbackForm()
        {
            if (_feedbackModal == null)
                return;

            if (_feedbackCategoryField != null)
                _feedbackCategoryField.index = 0;

            if (_feedbackMessageField != null)
                _feedbackMessageField.SetValueWithoutNotify(string.Empty);

            SetFeedbackStatus(FeedbackDefaultStatus, false);
            _feedbackModal.style.display = DisplayStyle.Flex;
            _feedbackModal.BringToFront();
            _isFeedbackOpen = true;
            ConfigureHitTesting();
            _feedbackMessageField?.Focus();
        }

        private void CloseFeedbackForm()
        {
            if (_feedbackModal != null)
                _feedbackModal.style.display = DisplayStyle.None;

            _isFeedbackOpen = false;
            ConfigureHitTesting();
        }

        private void SubmitFeedback()
        {
            string category = _feedbackCategoryField != null ? _feedbackCategoryField.value : string.Empty;
            string message = _feedbackMessageField != null ? _feedbackMessageField.value : string.Empty;
            string trimmedMessage = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();

            if (string.IsNullOrWhiteSpace(category))
            {
                SetFeedbackStatus("Choose a category.", true);
                return;
            }

            if (string.IsNullOrWhiteSpace(trimmedMessage))
            {
                SetFeedbackStatus("Enter feedback before submitting.", true);
                return;
            }

            if (trimmedMessage.Length > SpacetimeDbFeedbackSink.MaxMessageLength)
            {
                SetFeedbackStatus($"Feedback is limited to {SpacetimeDbFeedbackSink.MaxMessageLength} characters.", true);
                return;
            }

            SpacetimeDbLocalDevRuntime.SubmitFeedback(
                category,
                trimmedMessage,
                Application.version,
                SceneManager.GetActiveScene().name);

            if (_feedbackMessageField != null)
                _feedbackMessageField.SetValueWithoutNotify(string.Empty);

            SetFeedbackStatus("Feedback submitted.", false);
            Debug.Log($"[NowPlaying] Feedback submitted category={category}", this);
        }

        private void SetFeedbackStatus(string message, bool isError)
        {
            if (_feedbackStatusLabel == null)
                return;

            _feedbackStatusLabel.text = message ?? string.Empty;
            _feedbackStatusLabel.style.color = isError
                ? new StyleColor(new Color(1f, 0.68f, 0.68f, 1f))
                : new StyleColor(new Color(0.96f, 0.92f, 0.69f, 1f));
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
