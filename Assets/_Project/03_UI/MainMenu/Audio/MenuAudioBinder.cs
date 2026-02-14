using System.Collections.Generic;
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

        private readonly HashSet<Button> boundButtons = new();
        private VisualElement currentRoot;
        private bool isRootCallbackRegistered;
        private bool warnedMissingSfxSource;
        private bool warnedMissingUiClickClip;

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
            RegisterRootGeometryCallback();
        }

        private void OnDisable()
        {
            UnregisterRootGeometryCallback();
            UnbindAllButtons();
        }

        private bool BindUiIfPossible()
        {
            if (uiDocument == null)
                return false;

            var root = uiDocument.rootVisualElement;
            if (root == null)
                return false;

            if (currentRoot != root)
            {
                UnregisterRootGeometryCallback();
                currentRoot = root;
                RegisterRootGeometryCallback();
            }

            var buttons = root.Query<Button>(className: clickableClass).ToList();
            foreach (var btn in buttons)
            {
                btn.clicked -= PlayClick;
                btn.clicked += PlayClick;
                boundButtons.Add(btn);
            }

            return buttons.Count > 0;
        }

        private void OnRootGeometryChanged(GeometryChangedEvent evt)
        {
            BindUiIfPossible();
        }

        private void RegisterRootGeometryCallback()
        {
            if (isRootCallbackRegistered || currentRoot == null)
                return;

            currentRoot.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            isRootCallbackRegistered = true;
        }

        private void UnregisterRootGeometryCallback()
        {
            if (!isRootCallbackRegistered || currentRoot == null)
                return;

            currentRoot.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
            isRootCallbackRegistered = false;
        }

        private void UnbindAllButtons()
        {
            if (boundButtons.Count == 0)
                return;

            foreach (var btn in boundButtons)
            {
                if (btn == null)
                    continue;

                btn.clicked -= PlayClick;
            }

            boundButtons.Clear();
        }

        public void PlayClick()
        {
            if (sfxSource == null)
            {
                if (!warnedMissingSfxSource)
                {
                    Debug.LogWarning("[MenuAudioBinder] sfxSource is null. UI click SFX cannot be played.", this);
                    warnedMissingSfxSource = true;
                }

                return;
            }

            if (uiClickClip == null)
            {
                if (!warnedMissingUiClickClip)
                {
                    Debug.LogWarning("[MenuAudioBinder] uiClickClip is null. UI click SFX cannot be played.", this);
                    warnedMissingUiClickClip = true;
                }

                return;
            }

            float volume = AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1f;
            sfxSource.PlayOneShot(uiClickClip, Mathf.Clamp01(volume));
        }
    }
}
