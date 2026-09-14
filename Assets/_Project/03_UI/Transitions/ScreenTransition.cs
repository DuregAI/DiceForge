using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Diceforge.Transitions
{
    public enum TransitionPhase { Idle, Closing, Preparing, Opening, Failed }

    /// <summary>Single owner of full-screen navigation; survives scene replacement.</summary>
    public sealed class ScreenTransition : MonoBehaviour
    {
        private static ScreenTransition instance;
        public static bool IsBusy => instance != null && instance.Phase != TransitionPhase.Idle;
        public static bool IsCovered => instance != null && (instance.Phase == TransitionPhase.Preparing || instance.Phase == TransitionPhase.Failed);
        public TransitionPhase Phase { get; private set; }
        public string LastError { get; private set; }
        public CloudTransitionSettings Settings { get; private set; }
        private UIDocument document;
        private PanelSettings panel;
        private VisualElement curtainRoot;
        private CloudCurtain curtain;
        private Label status;
        private Button dismiss;
        private readonly TransitionInputBlock input = new TransitionInputBlock();
        private bool ownsSettings;
        private bool dismissError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => EnsureInstance();

        private static ScreenTransition EnsureInstance()
        {
            if (instance != null) return instance;
            var go = new GameObject("ScreenTransitions");
            return go.AddComponent<ScreenTransition>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Settings = Resources.Load<CloudTransitionSettings>("Transitions/CloudTransitionSettings");
            if (Settings == null) { Settings = ScriptableObject.CreateInstance<CloudTransitionSettings>(); ownsSettings = true; }
            var template = Resources.Load<PanelSettings>("Transitions/TransitionPanelSettings");
            panel = template != null ? Instantiate(template) : ScriptableObject.CreateInstance<PanelSettings>();
            panel.sortingOrder = 32000;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.sortingOrder = 32000;
            curtainRoot = document.rootVisualElement;
            CloudCurtain.Fill(curtainRoot);
            curtain = new CloudCurtain(curtainRoot, Settings);
            status = new Label("Loading…") { pickingMode = PickingMode.Ignore };
            status.style.position = Position.Absolute;
            status.style.bottom = 70;
            status.style.left = status.style.right = 0;
            status.style.unityTextAlign = TextAnchor.MiddleCenter;
            status.style.fontSize = 24;
            status.style.color = new Color(0.22f, 0.25f, 0.17f);
            curtainRoot.Add(status);
            dismiss = new Button(() => dismissError = true) { text = "Continue" };
            dismiss.style.position = Position.Absolute;
            dismiss.style.bottom = 20;
            dismiss.style.alignSelf = Align.Center;
            dismiss.style.width = 180;
            dismiss.style.height = 44;
            curtainRoot.Add(dismiss);
            curtainRoot.style.display = DisplayStyle.None;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Returns false on duplicate navigation; callers must not mutate payloads first.
        public static bool Switch(Action changeScreen, Func<bool> ready = null)
        {
            if (changeScreen == null) throw new ArgumentNullException(nameof(changeScreen));
            return EnsureInstance().Begin(changeScreen, null, ready);
        }

        public static bool LoadScene(string sceneName, Action prepare = null, Func<bool> ready = null)
        {
            if (IsBusy) return false;
            if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
                throw new ArgumentException("Scene is not available in the build: " + sceneName, nameof(sceneName));
            return EnsureInstance().Begin(prepare, sceneName, ready);
        }

        private bool Begin(Action change, string sceneName, Func<bool> ready)
        {
            if (!isActiveAndEnabled || Phase != TransitionPhase.Idle) return false;
            Phase = TransitionPhase.Closing;
            LastError = null;
            dismissError = false;
            input.Capture(document);
            curtainRoot.style.display = DisplayStyle.Flex;
            status.style.display = dismiss.style.display = DisplayStyle.None;
            curtain.SetCoverage(0, false);
            StartCoroutine(Run(change, sceneName, ready));
            return true;
        }

        private IEnumerator Run(Action change, string sceneName, Func<bool> ready)
        {
            try
            {
                yield return Animate(false);
                Phase = TransitionPhase.Preparing;
                yield return null; // Fully opaque frame before any content mutation.
                AsyncOperation loading = null;
                try
                {
                    change?.Invoke();
                    if (sceneName != null) loading = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                }
                catch (Exception exception) { Fail(exception); }
                float started = Time.realtimeSinceStartup;
                while (loading != null && !loading.isDone)
                {
                    status.style.display = Time.realtimeSinceStartup - started > 2 ? DisplayStyle.Flex : DisplayStyle.None;
                    yield return null;
                }
                // sceneLoaded precedes Start; allow Start and UI layout to finish under cover.
                yield return null;
                yield return null;
                input.Capture(document);
                started = Time.realtimeSinceStartup;
                while (LastError == null && ready != null)
                {
                    bool isReady = false;
                    try { isReady = ready(); }
                    catch (Exception exception) { Fail(exception); }
                    if (isReady || LastError != null) break;
                    if (Time.realtimeSinceStartup - started > Mathf.Max(1, Settings.readinessTimeout))
                    { Fail(new TimeoutException("Destination did not become ready.")); break; }
                    status.style.display = DisplayStyle.Flex;
                    yield return null;
                }
                if (LastError != null)
                {
                    Phase = TransitionPhase.Failed;
                    status.text = "Could not open the screen. Please try again.";
                    status.style.display = dismiss.style.display = DisplayStyle.Flex;
                    dismiss.Focus();
                    while (!dismissError) yield return null;
                }
                else if (Settings.coveredHold > 0)
                    yield return new WaitForSecondsRealtime(Settings.coveredHold);
                status.style.display = dismiss.style.display = DisplayStyle.None;
                Phase = TransitionPhase.Opening;
                yield return Animate(true);
            }
            finally { Release(); }
        }

        private IEnumerator Animate(bool opening)
        {
            float duration = Mathf.Max(0, opening ? Settings.openDuration : Settings.closeDuration);
            if (Settings.reducedMotion) duration = Mathf.Min(duration, 0.2f);
            float elapsed = 0;
            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                curtain.SetCoverage(opening ? 1 - progress : progress, opening);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            curtain.SetCoverage(opening ? 0 : 1, opening);
        }

        private void Fail(Exception exception)
        {
            LastError = exception.Message;
            Debug.LogError("[ScreenTransition] " + exception, this);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsBusy) input.Capture(document);
        }

        private void Release()
        {
            input.Release();
            if (curtainRoot != null) curtainRoot.style.display = DisplayStyle.None;
            Phase = TransitionPhase.Idle;
        }

        private void OnDisable() { StopAllCoroutines(); Release(); }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Release();
            if (instance == this) instance = null;
            if (panel != null) Destroy(panel);
            if (ownsSettings && Settings != null) Destroy(Settings);
        }
    }
}
