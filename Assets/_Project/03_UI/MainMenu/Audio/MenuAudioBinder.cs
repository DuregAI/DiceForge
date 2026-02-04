using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI.MainMenu
{
    public class MenuAudioBinder : MonoBehaviour
    {
        private const string MusicKey = "audio.musicVolume";
        private const string SfxKey = "audio.sfxVolume";

        [Header("Scene refs")]
        [SerializeField] private AudioSource musicSource;

        [Header("UI Toolkit")]
        [SerializeField] private UIDocument uiDocument;

        [Tooltip("Optional. If empty, script will try to find sliders by names: sliderMusicVolume / sliderSfxVolume")]
        [SerializeField] private string musicSliderName = "sliderMusicVolume";
        [SerializeField] private string sfxSliderName = "sliderSfxVolume";

        private Slider _musicSlider;
        private Slider _sfxSlider;

        [Header("UI Click SFX")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private string clickableClass = "df-interactive";


        private void Reset()
        {
            // Handy auto-fill when you add the component
            uiDocument = FindFirstObjectByType<UIDocument>();
        }

        private void Awake()
        {
            if (musicSource == null)
            {
                // Try to find on the same GameObject
                musicSource = GetComponent<AudioSource>();
            }

            ApplyMusicVolumeFromPrefs();

            // Bind UI (safe even if settings panel is hidden)
            BindUiIfPossible();
        }

        private void OnEnable()
        {
            // If UI rebuilds after enable, try again
            BindUiIfPossible();
        }

        private void OnDisable()
        {
            UnbindUi();
        }

        private void ApplyMusicVolumeFromPrefs()
        {
            float v = PlayerPrefs.GetFloat(MusicKey, 1f);
            v = Mathf.Clamp01(v);

            if (musicSource != null)
                musicSource.volume = v;
        }

        private void BindUiIfPossible()
        {
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;
            if (root == null) return;

            // Try find sliders by UXML "name"
            _musicSlider ??= root.Q<Slider>(musicSliderName);
            _sfxSlider ??= root.Q<Slider>(sfxSliderName);

            // Set current values into UI (so sliders reflect prefs)
            if (_musicSlider != null)
            {
                _musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MusicKey, 1f));
                _musicSlider.RegisterValueChangedCallback(OnMusicSliderChanged);
            }

            if (_sfxSlider != null)
            {
                _sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(SfxKey, 1f));
                _sfxSlider.RegisterValueChangedCallback(OnSfxSliderChanged);
            }

            // Auto-bind click sound to all interactive buttons
            var buttons = root.Query<Button>(className: clickableClass).ToList();
            foreach (var btn in buttons)
            {
                btn.clicked -= PlayClick; // защита от двойной подписки
                btn.clicked += PlayClick;
            }


        }

        private void UnbindUi()
        {
            if (_musicSlider != null)
                _musicSlider.UnregisterValueChangedCallback(OnMusicSliderChanged);

            if (_sfxSlider != null)
                _sfxSlider.UnregisterValueChangedCallback(OnSfxSliderChanged);
        }

        private void OnMusicSliderChanged(ChangeEvent<float> evt)
        {
            float v = Mathf.Clamp01(evt.newValue);
            PlayerPrefs.SetFloat(MusicKey, v);
            PlayerPrefs.Save();

            if (musicSource != null)
                musicSource.volume = v;
        }

        private void OnSfxSliderChanged(ChangeEvent<float> evt)
        {
            float v = Mathf.Clamp01(evt.newValue);
            PlayerPrefs.SetFloat(SfxKey, v);
            PlayerPrefs.Save();

            if (sfxSource != null)
                sfxSource.volume = v;
        }

        /// <summary>
        /// ћожно дергать вручную, если в будущем UI пересоздаетс€ или ты открываешь настройки и хочешь синкнуть.
        /// </summary>
        public void RefreshVolumesFromPrefs()
        {
            ApplyMusicVolumeFromPrefs();

            if (_musicSlider != null)
                _musicSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(MusicKey, 1f));

            if (_sfxSlider != null)
                _sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(SfxKey, 1f));
        }

        public void PlayClick()
        {
            if (sfxSource == null || uiClickClip == null)
                return;

            float volume = PlayerPrefs.GetFloat("audio.sfxVolume", 1f);
            sfxSource.PlayOneShot(uiClickClip, Mathf.Clamp01(volume));
        }


    }
}
