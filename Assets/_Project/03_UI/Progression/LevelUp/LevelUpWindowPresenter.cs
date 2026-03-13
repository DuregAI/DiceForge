using System;
using System.Collections;
using System.Collections.Generic;
using Diceforge.Audio;
using Diceforge.Diagnostics;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class LevelUpWindowPresenter : MonoBehaviour
{
    private sealed class PendingPresentation
    {
        public PendingPresentation(LevelUpPresentationData data, Action onClosed)
        {
            Data = data;
            OnClosed = onClosed;
        }

        public LevelUpPresentationData Data { get; }
        public Action OnClosed { get; }
    }

    private const string ViewResourcePath = "UI/LevelUp/LevelUpWindowView";
    private const float OverlayDelaySeconds = 0.08f;
    private const float PanelDelaySeconds = 0.12f;
    private const float PrimaryDelaySeconds = 0.2f;
    private const float UnlockDelaySeconds = 0.18f;

    [SerializeField] private AudioClip levelUpRevealClip;
    [SerializeField] private AudioClip continueClip;

    private readonly Queue<PendingPresentation> _queue = new();
    private VisualElement _hostRoot;
    private LevelUpWindowView _view;
    private RewardPopupEffectsBridge _backEffectsBridge;
    private RewardPopupEffectsBridge _frontEffectsBridge;
    private Coroutine _queueRoutine;
    private bool _continueRequested;
    private bool _initialized;
    private bool _analyticsSentForCurrentPopup;

    public bool IsOpen => _view != null && _view.Root.style.display != DisplayStyle.None;

    public void Initialize(VisualElement hostRoot)
    {
        if (_initialized || hostRoot == null)
            return;

        VisualTreeAsset treeAsset = Resources.Load<VisualTreeAsset>(ViewResourcePath);
        if (treeAsset == null)
            throw new InvalidOperationException($"[LevelUpWindowPresenter] Missing VisualTreeAsset at Resources path '{ViewResourcePath}'.");

        _hostRoot = hostRoot;
        TemplateContainer instance = treeAsset.CloneTree();
        instance.style.position = Position.Absolute;
        instance.style.left = 0f;
        instance.style.right = 0f;
        instance.style.top = 0f;
        instance.style.bottom = 0f;
        instance.pickingMode = PickingMode.Ignore;
        _hostRoot.Add(instance);

        _view = new LevelUpWindowView(instance);
        _view.ContinueRequested += HandleContinueRequested;
        RewardPopupEffectsBridge[] effectBridges = GetComponents<RewardPopupEffectsBridge>();
        if (effectBridges.Length > 0)
            _backEffectsBridge = effectBridges[0];
        else
            _backEffectsBridge = gameObject.AddComponent<RewardPopupEffectsBridge>();

        if (effectBridges.Length > 1)
            _frontEffectsBridge = effectBridges[1];
        else
            _frontEffectsBridge = gameObject.AddComponent<RewardPopupEffectsBridge>();

        _view.Hide();
        _initialized = true;
    }

    public void Show(LevelUpPresentationData data, Action onClosed = null)
    {
        if (!_initialized || data == null)
            return;

        _queue.Enqueue(new PendingPresentation(data, onClosed));
        if (_queueRoutine == null)
            _queueRoutine = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        while (_queue.Count > 0)
        {
            yield return Present(_queue.Dequeue());
        }

        _queueRoutine = null;
    }

    private IEnumerator Present(PendingPresentation request)
    {
        _continueRequested = false;
        _analyticsSentForCurrentPopup = false;

        _view.Bind(request.Data);
        _view.PrepareForShow();
        SendAnalyticsIfNeeded(request.Data);
        PlayRevealAudio();

        yield return null;

        _view.ShowOverlay();
        yield return new WaitForSecondsRealtime(OverlayDelaySeconds);

        _view.ShowPanel();
        yield return new WaitForSecondsRealtime(PanelDelaySeconds);

        _view.ShowPrimaryContent();
        _view.StartLevelPulse();
        _backEffectsBridge?.BeginPresentation(request.Data.EffectPresetId, _view.FxBackLayerImage, _view.LevelAnchorElement);
        _frontEffectsBridge?.BeginPresentation(request.Data.EffectPresetId, _view.FxFrontLayerImage, _view.LevelAnchorElement);
        yield return new WaitForSecondsRealtime(PrimaryDelaySeconds);

        _view.ShowUnlocks();
        yield return new WaitForSecondsRealtime(UnlockDelaySeconds);

        _view.SetInteractionReady(true);

        while (!_continueRequested)
            yield return null;

        PlayContinueAudio();
        _backEffectsBridge?.StopActivePresentation();
        _frontEffectsBridge?.StopActivePresentation();
        _view.Hide();
        request.OnClosed?.Invoke();
    }

    private void HandleContinueRequested()
    {
        if (_view != null && _view.IsInteractionReady)
            _continueRequested = true;
    }

    private void PlayRevealAudio()
    {
        AudioManager audioManager = AudioManager.Instance != null ? AudioManager.Instance : FindAnyObjectByType<AudioManager>();
        if (audioManager == null || levelUpRevealClip == null)
            return;

        audioManager.PlayUiClick(levelUpRevealClip);
    }

    private void PlayContinueAudio()
    {
        AudioManager audioManager = AudioManager.Instance != null ? AudioManager.Instance : FindAnyObjectByType<AudioManager>();
        if (audioManager == null)
            return;

        audioManager.PlayUiClick(continueClip);
    }

    private void SendAnalyticsIfNeeded(LevelUpPresentationData data)
    {
        if (_analyticsSentForCurrentPopup || data == null)
            return;

        var unlockIds = new List<string>(data.Unlocks != null ? data.Unlocks.Count : 0);
        if (data.Unlocks != null)
        {
            for (int i = 0; i < data.Unlocks.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(data.Unlocks[i].Id))
                    unlockIds.Add(data.Unlocks[i].Id);
            }
        }

        ClientDiagnostics.RecordPlayerLevelUp(new PlayerLevelUpDiagnosticsContext(
            data.PreviousLevel,
            data.NewLevel,
            unlockIds,
            data.SourceContext));

        _analyticsSentForCurrentPopup = true;
    }

    private void OnDestroy()
    {
        if (_queueRoutine != null)
            StopCoroutine(_queueRoutine);

        _backEffectsBridge?.StopActivePresentation();
        _frontEffectsBridge?.StopActivePresentation();

        if (_view != null)
        {
            _view.ContinueRequested -= HandleContinueRequested;
            _view.Dispose();
        }
    }
}
