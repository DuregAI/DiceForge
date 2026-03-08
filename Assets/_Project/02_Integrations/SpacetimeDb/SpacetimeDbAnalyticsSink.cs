using System;
using Diceforge.Diagnostics;
using SpacetimeDB.Types;
using UnityEngine;

namespace Diceforge.Integrations.SpacetimeDb
{
    public sealed class SpacetimeDbAnalyticsSink : IDiagnosticsSessionSummarySink
    {
        private readonly DbConnection _connection;
        private SessionSummaryData _pendingSummary;
        private string _lastSubmittedSessionId = string.Empty;

        public SpacetimeDbAnalyticsSink(DbConnection connection)
        {
            _connection = connection;
        }

        public void SubmitSessionSummary(SessionSummaryData summary)
        {
            if (summary == null)
                return;

            _pendingSummary = summary;
            TrySubmitPendingSummary();
        }

        public void HandleConnected()
        {
            TrySubmitPendingSummary();
        }

        public void HandleSubmitPerformanceSessionSummary(
            ReducerEventContext ctx,
            string sessionId,
            long startedAtUnixMsUtc,
            long endedAtUnixMsUtc,
            double durationSeconds,
            string buildVersion,
            string platform,
            string operatingSystem,
            string cpuName,
            string gpuName,
            int screenWidth,
            int screenHeight,
            double averageFps,
            double minimumWindowFps,
            double worstSpikeMs,
            int battlesStartedCount,
            int battlesEndedCount)
        {
            if (!string.Equals(sessionId, _lastSubmittedSessionId, StringComparison.Ordinal))
                return;

            Debug.Log($"[SpacetimeDb] submit_performance_session_summary callback session={sessionId} status={ctx.Event.Status}");
        }

        private void TrySubmitPendingSummary()
        {
            if (_pendingSummary == null)
                return;

            if (_connection == null || !_connection.IsActive)
            {
                Debug.Log("[SpacetimeDb] Session summary queued; connection is not active yet.");
                return;
            }

            SessionSummaryData summary = _pendingSummary;
            _pendingSummary = null;
            _lastSubmittedSessionId = Sanitize(summary.SessionId);

            Debug.Log($"[SpacetimeDb] Submitting performance_session_summary session={_lastSubmittedSessionId}");

            _connection.Reducers.SubmitPerformanceSessionSummary(
                _lastSubmittedSessionId,
                summary.SessionStartUnixMsUtc,
                summary.SessionEndUnixMsUtc,
                summary.DurationSeconds,
                Sanitize(summary.BuildVersion),
                Sanitize(summary.Platform),
                Sanitize(summary.OperatingSystem),
                Sanitize(summary.CpuName),
                Sanitize(summary.GpuName),
                summary.ScreenWidth,
                summary.ScreenHeight,
                summary.Performance != null ? summary.Performance.AverageFps : 0d,
                summary.Performance != null ? summary.Performance.MinimumWindowFps : 0d,
                summary.Performance != null ? summary.Performance.WorstSpikeMs : 0d,
                summary.BattlesStartedCount,
                summary.BattlesEndedCount);
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }
    }
}
