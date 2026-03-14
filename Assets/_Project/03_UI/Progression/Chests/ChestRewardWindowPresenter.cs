using System;
using System.Collections;
using System.Collections.Generic;
using Diceforge.Audio;
using Diceforge.Diagnostics;
using Diceforge.Progression;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ChestRewardWindowPresenter : MonoBehaviour
{
    private sealed class PendingPresentation
    {
        public PendingPresentation(ChestRewardPresentationData data, Action onClosed)
        {
            Data = data;
            OnClosed = onClosed;
        }

        public ChestRewardPresentationData Data { get; }
        public Action OnClosed { get; }
    }

    private const string ViewResourcePath = "UI/Chests/ChestRewardWindowView";
    private const float OverlayDelaySeconds = 0.08f;
    private const float PanelDelaySeconds = 0.12f;
    private const float PrimaryDelaySeconds = 0.2f;
    private const float DetailsDelaySeconds = 0.16f;

    [SerializeField] private AudioClip chestRevealClip;
    [SerializeField] private AudioClip continueClip;

    private readonly Queue<PendingPresentation> _queue = new();
    private VisualElement _hostRoot;
    private ChestRewardWindowView _view;
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
            throw new InvalidOperationException($"[ChestRewardWindowPresenter] Missing VisualTreeAsset at Resources path '{ViewResourcePath}'.");

        _hostRoot = hostRoot;
        TemplateContainer instance = treeAsset.CloneTree();
        instance.style.position = Position.Absolute;
        instance.style.left = 0f;
        instance.style.right = 0f;
        instance.style.top = 0f;
        instance.style.bottom = 0f;
        instance.pickingMode = PickingMode.Ignore;
        _hostRoot.Add(instance);

        _view = new ChestRewardWindowView(instance);
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

    public void Show(ChestRewardPresentationData data, Action onClosed = null)
    {
        if (!_initialized || data == null || !data.HasEntries)
            return;

        _queue.Enqueue(new PendingPresentation(data, onClosed));
        if (_queueRoutine == null)
            _queueRoutine = StartCoroutine(RunQueue());
    }

    private IEnumerator RunQueue()
    {
        while (_queue.Count > 0)
            yield return Present(_queue.Dequeue());

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
        _view.StartChestPulse();
        _backEffectsBridge?.BeginPresentation(request.Data.EffectPresetId, _view.FxBackLayerImage, _view.ChestAnchorElement);
        _frontEffectsBridge?.BeginPresentation(request.Data.EffectPresetId, _view.FxFrontLayerImage, _view.ChestAnchorElement);
        yield return new WaitForSecondsRealtime(PrimaryDelaySeconds);

        _view.ShowDetails();
        yield return new WaitForSecondsRealtime(DetailsDelaySeconds);

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
        if (audioManager == null || chestRevealClip == null)
            return;

        audioManager.PlayUiClick(chestRevealClip);
    }

    private void PlayContinueAudio()
    {
        AudioManager audioManager = AudioManager.Instance != null ? AudioManager.Instance : FindAnyObjectByType<AudioManager>();
        if (audioManager == null)
            return;

        audioManager.PlayUiClick(continueClip);
    }

    private void SendAnalyticsIfNeeded(ChestRewardPresentationData data)
    {
        if (_analyticsSentForCurrentPopup || data == null || !data.HasEntries)
            return;

        var chestIds = new List<string>(data.Entries.Count);
        var chestTypes = new List<string>(data.Entries.Count);
        var chestTiers = new List<string>(data.Entries.Count);
        for (int i = 0; i < data.Entries.Count; i++)
        {
            ChestRewardPresentationEntry entry = data.Entries[i];
            chestIds.Add(entry.ChestId);
            chestTypes.Add(entry.DisplayName);
            if (entry.HasRarityLabel)
                chestTiers.Add(entry.RarityLabel);
        }

        ClientDiagnostics.RecordChestObtained(new ChestObtainedDiagnosticsContext(
            chestIds,
            chestTypes,
            chestTiers,
            data.TotalChestCount,
            data.SourceContext,
            data.PlayerLevel,
            data.AfterLevelUp));

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
