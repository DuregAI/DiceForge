using System;
using System.Collections;
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

        public static AudioManager Instance { get; private set; }

        public event Action<string, string> OnTrackChanged;
        public event Action<string, TrackVote> OnVoteChanged;
        public event Action<float, float> OnVolumesChanged;

        public string CurrentTrackId => _currentTrackId;

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
            if (musicSource != null && musicSource.isPlaying && musicSource.clip != null)
                return;

            TryPlayNext();
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
                    TryPlayNext();

                yield return wait;
            }
        }

        private void TryPlayNext()
        {
            if (musicLibrary == null)
            {
                WarnMissingLibraryOnce("[AudioManager] MusicLibrary is not assigned.");
                return;
            }

            var enabledTracks = musicLibrary.GetEnabledTracks();
            if (enabledTracks == null || enabledTracks.Count == 0)
            {
                WarnMissingLibraryOnce("[AudioManager] MusicLibrary has no enabled tracks with clips.");
                return;
            }

            TrackDef next = _selector.PickNextTrack(enabledTracks, _prefs, _currentTrackId);
            if (next == null)
                return;

            PlayTrack(next);
        }

        private void PlayTrack(TrackDef def)
        {
            if (def == null || musicSource == null)
                return;

            musicSource.clip = def.clip;
            musicSource.loop = false;
            musicSource.Play();

            _currentTrackId = def.id;
            _missingLibraryWarned = false;
            OnTrackChanged?.Invoke(def.id, musicLibrary.GetDisplayName(def.id));
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
