using System.Collections.Generic;
using Diceforge.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Diceforge.UI.MainMenu
{
    // Lives with the persistent AudioManager; event delegation also covers controls
    // created after a document has been attached (popups, map nodes and tutorial).
    public class MenuAudioBinder : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private AudioClip defaultClickClip;
        [SerializeField] private string clickableClass = "df-interactive";
        private readonly HashSet<VisualElement> roots = new();
        private readonly HashSet<VisualElement> activeRoots = new();
        private readonly List<VisualElement> staleRoots = new();
        private float nextScan;
        private int lastSoundFrame = -1;
        private VisualElement pressedControl;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            ScanDocuments();
        }
        private void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + .25f;
            ScanDocuments();
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ScanDocuments();
        private void ScanDocuments()
        {
            activeRoots.Clear();
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (!document.isActiveAndEnabled || document.rootVisualElement == null) continue;
                var root = document.rootVisualElement;
                activeRoots.Add(root);
                if (!roots.Add(root)) continue;
                root.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
                root.RegisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
                root.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
                root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            }
            staleRoots.Clear();
            foreach (var root in roots)
                if (!activeRoots.Contains(root)) staleRoots.Add(root);
            foreach (var root in staleRoots) { Unbind(root); roots.Remove(root); }
        }
        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            foreach (var root in roots) Unbind(root);
            roots.Clear();
            pressedControl = null;
        }
        private void Unbind(VisualElement root)
        {
            root.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            root.UnregisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
            root.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            root.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }
        private VisualElement FindControl(IEventHandler target)
        {
            for (var element = target as VisualElement; element != null; element = element.parent)
            {
                if (element.ClassListContains("ui-silent")) return null;
                if (element is Button || element is Toggle || element is DropdownField ||
                    element is Slider || element is SliderInt || element.ClassListContains(clickableClass))
                    return element.enabledInHierarchy && element.resolvedStyle.display != DisplayStyle.None &&
                        element.resolvedStyle.visibility == Visibility.Visible ? element : null;
            }
            return null;
        }
        private void OnClick(ClickEvent evt)
        {
            if (evt.button != 0) return;
            var control = FindControl(evt.target);
            if (control == null || control is Slider || control is SliderInt) return;
            PlayClick();
        }
        private void OnSubmit(NavigationSubmitEvent evt)
        {
            if (FindControl(evt.target) != null) PlayClick();
        }
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            var control = FindControl(evt.target);
            pressedControl = control;
            if (control is Slider || control is SliderInt) PlayClick();
        }
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0) return;
            var control = pressedControl;
            pressedControl = null;
            // Play before the button callback can hide its popup or unload the scene.
            if (control == null || control is Slider || control is SliderInt ||
                !control.enabledInHierarchy || !control.worldBound.Contains(evt.position)) return;
            if (FindControl(evt.target) == control) PlayClick();
        }
        private void OnKeyDown(KeyDownEvent evt)
        {
            var control = FindControl(evt.target);
            if ((control is Slider || control is SliderInt) &&
                (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow ||
                 evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow)) PlayClick();
        }
        public void PlayClick()
        {
            // Nested UIDocuments and submit-generated clicks must not double the sound.
            if (lastSoundFrame == Time.frameCount) return;
            lastSoundFrame = Time.frameCount;
            var manager = AudioManager.Instance;
            manager?.PlayUiClick(defaultClickClip);
        }
    }
}
