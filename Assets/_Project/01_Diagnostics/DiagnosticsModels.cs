using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Diceforge.Diagnostics
{
    public enum DiagnosticsEventType
    {
        SessionStarted = 0,
        SceneLoaded = 1,
        BattleStarted = 2,
        BattleEnded = 3,
        SessionEnded = 4,
        BattleSurrender = 5
    }

    public sealed class AnalyticsEventData
    {
        public AnalyticsEventData(DiagnosticsEventType eventType, long timestampUnixMsUtc, string sessionId, Dictionary<string, string> payload, string displayName = null)
        {
            EventType = eventType;
            TimestampUnixMsUtc = timestampUnixMsUtc;
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? string.Empty : sessionId;
            Payload = payload ?? new Dictionary<string, string>(0, StringComparer.Ordinal);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? eventType.ToString() : displayName;
        }

        public DiagnosticsEventType EventType { get; }
        public long TimestampUnixMsUtc { get; }
        public string SessionId { get; }
        public Dictionary<string, string> Payload { get; }
        public string DisplayName { get; }
    }

    public sealed class SessionDiagnosticsSnapshot
    {
        public string SessionId { get; private set; }
        public long SessionStartUnixMsUtc { get; private set; }
        public string AppVersion { get; private set; }
        public string UnityVersion { get; private set; }
        public string Platform { get; private set; }
        public string OperatingSystem { get; private set; }
        public string CpuName { get; private set; }
        public int CpuCoreCount { get; private set; }
        public string GpuName { get; private set; }
        public int SystemMemoryMb { get; private set; }
        public int GraphicsMemoryMb { get; private set; }
        public int ScreenWidth { get; private set; }
        public int ScreenHeight { get; private set; }
        public string FullscreenMode { get; private set; }
        public float RefreshRateHz { get; private set; }
        public int TargetFrameRate { get; private set; }
        public int VSyncCount { get; private set; }

        public static SessionDiagnosticsSnapshot Capture(string sessionId, long sessionStartUnixMsUtc)
        {
            return new SessionDiagnosticsSnapshot
            {
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? string.Empty : sessionId,
                SessionStartUnixMsUtc = sessionStartUnixMsUtc,
                AppVersion = Application.version,
                UnityVersion = Application.unityVersion,
                Platform = Application.platform.ToString(),
                OperatingSystem = SystemInfo.operatingSystem,
                CpuName = SystemInfo.processorType,
                CpuCoreCount = SystemInfo.processorCount,
                GpuName = SystemInfo.graphicsDeviceName,
                SystemMemoryMb = SystemInfo.systemMemorySize,
                GraphicsMemoryMb = SystemInfo.graphicsMemorySize,
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
                FullscreenMode = Screen.fullScreenMode.ToString(),
                RefreshRateHz = (float)Screen.currentResolution.refreshRateRatio.value,
                TargetFrameRate = Application.targetFrameRate,
                VSyncCount = QualitySettings.vSyncCount
            };
        }

        public Dictionary<string, string> ToPayload()
        {
            var payload = new Dictionary<string, string>(16, StringComparer.Ordinal);
            DiagnosticsPayloadUtility.Add(payload, "sessionStartUnixMsUtc", SessionStartUnixMsUtc);
            DiagnosticsPayloadUtility.Add(payload, "appVersion", AppVersion);
            DiagnosticsPayloadUtility.Add(payload, "unityVersion", UnityVersion);
            DiagnosticsPayloadUtility.Add(payload, "platform", Platform);
            DiagnosticsPayloadUtility.Add(payload, "operatingSystem", OperatingSystem);
            DiagnosticsPayloadUtility.Add(payload, "cpuName", CpuName);
            DiagnosticsPayloadUtility.Add(payload, "cpuCoreCount", CpuCoreCount);
            DiagnosticsPayloadUtility.Add(payload, "gpuName", GpuName);
            DiagnosticsPayloadUtility.Add(payload, "systemMemoryMb", SystemMemoryMb);
            DiagnosticsPayloadUtility.Add(payload, "graphicsMemoryMb", GraphicsMemoryMb);
            DiagnosticsPayloadUtility.Add(payload, "screenWidth", ScreenWidth);
            DiagnosticsPayloadUtility.Add(payload, "screenHeight", ScreenHeight);
            DiagnosticsPayloadUtility.Add(payload, "fullscreenMode", FullscreenMode);
            DiagnosticsPayloadUtility.Add(payload, "refreshRateHz", RefreshRateHz);
            DiagnosticsPayloadUtility.Add(payload, "targetFrameRate", TargetFrameRate);
            DiagnosticsPayloadUtility.Add(payload, "vSyncCount", VSyncCount);
            return payload;
        }
    }

    public sealed class PerformanceSessionAggregate
    {
        public float SampleWindowSeconds { get; set; }
        public int TotalFrames { get; set; }
        public double TotalSampledSeconds { get; set; }
        public float AverageFps { get; set; }
        public float MinimumWindowFps { get; set; }
        public float AverageFrameTimeMs { get; set; }
        public float WorstSpikeMs { get; set; }

        public PerformanceSessionAggregate Copy()
        {
            return new PerformanceSessionAggregate
            {
                SampleWindowSeconds = SampleWindowSeconds,
                TotalFrames = TotalFrames,
                TotalSampledSeconds = TotalSampledSeconds,
                AverageFps = AverageFps,
                MinimumWindowFps = MinimumWindowFps,
                AverageFrameTimeMs = AverageFrameTimeMs,
                WorstSpikeMs = WorstSpikeMs
            };
        }

        public Dictionary<string, string> ToPayload()
        {
            var payload = new Dictionary<string, string>(7, StringComparer.Ordinal);
            DiagnosticsPayloadUtility.Add(payload, "sampleWindowSeconds", SampleWindowSeconds);
            DiagnosticsPayloadUtility.Add(payload, "totalFrames", TotalFrames);
            DiagnosticsPayloadUtility.Add(payload, "totalSampledSeconds", TotalSampledSeconds);
            DiagnosticsPayloadUtility.Add(payload, "averageFps", AverageFps);
            DiagnosticsPayloadUtility.Add(payload, "minimumWindowFps", MinimumWindowFps);
            DiagnosticsPayloadUtility.Add(payload, "averageFrameTimeMs", AverageFrameTimeMs);
            DiagnosticsPayloadUtility.Add(payload, "worstSpikeMs", WorstSpikeMs);
            return payload;
        }
    }

    public readonly struct BattleStartDiagnosticsContext
    {
        public BattleStartDiagnosticsContext(string modeId, string rulesetId, string setupId, string mapId, string mapName, int boardCellCount)
        {
            ModeId = modeId ?? string.Empty;
            RulesetId = rulesetId ?? string.Empty;
            SetupId = setupId ?? string.Empty;
            MapId = mapId ?? string.Empty;
            MapName = mapName ?? string.Empty;
            BoardCellCount = boardCellCount;
        }

        public string ModeId { get; }
        public string RulesetId { get; }
        public string SetupId { get; }
        public string MapId { get; }
        public string MapName { get; }
        public int BoardCellCount { get; }

        public Dictionary<string, string> ToPayload()
        {
            var payload = new Dictionary<string, string>(6, StringComparer.Ordinal);
            DiagnosticsPayloadUtility.Add(payload, "modeId", ModeId);
            DiagnosticsPayloadUtility.Add(payload, "rulesetId", RulesetId);
            DiagnosticsPayloadUtility.Add(payload, "setupId", SetupId);
            DiagnosticsPayloadUtility.Add(payload, "mapId", MapId);
            DiagnosticsPayloadUtility.Add(payload, "mapName", MapName);
            DiagnosticsPayloadUtility.Add(payload, "boardCellCount", BoardCellCount);
            return payload;
        }
    }

    public readonly struct BattleEndDiagnosticsContext
    {
        public BattleEndDiagnosticsContext(string winner, string endReason, int turnIndex)
        {
            Winner = winner ?? string.Empty;
            EndReason = endReason ?? string.Empty;
            TurnIndex = turnIndex;
        }

        public string Winner { get; }
        public string EndReason { get; }
        public int TurnIndex { get; }

        public Dictionary<string, string> ToPayload()
        {
            var payload = new Dictionary<string, string>(3, StringComparer.Ordinal);
            DiagnosticsPayloadUtility.Add(payload, "winner", Winner);
            DiagnosticsPayloadUtility.Add(payload, "endReason", EndReason);
            DiagnosticsPayloadUtility.Add(payload, "turnIndex", TurnIndex);
            return payload;
        }
    }

    public readonly struct BattleSurrenderDiagnosticsContext
    {
        public const string EventName = "battle_surrender";

        public BattleSurrenderDiagnosticsContext(string battleId, int turnNumber, int playerHp, int enemyHp)
        {
            BattleId = battleId ?? string.Empty;
            TurnNumber = turnNumber;
            PlayerHp = playerHp;
            EnemyHp = enemyHp;
        }

        public string BattleId { get; }
        public int TurnNumber { get; }
        public int PlayerHp { get; }
        public int EnemyHp { get; }

        public Dictionary<string, string> ToPayload()
        {
            var payload = new Dictionary<string, string>(4, StringComparer.Ordinal);
            DiagnosticsPayloadUtility.Add(payload, "battle_id", BattleId);
            DiagnosticsPayloadUtility.Add(payload, "turn_number", TurnNumber);
            DiagnosticsPayloadUtility.Add(payload, "player_hp", PlayerHp);
            DiagnosticsPayloadUtility.Add(payload, "enemy_hp", EnemyHp);
            return payload;
        }
    }

    public sealed class SessionSummaryData
    {
        public string SessionId { get; set; }
        public long SessionStartUnixMsUtc { get; set; }
        public long SessionEndUnixMsUtc { get; set; }
        public double DurationSeconds { get; set; }
        public string BuildVersion { get; set; }
        public string Platform { get; set; }
        public string OperatingSystem { get; set; }
        public string CpuName { get; set; }
        public string GpuName { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public string LastSceneName { get; set; }
        public int ScenesLoadedCount { get; set; }
        public int BattlesStartedCount { get; set; }
        public int BattlesEndedCount { get; set; }
        public PerformanceSessionAggregate Performance { get; set; }

        public Dictionary<string, string> ToPayload()
        {
            var payload = new Dictionary<string, string>(23, StringComparer.Ordinal);
            DiagnosticsPayloadUtility.Add(payload, "sessionStartUnixMsUtc", SessionStartUnixMsUtc);
            DiagnosticsPayloadUtility.Add(payload, "sessionEndUnixMsUtc", SessionEndUnixMsUtc);
            DiagnosticsPayloadUtility.Add(payload, "durationSeconds", DurationSeconds);
            DiagnosticsPayloadUtility.Add(payload, "buildVersion", BuildVersion ?? string.Empty);
            DiagnosticsPayloadUtility.Add(payload, "platform", Platform ?? string.Empty);
            DiagnosticsPayloadUtility.Add(payload, "operatingSystem", OperatingSystem ?? string.Empty);
            DiagnosticsPayloadUtility.Add(payload, "cpuName", CpuName ?? string.Empty);
            DiagnosticsPayloadUtility.Add(payload, "gpuName", GpuName ?? string.Empty);
            DiagnosticsPayloadUtility.Add(payload, "screenWidth", ScreenWidth);
            DiagnosticsPayloadUtility.Add(payload, "screenHeight", ScreenHeight);
            DiagnosticsPayloadUtility.Add(payload, "lastSceneName", LastSceneName ?? string.Empty);
            DiagnosticsPayloadUtility.Add(payload, "scenesLoadedCount", ScenesLoadedCount);
            DiagnosticsPayloadUtility.Add(payload, "battlesStartedCount", BattlesStartedCount);
            DiagnosticsPayloadUtility.Add(payload, "battlesEndedCount", BattlesEndedCount);

            if (Performance != null)
            {
                foreach (var pair in Performance.ToPayload())
                    payload[pair.Key] = pair.Value;
            }

            return payload;
        }
    }

    internal static class DiagnosticsPayloadUtility
    {
        private const string FloatFormat = "0.###";

        public static void Add(Dictionary<string, string> payload, string key, string value)
        {
            if (payload == null || string.IsNullOrEmpty(key) || value == null)
                return;

            payload[key] = value;
        }

        public static void Add(Dictionary<string, string> payload, string key, int value)
        {
            Add(payload, key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Add(Dictionary<string, string> payload, string key, long value)
        {
            Add(payload, key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Add(Dictionary<string, string> payload, string key, float value)
        {
            Add(payload, key, value.ToString(FloatFormat, CultureInfo.InvariantCulture));
        }

        public static void Add(Dictionary<string, string> payload, string key, double value)
        {
            Add(payload, key, value.ToString(FloatFormat, CultureInfo.InvariantCulture));
        }
    }
}