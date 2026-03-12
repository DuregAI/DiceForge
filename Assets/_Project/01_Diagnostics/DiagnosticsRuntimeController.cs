using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Diceforge.Diagnostics
{
    public static class ClientDiagnostics
    {
        public static string BuildSupportLog(string buildInfo)
        {
            return DiagnosticsRuntimeController.EnsureCreated().BuildSupportLog(buildInfo);
        }

        public static void Configure(DiagnosticsOptions options)
        {
            DiagnosticsRuntimeController.EnsureCreated().Configure(options);
        }

        public static string GetCurrentSessionId()
        {
            return DiagnosticsRuntimeController.EnsureCreated().GetCurrentSessionId();
        }

        public static void RegisterSessionSummarySink(IDiagnosticsSessionSummarySink sink)
        {
            DiagnosticsRuntimeController.EnsureCreated().RegisterSessionSummarySink(sink);
        }

        public static void UnregisterSessionSummarySink(IDiagnosticsSessionSummarySink sink)
        {
            DiagnosticsRuntimeController.EnsureCreated().UnregisterSessionSummarySink(sink);
        }

        public static void RecordBattleStarted(BattleStartDiagnosticsContext context)
        {
            DiagnosticsRuntimeController.EnsureCreated().RecordBattleStarted(context);
        }

        public static void RecordBattleEnded(BattleEndDiagnosticsContext context)
        {
            DiagnosticsRuntimeController.EnsureCreated().RecordBattleEnded(context);
        }

        public static void RecordBattleSurrender(BattleSurrenderDiagnosticsContext context)
        {
            DiagnosticsRuntimeController.EnsureCreated().RecordBattleSurrender(context);
        }
    }

    internal static class DiagnosticsRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DiagnosticsRuntimeController.ResetStaticState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            DiagnosticsRuntimeController.EnsureCreated();
        }
    }

    public sealed class DiagnosticsRuntimeController : MonoBehaviour
    {
        private const string RuntimeObjectName = "DiagnosticsRuntime";
        private const int MaxRecentEvents = 16;
        private const float OverlayWidth = 560f;
        private const float OverlayPadding = 12f;
        private static readonly Color OverlayBackgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.92f);

        private static DiagnosticsRuntimeController _instance;

        private readonly List<AnalyticsEventData> _flushBuffer = new List<AnalyticsEventData>(16);
        private readonly List<IDiagnosticsSessionSummarySink> _sessionSummarySinks = new List<IDiagnosticsSessionSummarySink>(1);
        private readonly Queue<string> _recentEventLines = new Queue<string>(MaxRecentEvents);
        private bool _initialized;
        private bool _sceneLoadedSubscribed;
        private bool _shutdownRecorded;
        private bool _overlayVisible;
        private DiagnosticsOptions _options;
        private AnalyticsEventQueue _eventQueue;
        private IDiagnosticsTransport _transport;
        private PerformanceSessionSampler _performanceSampler;
        private SessionDiagnosticsSnapshot _sessionSnapshot;
        private BattleStartDiagnosticsContext? _activeBattleContext;
        private int _sceneLoadedCount;
        private int _battleStartedCount;
        private int _battleEndedCount;
        private string _lastSceneName = string.Empty;
        private GUIStyle _overlayTitleStyle;
        private GUIStyle _overlayBodyStyle;
        private GUIStyle _overlaySectionStyle;

        public static DiagnosticsRuntimeController EnsureCreated()
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<DiagnosticsRuntimeController>();
            if (_instance != null)
            {
                _instance.InitializeIfNeeded();
                return _instance;
            }

            var runtimeObject = new GameObject(RuntimeObjectName);
            runtimeObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(runtimeObject);

            _instance = runtimeObject.AddComponent<DiagnosticsRuntimeController>();
            return _instance;
        }

        internal static void ResetStaticState()
        {
            _instance = null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(gameObject);
            InitializeIfNeeded();
        }

        private void OnEnable()
        {
            SubscribeSceneLoaded();
        }

        private void OnDisable()
        {
            UnsubscribeSceneLoaded();
        }

        private void Update()
        {
            if (!_initialized || _shutdownRecorded)
                return;

            if (Keyboard.current != null)
            {
                bool isCtrlPressed = Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
                if (!isCtrlPressed && Keyboard.current.backquoteKey.wasPressedThisFrame)
                    _overlayVisible = !_overlayVisible;
            }

            _performanceSampler.Sample(Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (!_initialized || !_overlayVisible)
                return;

            DrawOverlay();
        }

        private void OnApplicationQuit()
        {
            if (_instance == this)
                Shutdown();
        }

        private void OnDestroy()
        {
            if (_instance != this)
                return;

            UnsubscribeSceneLoaded();
            Shutdown();
            _instance = null;
        }

        public void Configure(DiagnosticsOptions options)
        {
            InitializeIfNeeded();

            if (options == null)
                return;

            _options.Apply(options);
            _eventQueue.Resize(_options.MaxQueuedEvents);
            _performanceSampler.Configure(_options.PerformanceSamplingWindowSeconds);
        }

        public void RegisterSessionSummarySink(IDiagnosticsSessionSummarySink sink)
        {
            InitializeIfNeeded();

            if (sink == null || _sessionSummarySinks.Contains(sink))
                return;

            _sessionSummarySinks.Add(sink);
        }

        public void UnregisterSessionSummarySink(IDiagnosticsSessionSummarySink sink)
        {
            if (sink == null)
                return;

            _sessionSummarySinks.Remove(sink);
        }

        public string GetCurrentSessionId()
        {
            InitializeIfNeeded();
            return _sessionSnapshot != null ? _sessionSnapshot.SessionId : string.Empty;
        }

        public void RecordBattleStarted(BattleStartDiagnosticsContext context)
        {
            InitializeIfNeeded();
            if (_shutdownRecorded)
                return;

            _activeBattleContext = context;
            _battleStartedCount++;
            EnqueueAndFlush(DiagnosticsEventType.BattleStarted, context.ToPayload());
        }

        public void RecordBattleEnded(BattleEndDiagnosticsContext context)
        {
            InitializeIfNeeded();
            if (_shutdownRecorded)
                return;

            _battleEndedCount++;

            Dictionary<string, string> payload = _activeBattleContext.HasValue
                ? _activeBattleContext.Value.ToPayload()
                : new Dictionary<string, string>(6, StringComparer.Ordinal);

            foreach (var pair in context.ToPayload())
                payload[pair.Key] = pair.Value;

            EnqueueAndFlush(DiagnosticsEventType.BattleEnded, payload);
            _activeBattleContext = null;
        }

        public void RecordBattleSurrender(BattleSurrenderDiagnosticsContext context)
        {
            InitializeIfNeeded();
            if (_shutdownRecorded)
                return;

            EnqueueAndFlush(DiagnosticsEventType.BattleSurrender, context.ToPayload(), null, BattleSurrenderDiagnosticsContext.EventName);
        }

        public string BuildSupportLog(string buildInfo)
        {
            InitializeIfNeeded();

            var sb = new StringBuilder(1024);
            sb.AppendLine(buildInfo ?? "Build: unknown");
            sb.Append("GeneratedAtUtc: ").AppendLine(DateTime.UtcNow.ToString("O"));
            sb.Append("Platform: ").AppendLine(Application.platform.ToString());
            sb.Append("OverlayVisible: ").AppendLine(_overlayVisible ? "true" : "false");
            sb.AppendLine();

            AppendSection(sb, "Session", _sessionSnapshot.ToPayload());
            AppendSection(sb, "Performance", _performanceSampler.CreateSnapshot().ToPayload());

            sb.AppendLine("Counters");
            sb.Append("sceneLoads=").AppendLine(_sceneLoadedCount.ToString());
            sb.Append("battlesStarted=").AppendLine(_battleStartedCount.ToString());
            sb.Append("battlesEnded=").AppendLine(_battleEndedCount.ToString());
            sb.Append("lastScene=").AppendLine(string.IsNullOrWhiteSpace(_lastSceneName) ? "<none>" : _lastSceneName);
            sb.AppendLine();

            sb.AppendLine("RecentEvents");
            if (_recentEventLines.Count == 0)
            {
                sb.AppendLine("<none>");
            }
            else
            {
                foreach (string line in _recentEventLines)
                    sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd();
        }

        private void InitializeIfNeeded()
        {
            if (_initialized)
                return;

            _options = new DiagnosticsOptions();
            _eventQueue = new AnalyticsEventQueue(_options.MaxQueuedEvents);
            _transport = new DebugLogDiagnosticsTransport();
            _performanceSampler = new PerformanceSessionSampler(_options.PerformanceSamplingWindowSeconds);

            long sessionStartUnixMsUtc = GetUtcNowUnixMs();
            string sessionId = Guid.NewGuid().ToString("N");
            _sessionSnapshot = SessionDiagnosticsSnapshot.Capture(sessionId, sessionStartUnixMsUtc);

            _initialized = true;
            EnqueueAndFlush(DiagnosticsEventType.SessionStarted, _sessionSnapshot.ToPayload(), _sessionSnapshot.SessionStartUnixMsUtc);
        }

        private void SubscribeSceneLoaded()
        {
            if (_sceneLoadedSubscribed)
                return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            _sceneLoadedSubscribed = true;
        }

        private void UnsubscribeSceneLoaded()
        {
            if (!_sceneLoadedSubscribed)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            _sceneLoadedSubscribed = false;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            InitializeIfNeeded();
            if (_shutdownRecorded)
                return;

            _sceneLoadedCount++;
            _lastSceneName = scene.name ?? string.Empty;

            var payload = new Dictionary<string, string>(3, StringComparer.Ordinal);
            DiagnosticsPayloadUtility.Add(payload, "sceneName", _lastSceneName);
            DiagnosticsPayloadUtility.Add(payload, "buildIndex", scene.buildIndex);
            DiagnosticsPayloadUtility.Add(payload, "loadMode", loadSceneMode.ToString());

            EnqueueAndFlush(DiagnosticsEventType.SceneLoaded, payload);
        }

        private void Shutdown()
        {
            if (!_initialized || _shutdownRecorded)
                return;

            _shutdownRecorded = true;

            long sessionEndUnixMsUtc = GetUtcNowUnixMs();
            var summary = new SessionSummaryData
            {
                SessionId = _sessionSnapshot.SessionId,
                SessionStartUnixMsUtc = _sessionSnapshot.SessionStartUnixMsUtc,
                SessionEndUnixMsUtc = sessionEndUnixMsUtc,
                DurationSeconds = Math.Max(0d, (sessionEndUnixMsUtc - _sessionSnapshot.SessionStartUnixMsUtc) / 1000d),
                BuildVersion = _sessionSnapshot.AppVersion,
                Platform = _sessionSnapshot.Platform,
                OperatingSystem = _sessionSnapshot.OperatingSystem,
                CpuName = _sessionSnapshot.CpuName,
                GpuName = _sessionSnapshot.GpuName,
                ScreenWidth = _sessionSnapshot.ScreenWidth,
                ScreenHeight = _sessionSnapshot.ScreenHeight,
                LastSceneName = _lastSceneName,
                ScenesLoadedCount = _sceneLoadedCount,
                BattlesStartedCount = _battleStartedCount,
                BattlesEndedCount = _battleEndedCount,
                Performance = _performanceSampler.CreateSnapshot()
            };

            EnqueueAndFlush(DiagnosticsEventType.SessionEnded, summary.ToPayload(), sessionEndUnixMsUtc);
            SubmitSessionSummary(summary);
        }

        private void EnqueueAndFlush(DiagnosticsEventType eventType, Dictionary<string, string> payload, long? timestampUnixMsUtc = null, string displayName = null)
        {
            var eventData = new AnalyticsEventData(
                eventType,
                timestampUnixMsUtc ?? GetUtcNowUnixMs(),
                _sessionSnapshot != null ? _sessionSnapshot.SessionId : string.Empty,
                payload,
                displayName);

            AddRecentEventLine(eventData);
            _eventQueue.Enqueue(eventData);
            FlushQueue();
        }

        private void AddRecentEventLine(AnalyticsEventData eventData)
        {
            if (eventData == null)
                return;

            string line = $"{eventData.DisplayName} ts={eventData.TimestampUnixMsUtc} {BuildInlinePayload(eventData.Payload)}";
            while (_recentEventLines.Count >= MaxRecentEvents)
                _recentEventLines.Dequeue();

            _recentEventLines.Enqueue(line.TrimEnd());
        }

        private void FlushQueue()
        {
            _eventQueue.DrainTo(_flushBuffer);
            if (_flushBuffer.Count == 0)
                return;

            try
            {
                _transport.SendBatch(_flushBuffer);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Diagnostics] Transport send failed: {exception.Message}", this);
            }
            finally
            {
                _flushBuffer.Clear();
            }
        }

        private void SubmitSessionSummary(SessionSummaryData summary)
        {
            if (summary == null || _sessionSummarySinks.Count == 0)
                return;

            for (int i = 0; i < _sessionSummarySinks.Count; i++)
            {
                IDiagnosticsSessionSummarySink sink = _sessionSummarySinks[i];
                if (sink == null)
                    continue;

                try
                {
                    sink.SubmitSessionSummary(summary);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[Diagnostics] Session summary sink failed: {exception.Message}", this);
                }
            }
        }

        private void DrawOverlay()
        {
            EnsureOverlayStyles();

            PerformanceSessionAggregate performance = _performanceSampler.CreateSnapshot();
            float width = Mathf.Min(OverlayWidth, Screen.width - 24f);
            float height = 312f + (_recentEventLines.Count * 24f);
            var area = new Rect(12f, 12f, width, Mathf.Min(height, Screen.height - 24f));

            Color previousColor = GUI.color;
            GUI.color = OverlayBackgroundColor;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = previousColor;

            var contentArea = new Rect(
                area.x + OverlayPadding,
                area.y + OverlayPadding,
                area.width - (OverlayPadding * 2f),
                area.height - (OverlayPadding * 2f));

            GUILayout.BeginArea(contentArea);
            GUILayout.Label("Diagnostics", _overlayTitleStyle);
            GUILayout.Space(6f);
            GUILayout.Label($"Session: {_sessionSnapshot.SessionId}", _overlayBodyStyle);
            GUILayout.Label($"Scene: {(_lastSceneName == string.Empty ? "<none>" : _lastSceneName)}", _overlayBodyStyle);
            GUILayout.Label($"Window: {_options.PerformanceSamplingWindowSeconds:0.##}s", _overlayBodyStyle);
            GUILayout.Label($"Avg FPS: {performance.AverageFps:0.0}", _overlayBodyStyle);
            GUILayout.Label($"Min Window FPS: {performance.MinimumWindowFps:0.0}", _overlayBodyStyle);
            GUILayout.Label($"Worst Spike: {performance.WorstSpikeMs:0.0} ms", _overlayBodyStyle);
            GUILayout.Label($"Battles: {_battleStartedCount}/{_battleEndedCount}", _overlayBodyStyle);
            GUILayout.Space(10f);
            GUILayout.Label("Recent Events", _overlaySectionStyle);

            if (_recentEventLines.Count == 0)
            {
                GUILayout.Label("<none>", _overlayBodyStyle);
            }
            else
            {
                foreach (string line in _recentEventLines)
                    GUILayout.Label(line, _overlayBodyStyle);
            }

            GUILayout.EndArea();
        }

        private void EnsureOverlayStyles()
        {
            if (_overlayTitleStyle != null)
                return;
            _overlayTitleStyle = new GUIStyle(GUI.skin.label);
            _overlayTitleStyle.fontSize = 18;
            _overlayTitleStyle.fontStyle = FontStyle.Bold;
            _overlayTitleStyle.normal.textColor = new Color(0.96f, 0.97f, 0.99f, 1f);
            _overlayBodyStyle = new GUIStyle(GUI.skin.label);
            _overlayBodyStyle.fontSize = 15;
            _overlayBodyStyle.richText = false;
            _overlayBodyStyle.wordWrap = false;
            _overlayBodyStyle.normal.textColor = new Color(0.9f, 0.93f, 0.98f, 1f);
            _overlaySectionStyle = new GUIStyle(_overlayBodyStyle);
            _overlaySectionStyle.fontSize = 16;
            _overlaySectionStyle.fontStyle = FontStyle.Bold;
            _overlaySectionStyle.normal.textColor = new Color(0.98f, 0.99f, 1f, 1f);
        }
        private static void AppendSection(StringBuilder sb, string title, Dictionary<string, string> values)
        {
            sb.AppendLine(title);
            if (values == null || values.Count == 0)
            {
                sb.AppendLine("<none>");
                sb.AppendLine();
                return;
            }

            foreach (var pair in values)
                sb.Append(pair.Key).Append('=').AppendLine(pair.Value);

            sb.AppendLine();
        }

        private static string BuildInlinePayload(Dictionary<string, string> payload)
        {
            if (payload == null || payload.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            bool first = true;
            foreach (var pair in payload)
            {
                if (!first)
                    sb.Append(", ");

                sb.Append(pair.Key).Append('=').Append(pair.Value);
                first = false;
            }

            return sb.ToString();
        }

        private static long GetUtcNowUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}

