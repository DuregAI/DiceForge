using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Audio
{
    public sealed class AudioManager : MonoBehaviour
    {
        [SerializeField] private MusicLibrary musicLibrary;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private bool dontDestroyOnLoad = true;

        private PlayerMusicPrefsStorage _storage;
        private MusicSelector _selector;
        private Coroutine _playbackWatcher;
        private PlayerMusicPrefs _prefs;
        private string _currentTrackId;
        private bool _missingLibraryWarned;
        private readonly List<string> _trackHistory = new();
        private int _historyIndex = -1;
        private const int MaxHistorySize = 20;
        private MusicContext _activeContext = MusicContext.Gameplay;

        public static AudioManager Instance { get; private set; }

        public event Action<string, string> OnTrackChanged;
        public event Action<string, TrackVote> OnVoteChanged;
        public event Action<float, float> OnVolumesChanged;
        public event Action OnStatsChanged;

        public string CurrentTrackId => _currentTrackId;
        public MusicContext ActiveContext => _activeContext;
        public float MusicVolume => _prefs != null ? Mathf.Clamp01(_prefs.musicVolume) : 1f;
        public float SfxVolume => _prefs != null ? Mathf.Clamp01(_prefs.sfxVolume) : 1f;

        public string CurrentTrackDisplayName => musicLibrary != null
            ? musicLibrary.GetDisplayName(_currentTrackId)
            : $"Unknown ({_currentTrackId})";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
                DontDestroyOnLoad(gameObject);

            _storage = new PlayerMusicPrefsStorage();
            _selector = new MusicSelector();
            _prefs = _storage.LoadOrCreate();

            if (musicSource == null)
                musicSource = GetComponent<AudioSource>();

            ApplyVolumes();
        }

        private void OnEnable()
        {
            if (_playbackWatcher == null)
                _playbackWatcher = StartCoroutine(PlaybackWatcher());
        }

        private void OnDisable()
        {
            if (_playbackWatcher != null)
            {
                StopCoroutine(_playbackWatcher);
                _playbackWatcher = null;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void EnsureGameplayMusic()
        {
            EnsureMusicForContext(MusicContext.Gameplay);
        }

        public bool EnsureMusicForContext(MusicContext context)
        {
            _activeContext = context;

            if (musicSource != null && musicSource.isPlaying && musicSource.clip != null &&
                !string.IsNullOrWhiteSpace(_currentTrackId) &&
                musicLibrary != null &&
                musicLibrary.IsTrackAllowedInContext(_currentTrackId, _activeContext))
            {
                return true;
            }

            return TryPlayNext(false);
        }

        public void StopMusic()
        {
            if (musicSource != null)
                musicSource.Stop();
        }

        public TrackVote GetVote(string trackId)
        {
            return _prefs != null ? _prefs.GetVote(trackId) : TrackVote.Neutral;
        }

        public void SetVote(string trackId, TrackVote vote)
        {
            if (string.IsNullOrWhiteSpace(trackId) || _prefs == null)
                return;

            bool changed = _prefs.SetVote(trackId, vote);
            if (!changed)
                return;

            _storage.Save(_prefs);
            OnVoteChanged?.Invoke(trackId, _prefs.GetVote(trackId));
            OnStatsChanged?.Invoke();
        }

        public int GetTotalLikedTracksCount()
        {
            return _prefs != null ? _prefs.CountVotes(TrackVote.Like) : 0;
        }

        public bool TryPlayNext(bool userInitiated = true)
        {
            if (TryPlayFromHistoryOffset(1, userInitiated))
                return true;

            if (musicLibrary == null)
            {
                WarnMissingLibraryOnce("[AudioManager] MusicLibrary is not assigned.");
                return false;
            }

            var enabledTracks = musicLibrary.GetTracksForContext(_activeContext);
            if (enabledTracks == null || enabledTracks.Count == 0)
            {
                WarnMissingLibraryOnce($"[AudioManager] MusicLibrary has no enabled tracks for context {_activeContext}.");
                return false;
            }

            TrackDef next = _selector.PickNextTrack(enabledTracks, _prefs, _currentTrackId);
            if (next == null)
                return false;

            PlayTrack(next, true);
            return true;
        }

        public bool TryPlayPrev()
        {
            return TryPlayFromHistoryOffset(-1, true);
        }

        public void SetMusicVolume(float v)
        {
            if (_prefs == null)
                return;

            float clamped = Mathf.Clamp01(v);
            if (Mathf.Approximately(_prefs.musicVolume, clamped))
                return;

            _prefs.musicVolume = clamped;
            ApplyVolumes();
            _storage.Save(_prefs);
            OnVolumesChanged?.Invoke(_prefs.musicVolume, _prefs.sfxVolume);
        }

        public void SetSfxVolume(float v)
        {
            if (_prefs == null)
                return;

            float clamped = Mathf.Clamp01(v);
            if (Mathf.Approximately(_prefs.sfxVolume, clamped))
                return;

            _prefs.sfxVolume = clamped;
            ApplyVolumes();
            _storage.Save(_prefs);
            OnVolumesChanged?.Invoke(_prefs.musicVolume, _prefs.sfxVolume);
        }

        private IEnumerator PlaybackWatcher()
        {
            var wait = new WaitForSeconds(0.2f);
            while (true)
            {
                if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
                    TryPlayNext(false);

                yield return wait;
            }
        }

        private bool TryPlayFromHistoryOffset(int offset, bool userInitiated)
        {
            if (!userInitiated)
                return false;

            int targetIndex = _historyIndex + offset;
            if (targetIndex < 0 || targetIndex >= _trackHistory.Count)
                return false;

            string targetTrackId = _trackHistory[targetIndex];
            if (string.IsNullOrWhiteSpace(targetTrackId) || musicLibrary == null)
                return false;

            if (!musicLibrary.TryGetById(targetTrackId, out TrackDef targetTrack) || targetTrack == null)
                return false;

            _historyIndex = targetIndex;
            PlayTrack(targetTrack, false);
            return true;
        }

        private void PlayTrack(TrackDef def, bool recordHistory)
        {
            if (def == null || musicSource == null)
            {
                return;
            }

            musicSource.clip = def.clip;
            musicSource.loop = false;
            musicSource.Play();

            _currentTrackId = def.id;
            if (recordHistory)
                RegisterTrackInHistory(def.id);

            _missingLibraryWarned = false;
            OnTrackChanged?.Invoke(def.id, musicLibrary.GetDisplayName(def.id));
        }

        private void RegisterTrackInHistory(string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId))
                return;

            if (_historyIndex >= 0 && _historyIndex < _trackHistory.Count && _trackHistory[_historyIndex] == trackId)
                return;

            if (_historyIndex < _trackHistory.Count - 1)
                _trackHistory.RemoveRange(_historyIndex + 1, _trackHistory.Count - (_historyIndex + 1));

            _trackHistory.Add(trackId);
            if (_trackHistory.Count > MaxHistorySize)
                _trackHistory.RemoveAt(0);

            _historyIndex = _trackHistory.Count - 1;
        }

        private void WarnMissingLibraryOnce(string message)
        {
            if (_missingLibraryWarned)
                return;

            _missingLibraryWarned = true;
            Debug.LogWarning(message, this);
        }

        private void ApplyVolumes()
        {
            if (_prefs == null)
                return;

            if (musicSource != null)
                musicSource.volume = Mathf.Clamp01(_prefs.musicVolume);

            if (sfxSource != null)
                sfxSource.volume = Mathf.Clamp01(_prefs.sfxVolume);
        }
    }
}
