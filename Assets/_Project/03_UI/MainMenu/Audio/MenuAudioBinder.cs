using Diceforge.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace Diceforge.UI.MainMenu
{
    public class MenuAudioBinder : MonoBehaviour
    {
        [Header("UI Toolkit")]
        [SerializeField] private UIDocument uiDocument;

        [Header("UI Click SFX")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private string clickableClass = "df-interactive";

        private void Reset()
        {
            uiDocument = FindFirstObjectByType<UIDocument>();
        }

        private void Awake()
        {
            BindUiIfPossible();
        }

        private void OnEnable()
        {
            BindUiIfPossible();
        }

        private void BindUiIfPossible()
        {
            if (uiDocument == null)
                return;

            var root = uiDocument.rootVisualElement;
            if (root == null)
                return;

            var buttons = root.Query<Button>(className: clickableClass).ToList();
            foreach (var btn in buttons)
            {
                btn.clicked -= PlayClick;
                btn.clicked += PlayClick;
            }
        }

        public void PlayClick()
        {
            if (sfxSource == null || uiClickClip == null)
                return;

            float volume = AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1f;
            sfxSource.PlayOneShot(uiClickClip, Mathf.Clamp01(volume));
        }
    }
}
