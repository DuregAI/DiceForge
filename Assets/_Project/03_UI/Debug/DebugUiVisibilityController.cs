using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Diceforge.View
{
    internal static class DebugUiVisibilityControllerBootstrap
    {
        private static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_subscribed)
                SceneManager.sceneLoaded -= HandleSceneLoaded;

            _subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_subscribed)
                return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            _subscribed = true;
            AttachControllers();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachAfterSceneLoad()
        {
            AttachControllers();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            AttachControllers();
        }

        private static void AttachControllers()
        {
            var huds = Object.FindObjectsByType<DebugHudUITK>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < huds.Length; i++)
            {
                DebugHudUITK hud = huds[i];
                if (hud == null || hud.gameObject.GetComponent<DebugUiVisibilityController>() != null)
                    continue;

                hud.gameObject.AddComponent<DebugUiVisibilityController>();
            }
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(DebugHudUITK))]
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugUiVisibilityController : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private bool showInEditorByDefault = true;

        private VisualElement _debugUiRoot;
        private bool _isDebugUiVisible;
        private bool _canShowDebugUi;

        public bool IsDebugUiVisible => _canShowDebugUi && _isDebugUiVisible;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            _canShowDebugUi = Application.isEditor || Debug.isDebugBuild;
            _isDebugUiVisible = Application.isEditor && showInEditorByDefault;
            ApplyVisibility();
        }

        private void Update()
        {
            if (!_canShowDebugUi)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            bool isCtrlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (!isCtrlPressed || !keyboard.backquoteKey.wasPressedThisFrame)
                return;

            _isDebugUiVisible = !_isDebugUiVisible;
            ApplyVisibility();
        }

        private void ResolveReferences()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            VisualElement root = document != null ? document.rootVisualElement : null;
            _debugUiRoot = root != null ? root.Q<VisualElement>("debugUiRoot") : null;
        }

        private void ApplyVisibility()
        {
            if (_debugUiRoot == null)
                return;

            bool shouldShow = _canShowDebugUi && _isDebugUiVisible;
            _debugUiRoot.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
