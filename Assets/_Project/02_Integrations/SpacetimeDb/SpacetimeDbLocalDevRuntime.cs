using System;
using Diceforge.Diagnostics;
using SpacetimeDB;
using SpacetimeDB.Types;
using UnityEngine;

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
            _connection.Reducers.OnSubmitPerformanceSessionSummary += _analyticsSink.HandleSubmitPerformanceSessionSummary;
            _connection.OnUnhandledReducerError += HandleUnhandledReducerError;

            ClientDiagnostics.RegisterSessionSummarySink(_analyticsSink);
            _sinkRegistered = true;
            _initialized = true;
        }

        private void HandleConnect(DbConnection connection, Identity identity, string token)
        {
            Debug.Log($"[SpacetimeDb] Connected to '{DatabaseName}' identity={identity}.");
            if (_analyticsSink != null)
                _analyticsSink.HandleConnected();
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
    }
}
