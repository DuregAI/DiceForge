using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Diceforge.Tests.BattleTermination
{
    public sealed class ServerConnectionRecoveryTests
    {
        private object originalConfig;
        private UnityEngine.Object config;
        private GameObject host;
        private Component runtime;
        private Component networkManager;
        private readonly List<object> releasedConnections = new();
        private string originalLanguage;
        private bool hadLanguage;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return new EnterPlayMode();
            foreach (UnityEngine.Object client in UnityEngine.Object.FindObjectsByType(
                Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbLocalDevRuntime"), FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(((Component)client).gameObject);
            var configType = Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbRuntimeConfig");
            var current = configType.GetField("_current", Runtime.Members);
            originalConfig = current.GetValue(null);
            config = ScriptableObject.CreateInstance(configType);
            Runtime.Set(config, "serverUri", "http://127.0.0.1:1");
            Runtime.Set(config, "databaseName", "connection-recovery-test");
            current.SetValue(null, config);
            originalLanguage = PlayerPrefs.GetString("ui.language", "en");
            hadLanguage = PlayerPrefs.HasKey("ui.language");
            PlayerPrefs.SetString("ui.language", "ru");
            host = new GameObject("Connection recovery test");
            var connectionType = Type.GetType("SpacetimeDB.Types.DbConnection, Assembly-CSharp", true);
            networkManager = host.AddComponent(connectionType.BaseType.Assembly.GetType("SpacetimeDB.SpacetimeDBNetworkManager", true));
            ((Behaviour)networkManager).enabled = false;
            runtime = host.AddComponent(Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbLocalDevRuntime"));
            ((Behaviour)runtime).enabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            Runtime.Type("Integrations.SpacetimeDb.SpacetimeDbRuntimeConfig").GetField("_current", Runtime.Members)
                .SetValue(null, originalConfig);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            if (hadLanguage) PlayerPrefs.SetString("ui.language", originalLanguage);
            else PlayerPrefs.DeleteKey("ui.language");
            releasedConnections.Clear();
            yield return new ExitPlayMode();
        }

        private object Connection => Runtime.Field(runtime, "_connection");

        private static bool ParserCancelled(object connection)
        {
            FieldInfo cancellation = connection.GetType().BaseType.GetField("_parseCancellationTokenSource",
                BindingFlags.Instance | BindingFlags.NonPublic);
            return ((CancellationTokenSource)cancellation.GetValue(connection)).IsCancellationRequested;
        }

        [UnityTest]
        public IEnumerator TwentyThreeFailuresStopAllRetriesAndReleaseEveryParserAndSink()
        {
            for (int attempt = 1; attempt <= 23; attempt++)
            {
                Assert.That(Runtime.Field(runtime, "_connectionAttempts"), Is.EqualTo(attempt));
                Assert.That(Connection, Is.Not.Null);
                releasedConnections.Add(Connection);
                LogAssert.Expect(LogType.Warning, new Regex("Connect failed: simulated failure"));
                Runtime.Call(runtime, "HandleConnectError", new Exception("simulated failure"));
                if (attempt == 23) LogAssert.Expect(LogType.Warning, new Regex("Server unavailable after 23 attempts"));
                Runtime.Call(runtime, "TickConnection", Time.realtimeSinceStartup);
                Assert.That(Connection, Is.Null);
                Assert.That(ParserCancelled(releasedConnections[attempt - 1]), Is.True);
                Assert.That(((ICollection)Runtime.Field(networkManager, "activeConnections")).Count, Is.EqualTo(0),
                    "The SDK network manager retained the released connection.");
                foreach (string field in new[] { "_analyticsSink", "_likeSink", "_feedbackSink", "_musicEventSink", "_playerNameChangeSink", "_progressSync" })
                    Assert.That(Runtime.Field(runtime, field), Is.Null, field + " retained the failed connection.");
                if (attempt != 23)
                {
                    Runtime.Set(runtime, "_lastConnectionAttemptAt", Time.realtimeSinceStartup - 16f);
                    Runtime.Call(runtime, "InitializeIfNeeded");
                }
            }
            Assert.That(Runtime.Field(runtime, "_connectionAttemptsStopped"), Is.True);
            for (int tick = 0; tick < 100; tick++) Runtime.Call(runtime, "TickConnection", Time.realtimeSinceStartup + 1000f);
            Runtime.Call(runtime, "SubmitMusicTrackLikeInternal", "test");
            Runtime.Call(runtime, "SubmitMusicTrackSkipInternal", "test", 0L);
            Runtime.Call(runtime, "SubmitPlayerNameChangeInternal", "old", "new");
            Runtime.Call(runtime, "SubmitFeedbackInternal", "bug", "test", "test", "test", null);
            Assert.That(Runtime.Field(runtime, "_connectionAttempts"), Is.EqualTo(23));
            Assert.That(Connection, Is.Null);
            var docs = host.GetComponentsInChildren<UIDocument>();
            Assert.That(docs.Length, Is.EqualTo(1), "The failure window must only appear once.");
            yield return null;
            yield return null;
            var root = docs[0].rootVisualElement;
            Assert.That(root.Q<Label>("serverUnavailableMessage").text, Does.Contain("23"));
            Button close = root.Q<Button>("serverUnavailableClose");
            Assert.That(close.worldBound.width, Is.GreaterThan(0));
            Assert.That(close.enabledInHierarchy, Is.True);
            System.IO.Directory.CreateDirectory("docs/Validation/ConnectionRecovery");
            ScreenCapture.CaptureScreenshot("docs/Validation/ConnectionRecovery/server-unavailable-ru.png");
            yield return null;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = close;
                close.SendEvent(submit);
            }
            yield return null;
            Assert.That(host.GetComponentsInChildren<UIDocument>().Length, Is.EqualTo(0));
            Runtime.Call(runtime, "InitializeIfNeeded");
            Assert.That(Connection, Is.Null, "Closing the dialog must not reconnect.");
        }

        [UnityTest]
        public IEnumerator FinalHandshakeTimeoutStopsAndCancelsItsParser()
        {
            object pending = Connection;
            Runtime.Set(runtime, "_connectionAttempts", 23);
            Runtime.Set(runtime, "_lastConnectionAttemptAt", Time.realtimeSinceStartup - 16f);
            LogAssert.Expect(LogType.Warning, new Regex("Server unavailable after 23 attempts"));
            Runtime.Call(runtime, "TickConnection", Time.realtimeSinceStartup);
            Assert.That(Runtime.Field(runtime, "_connectionAttemptsStopped"), Is.True);
            Assert.That(Connection, Is.Null);
            Assert.That(ParserCancelled(pending), Is.True);
            yield return null;
        }
    }
}
