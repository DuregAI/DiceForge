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
        [SerializeField] private AudioClip defaultClickClip;
        [SerializeField] private string clickableClass = "df-interactive";

        private readonly HashSet<Button> boundButtons = new();
        private VisualElement currentRoot;
        private bool isRootCallbackRegistered;
        private bool warnedMissingSfxSource;
        private bool warnedMissingDefaultClickClip;

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

            var queriedButtons = root.Query<Button>(className: clickableClass).ToList();
            if (queriedButtons.Count == 0)
            {
                UnbindAllButtons();
                return false;
            }

            var activeButtons = new HashSet<Button>(queriedButtons);
            var staleButtons = new List<Button>();
            foreach (var boundButton in boundButtons)
            {
                if (boundButton == null || !activeButtons.Contains(boundButton))
                    staleButtons.Add(boundButton);
            }

            foreach (var staleButton in staleButtons)
            {
                if (staleButton != null)
                    staleButton.clicked -= PlayClick;

                boundButtons.Remove(staleButton);
            }

            foreach (var button in queriedButtons)
            {
                if (button == null || boundButtons.Contains(button))
                    continue;

                button.clicked += PlayClick;
                boundButtons.Add(button);
            }

            return true;
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

            foreach (var button in boundButtons)
            {
                if (button == null)
                    continue;

                button.clicked -= PlayClick;
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

            if (defaultClickClip == null)
            {
                if (!warnedMissingDefaultClickClip)
                {
                    Debug.LogWarning("[MenuAudioBinder] defaultClickClip is null. UI click SFX cannot be played.", this);
                    warnedMissingDefaultClickClip = true;
                }

                return;
            }

            float volume = AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1f;
            sfxSource.PlayOneShot(defaultClickClip, Mathf.Clamp01(volume));
        }
    }
}
