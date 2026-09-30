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
        [SerializeField] private AudioClip defaultUiClickClip;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField, Min(0f)] private float musicCrossfadeSeconds = 1.25f;

        private AudioSource _otherMusicSource;
        private AudioSource _voiceSource;
        private Coroutine _musicFade;
        private float _musicGain = 1f;
        private float _otherMusicGain;
        private PlayerMusicPrefsStorage _storage;
        private MusicSelector _selector;
        private Coroutine _playbackWatcher;
        private PlayerMusicPrefs _prefs;
        private string _currentTrackId;
        private bool _missingLibraryWarned;
        private bool _missingSfxSourceWarned;
        private bool _missingUiClickClipWarned;
        private int _lastUiClickFrame = -1;
        private readonly List<string> _trackHistory = new();
        private int _historyIndex = -1;
        private const int MaxHistorySize = 20;
        private MusicContext _activeContext = MusicContext.Gameplay;

        public static AudioManager Instance { get; private set; }

        public event Action<string, string> OnTrackChanged;
        public event Action<string, TrackVote> OnVoteChanged;
        public event Action<float, float> OnVolumesChanged;
        public event Action<bool> OnMuteChanged;
        public event Action OnStatsChanged;

        public string CurrentTrackId => _currentTrackId;
        public MusicContext ActiveContext => _activeContext;
        public float MusicVolume => _prefs != null ? Mathf.Clamp01(_prefs.musicVolume) : 1f;
        public float SfxVolume => _prefs != null ? Mathf.Clamp01(_prefs.sfxVolume) : 1f;
        public bool IsMuted => _prefs != null && _prefs.isMuted;
        public long CurrentTrackElapsedMs => GetCurrentTrackElapsedMs();

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

            _otherMusicSource = gameObject.AddComponent<AudioSource>();
            _otherMusicSource.playOnAwake = false;
            _otherMusicSource.spatialBlend = 0f;
            if (musicSource != null)
                _otherMusicSource.outputAudioMixerGroup = musicSource.outputAudioMixerGroup;

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
            _voiceSource.spatialBlend = 0f;
            if (sfxSource != null)
                _voiceSource.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;

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
            if (_musicFade != null)
            {
                StopCoroutine(_musicFade);
                _musicFade = null;
            }
            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.clip = null;
            }
            if (_otherMusicSource != null)
            {
                _otherMusicSource.Stop();
                _otherMusicSource.clip = null;
            }
            _currentTrackId = null;
            _musicGain = 1f;
            _otherMusicGain = 0f;
        }

        public void PlayVoice(AudioClip clip)
        {
            if (_voiceSource == null || clip == null)
                return;

            _voiceSource.Stop();
            _voiceSource.clip = clip;
            ApplyVolumes();
            _voiceSource.Play();
            ApplyVolumes();
        }

        public void StopVoice()
        {
            if (_voiceSource == null)
                return;

            _voiceSource.Stop();
            _voiceSource.clip = null;
            ApplyVolumes();
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

        public void SetMuted(bool muted)
        {
            if (_prefs == null || _prefs.isMuted == muted)
                return;

            _prefs.isMuted = muted;
            ApplyVolumes();
            _storage.Save(_prefs);
            OnMuteChanged?.Invoke(muted);
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

        public void PlayUiClick(AudioClip clipOverride = null)
        {
            bool isDefaultClick = clipOverride == null || clipOverride == defaultUiClickClip;
            if (isDefaultClick && _lastUiClickFrame == Time.frameCount) return;
            if (isDefaultClick) _lastUiClickFrame = Time.frameCount;
            AudioClip clip = clipOverride != null ? clipOverride : defaultUiClickClip;
            if (clip == null)
            {
                WarnMissingUiClickClipOnce();
                return;
            }

            if (sfxSource == null)
            {
                WarnMissingSfxSourceOnce();
                return;
            }

            sfxSource.PlayOneShot(clip, Mathf.Clamp01(SfxVolume));
        }

        public void PlayGameSfx(AudioClip clip, float gain = 1f)
        {
            if (clip == null || sfxSource == null || IsMuted)
                return;

            // Apply the saved SFX volume on the AudioSource, once.
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(gain));
        }

        private IEnumerator PlaybackWatcher()
        {
            var wait = new WaitForSeconds(0.2f);
            while (true)
            {
                if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying && _musicFade == null)
                    TryPlayNext(false);

                if (_voiceSource != null && !_voiceSource.isPlaying && _voiceSource.clip != null)
                {
                    _voiceSource.clip = null;
                    ApplyVolumes();
                }

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
            if (!musicLibrary.IsTrackAllowedInContext(targetTrackId, _activeContext))
                return false;

            _historyIndex = targetIndex;
            PlayTrack(targetTrack, false);
            return true;
        }

        private void PlayTrack(TrackDef def, bool recordHistory)
        {
            if (def == null || def.clip == null || musicSource == null || _otherMusicSource == null)
            {
                return;
            }

            if (_musicFade != null)
            {
                StopCoroutine(_musicFade);
                _musicFade = null;
            }

            if (_otherMusicGain > _musicGain && _otherMusicSource.isPlaying)
            {
                (musicSource, _otherMusicSource) = (_otherMusicSource, musicSource);
                (_musicGain, _otherMusicGain) = (_otherMusicGain, _musicGain);
            }

            _otherMusicSource.Stop();
            _otherMusicSource.clip = null;
            _otherMusicGain = 0f;

            bool hasOutgoingTrack = musicSource.isPlaying;
            if (hasOutgoingTrack)
            {
                (musicSource, _otherMusicSource) = (_otherMusicSource, musicSource);
                _otherMusicGain = _musicGain;
                _musicGain = 0f;
            }
            else
            {
                _musicGain = 0f;
            }

            musicSource.clip = def.clip;
            musicSource.loop = false;
            musicSource.Play();
            ApplyVolumes();
            _musicFade = StartCoroutine(FadeMusic(hasOutgoingTrack ? musicCrossfadeSeconds : 0.35f));

            _currentTrackId = def.id;
            if (recordHistory)
                RegisterTrackInHistory(def.id);

            _missingLibraryWarned = false;
            OnTrackChanged?.Invoke(def.id, musicLibrary.GetDisplayName(def.id));
        }

        private IEnumerator FadeMusic(float duration)
        {
            float elapsed = 0f;
            float outgoingStartGain = _otherMusicGain;
            duration = Mathf.Max(0.01f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                _musicGain = progress;
                _otherMusicGain = outgoingStartGain * (1f - progress);
                ApplyVolumes();
                yield return null;
            }

            _musicGain = 1f;
            _otherMusicGain = 0f;
            _otherMusicSource.Stop();
            _otherMusicSource.clip = null;
            _musicFade = null;
            ApplyVolumes();
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

        private long GetCurrentTrackElapsedMs()
        {
            if (musicSource == null || musicSource.clip == null || string.IsNullOrWhiteSpace(_currentTrackId))
                return 0L;

            return Math.Max(0L, (long)(musicSource.time * 1000f));
        }

        private void WarnMissingLibraryOnce(string message)
        {
            if (_missingLibraryWarned)
                return;

            _missingLibraryWarned = true;
            Debug.LogWarning(message, this);
        }

        private void WarnMissingSfxSourceOnce()
        {
            if (_missingSfxSourceWarned)
                return;

            _missingSfxSourceWarned = true;
            Debug.LogWarning("[AudioManager] sfxSource is null. UI click SFX cannot be played.", this);
        }

        private void WarnMissingUiClickClipOnce()
        {
            if (_missingUiClickClipWarned)
                return;

            _missingUiClickClipWarned = true;
            Debug.LogWarning("[AudioManager] defaultUiClickClip is null and no override clip was provided.", this);
        }

        private void ApplyVolumes()
        {
            if (_prefs == null)
                return;

            if (musicSource != null)
            {
                musicSource.mute = _prefs.isMuted;
                musicSource.volume = Mathf.Clamp01(_prefs.musicVolume) * _musicGain * VoiceMusicGain();
            }

            if (_otherMusicSource != null)
            {
                _otherMusicSource.mute = _prefs.isMuted;
                _otherMusicSource.volume = Mathf.Clamp01(_prefs.musicVolume) * _otherMusicGain * VoiceMusicGain();
            }

            if (sfxSource != null)
            {
                sfxSource.mute = _prefs.isMuted;
                sfxSource.volume = Mathf.Clamp01(_prefs.sfxVolume);
            }

            if (_voiceSource != null)
            {
                _voiceSource.mute = _prefs.isMuted;
                _voiceSource.volume = Mathf.Clamp01(_prefs.sfxVolume);
            }
        }

        private float VoiceMusicGain() => _voiceSource != null && _voiceSource.isPlaying ? 0.35f : 1f;
    }
}
