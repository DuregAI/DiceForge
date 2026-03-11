using System;
using Diceforge.Diagnostics;
using Diceforge.Progression;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Diceforge.Integrations.SpacetimeDb
{
    internal static class SpacetimeDbLocalDevRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SpacetimeDbLocalDevRuntime.ResetStaticState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SpacetimeDbLocalDevRuntime.EnsureCreated();
        }
    }

    public sealed class SpacetimeDbLocalDevRuntime : MonoBehaviour
    {
        private const string RuntimeObjectName = "SpacetimeDbLocalDevRuntime";
        private const string ServerUri = "http://localhost:3000";
        private const string DatabaseName = "diceforgelocaldev";

        private static SpacetimeDbLocalDevRuntime _instance;

        private bool _initialized;
        private bool _sinkRegistered;
        private DbConnection _connection;
        private SpacetimeDbAnalyticsSink _analyticsSink;
        private SpacetimeDbLikeSink _likeSink;
        private SpacetimeDbFeedbackSink _feedbackSink;
        private SpacetimeDbMusicEventSink _musicEventSink;
        private SpacetimeDbPlayerNameChangeSink _playerNameChangeSink;

        public static SpacetimeDbLocalDevRuntime EnsureCreated()
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<SpacetimeDbLocalDevRuntime>();
            if (_instance != null)
            {
                _instance.InitializeIfNeeded();
                return _instance;
            }

            var runtimeObject = new GameObject(RuntimeObjectName);
            runtimeObject.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(runtimeObject);

            _instance = runtimeObject.AddComponent<SpacetimeDbLocalDevRuntime>();
            return _instance;
        }

        public static void SubmitMusicTrackLike(string trackId)
        {
            EnsureCreated().SubmitMusicTrackLikeInternal(trackId);
        }

        public static void SubmitMusicTrackDislike(string trackId, long trackElapsedMs)
        {
            EnsureCreated().SubmitMusicTrackDislikeInternal(trackId, trackElapsedMs);
        }

        public static void SubmitMusicTrackSkip(string trackId, long trackElapsedMs)
        {
            EnsureCreated().SubmitMusicTrackSkipInternal(trackId, trackElapsedMs);
        }

        public static void SubmitFeedback(string category, string message, string buildVersion, string sceneName)
        {
            EnsureCreated().SubmitFeedbackInternal(category, message, buildVersion, sceneName);
        }

        public static void SubmitPlayerNameChange(string previousPlayerName, string newPlayerName)
        {
            EnsureCreated().SubmitPlayerNameChangeInternal(previousPlayerName, newPlayerName);
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

        private void Update()
        {
            if (!_initialized || _connection == null)
                return;

            // Advance the generated SpacetimeDB client from Unity's frame loop.
            _connection.FrameTick();
        }

        private void OnDestroy()
        {
            if (_connection != null)
            {
                if (_analyticsSink != null)
                    _connection.Reducers.OnSubmitPerformanceSessionSummary -= _analyticsSink.HandleSubmitPerformanceSessionSummary;

                if (_likeSink != null)
                    _connection.Reducers.OnSubmitLike -= _likeSink.HandleSubmitLike;

                if (_feedbackSink != null)
                    _connection.Reducers.OnSubmitFeedback -= _feedbackSink.HandleSubmitFeedback;

                if (_musicEventSink != null)
                {
                    _connection.Reducers.OnSubmitMusicDislike -= _musicEventSink.HandleSubmitMusicDislike;
                    _connection.Reducers.OnSubmitMusicSkip -= _musicEventSink.HandleSubmitMusicSkip;
                }

                if (_playerNameChangeSink != null)
                    _connection.Reducers.OnSubmitPlayerNameChange -= _playerNameChangeSink.HandleSubmitPlayerNameChange;

                _connection.OnUnhandledReducerError -= HandleUnhandledReducerError;

                if (_connection.IsActive)
                    _connection.Disconnect();
            }

            if (_analyticsSink != null && _sinkRegistered)
                ClientDiagnostics.UnregisterSessionSummarySink(_analyticsSink);

            if (_instance == this)
                _instance = null;
        }

        private void InitializeIfNeeded()
        {
            if (_initialized)
                return;

            Debug.Log($"[SpacetimeDb] Connecting to local database '{DatabaseName}' at {ServerUri}.");

            _connection = DbConnection.Builder()
                .WithUri(ServerUri)
                .WithDatabaseName(DatabaseName)
                .OnConnect(HandleConnect)
                .OnConnectError(HandleConnectError)
                .OnDisconnect(HandleDisconnect)
                .Build();

            _analyticsSink = new SpacetimeDbAnalyticsSink(_connection);
            _likeSink = new SpacetimeDbLikeSink(_connection);
            _feedbackSink = new SpacetimeDbFeedbackSink(_connection);
            _musicEventSink = new SpacetimeDbMusicEventSink(_connection);
            _playerNameChangeSink = new SpacetimeDbPlayerNameChangeSink(_connection);

            _connection.Reducers.OnSubmitPerformanceSessionSummary += _analyticsSink.HandleSubmitPerformanceSessionSummary;
            _connection.Reducers.OnSubmitLike += _likeSink.HandleSubmitLike;
            _connection.Reducers.OnSubmitFeedback += _feedbackSink.HandleSubmitFeedback;
            _connection.Reducers.OnSubmitMusicDislike += _musicEventSink.HandleSubmitMusicDislike;
            _connection.Reducers.OnSubmitMusicSkip += _musicEventSink.HandleSubmitMusicSkip;
            _connection.Reducers.OnSubmitPlayerNameChange += _playerNameChangeSink.HandleSubmitPlayerNameChange;
            _connection.OnUnhandledReducerError += HandleUnhandledReducerError;

            ClientDiagnostics.RegisterSessionSummarySink(_analyticsSink);
            _sinkRegistered = true;
            _initialized = true;
        }

        private void SubmitMusicTrackLikeInternal(string trackId)
        {
            InitializeIfNeeded();

            if (string.IsNullOrWhiteSpace(trackId) || _likeSink == null)
                return;

            _likeSink.SubmitMusicTrackLike(
                ClientDiagnostics.GetCurrentSessionId(),
                GetCurrentPlayerGuid(),
                GetCurrentPlayerName(),
                trackId);
        }

        private void SubmitMusicTrackDislikeInternal(string trackId, long trackElapsedMs)
        {
            InitializeIfNeeded();

            if (string.IsNullOrWhiteSpace(trackId) || _musicEventSink == null)
                return;

            _musicEventSink.SubmitMusicDislike(
                ClientDiagnostics.GetCurrentSessionId(),
                GetCurrentPlayerGuid(),
                GetCurrentPlayerName(),
                trackId,
                trackElapsedMs,
                Application.version,
                SceneManager.GetActiveScene().name);
        }

        private void SubmitMusicTrackSkipInternal(string trackId, long trackElapsedMs)
        {
            InitializeIfNeeded();

            if (string.IsNullOrWhiteSpace(trackId) || _musicEventSink == null)
                return;

            _musicEventSink.SubmitMusicSkip(
                ClientDiagnostics.GetCurrentSessionId(),
                GetCurrentPlayerGuid(),
                GetCurrentPlayerName(),
                trackId,
                trackElapsedMs,
                Application.version,
                SceneManager.GetActiveScene().name);
        }

        private void SubmitFeedbackInternal(string category, string message, string buildVersion, string sceneName)
        {
            InitializeIfNeeded();

            if (_feedbackSink == null)
                return;

            _feedbackSink.SubmitFeedback(
                ClientDiagnostics.GetCurrentSessionId(),
                GetCurrentPlayerGuid(),
                GetCurrentPlayerName(),
                category,
                message,
                buildVersion,
                sceneName);
        }

        private void SubmitPlayerNameChangeInternal(string previousPlayerName, string newPlayerName)
        {
            InitializeIfNeeded();

            if (_playerNameChangeSink == null)
                return;

            _playerNameChangeSink.SubmitPlayerNameChange(
                ClientDiagnostics.GetCurrentSessionId(),
                GetCurrentPlayerGuid(),
                previousPlayerName,
                newPlayerName,
                Application.version,
                SceneManager.GetActiveScene().name);
        }

        private void HandleConnect(DbConnection connection, Identity identity, string token)
        {
            Debug.Log($"[SpacetimeDb] Connected to '{DatabaseName}' identity={identity}.");
            if (_analyticsSink != null)
                _analyticsSink.HandleConnected();

            if (_likeSink != null)
                _likeSink.HandleConnected();

            if (_feedbackSink != null)
                _feedbackSink.HandleConnected();

            if (_musicEventSink != null)
                _musicEventSink.HandleConnected();

            if (_playerNameChangeSink != null)
                _playerNameChangeSink.HandleConnected();
        }

        private void HandleConnectError(Exception exception)
        {
            Debug.LogWarning($"[SpacetimeDb] Connect failed: {exception.Message}", this);
        }

        private void HandleDisconnect(DbConnection connection, Exception exception)
        {
            string reason = exception != null ? exception.Message : "unknown";
            Debug.LogWarning($"[SpacetimeDb] Disconnected from '{DatabaseName}': {reason}", this);
        }

        private void HandleUnhandledReducerError(ReducerEventContext context, Exception exception)
        {
            Debug.LogWarning($"[SpacetimeDb] Reducer error: {exception.Message}", this);
        }

        private static string GetCurrentPlayerGuid()
        {
            string playerGuid = ProfileService.Current.playerGuid;
            return string.IsNullOrWhiteSpace(playerGuid) ? string.Empty : playerGuid.Trim();
        }

        private static string GetCurrentPlayerName()
        {
            return ProfileService.GetDisplayName();
        }
    }
}
