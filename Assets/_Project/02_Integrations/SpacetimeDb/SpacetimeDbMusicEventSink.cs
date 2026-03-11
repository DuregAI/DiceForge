using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Diceforge.Integrations.SpacetimeDb
{
    public sealed class SpacetimeDbMusicEventSink
    {
        private readonly DbConnection _connection;
        private readonly Queue<PendingMusicEventSubmission> _pendingMusicDislikes = new Queue<PendingMusicEventSubmission>(2);
        private readonly Queue<PendingMusicEventSubmission> _pendingMusicSkips = new Queue<PendingMusicEventSubmission>(4);

        public SpacetimeDbMusicEventSink(DbConnection connection)
        {
            _connection = connection;
        }

        public void SubmitMusicDislike(string sessionId, string playerGuid, string playerName, string trackId, long trackElapsedMs, string buildVersion, string sceneName)
        {
            if (!TryCreatePendingSubmission(sessionId, playerGuid, playerName, trackId, trackElapsedMs, buildVersion, sceneName, out PendingMusicEventSubmission pendingSubmission))
                return;

            _pendingMusicDislikes.Enqueue(pendingSubmission);
            TrySubmitPendingMusicEvents();
        }

        public void SubmitMusicSkip(string sessionId, string playerGuid, string playerName, string trackId, long trackElapsedMs, string buildVersion, string sceneName)
        {
            if (!TryCreatePendingSubmission(sessionId, playerGuid, playerName, trackId, trackElapsedMs, buildVersion, sceneName, out PendingMusicEventSubmission pendingSubmission))
                return;

            _pendingMusicSkips.Enqueue(pendingSubmission);
            TrySubmitPendingMusicEvents();
        }

        public void HandleConnected()
        {
            TrySubmitPendingMusicEvents();
        }

        public void HandleSubmitMusicDislike(
            ReducerEventContext ctx,
            string eventId,
            string sessionId,
            string playerGuid,
            string playerName,
            string trackId,
            long trackElapsedMs,
            long createdAtUnixMsUtc,
            string buildVersion,
            string sceneName)
        {
            Debug.Log(
                $"[SpacetimeDb] submit_music_dislike callback eventId={eventId} session={sessionId} playerGuid={playerGuid} playerName={playerName} trackId={trackId} elapsedMs={trackElapsedMs} scene={sceneName} createdAt={createdAtUnixMsUtc} status={ctx.Event.Status}");
        }

        public void HandleSubmitMusicSkip(
            ReducerEventContext ctx,
            string eventId,
            string sessionId,
            string playerGuid,
            string playerName,
            string trackId,
            long trackElapsedMs,
            long createdAtUnixMsUtc,
            string buildVersion,
            string sceneName)
        {
            Debug.Log(
                $"[SpacetimeDb] submit_music_skip callback eventId={eventId} session={sessionId} playerGuid={playerGuid} playerName={playerName} trackId={trackId} elapsedMs={trackElapsedMs} scene={sceneName} createdAt={createdAtUnixMsUtc} status={ctx.Event.Status}");
        }

        private void TrySubmitPendingMusicEvents()
        {
            if (_connection == null || !_connection.IsActive)
            {
                if (_pendingMusicDislikes.Count > 0 || _pendingMusicSkips.Count > 0)
                    Debug.Log("[SpacetimeDb] Music analytics queued; connection is not active yet.");

                return;
            }

            while (_pendingMusicDislikes.Count > 0)
            {
                PendingMusicEventSubmission pendingSubmission = _pendingMusicDislikes.Dequeue();
                Debug.Log(
                    $"[SpacetimeDb] Submitting music_dislike_event eventId={pendingSubmission.EventId} session={pendingSubmission.SessionId} playerGuid={pendingSubmission.PlayerGuid} playerName={pendingSubmission.PlayerName} trackId={pendingSubmission.TrackId} elapsedMs={pendingSubmission.TrackElapsedMs} scene={pendingSubmission.SceneName}");

                _connection.Reducers.SubmitMusicDislike(
                    pendingSubmission.EventId,
                    pendingSubmission.SessionId,
                    pendingSubmission.PlayerGuid,
                    pendingSubmission.PlayerName,
                    pendingSubmission.TrackId,
                    pendingSubmission.TrackElapsedMs,
                    pendingSubmission.CreatedAtUnixMsUtc,
                    pendingSubmission.BuildVersion,
                    pendingSubmission.SceneName);
            }

            while (_pendingMusicSkips.Count > 0)
            {
                PendingMusicEventSubmission pendingSubmission = _pendingMusicSkips.Dequeue();
                Debug.Log(
                    $"[SpacetimeDb] Submitting music_skip_event eventId={pendingSubmission.EventId} session={pendingSubmission.SessionId} playerGuid={pendingSubmission.PlayerGuid} playerName={pendingSubmission.PlayerName} trackId={pendingSubmission.TrackId} elapsedMs={pendingSubmission.TrackElapsedMs} scene={pendingSubmission.SceneName}");

                _connection.Reducers.SubmitMusicSkip(
                    pendingSubmission.EventId,
                    pendingSubmission.SessionId,
                    pendingSubmission.PlayerGuid,
                    pendingSubmission.PlayerName,
                    pendingSubmission.TrackId,
                    pendingSubmission.TrackElapsedMs,
                    pendingSubmission.CreatedAtUnixMsUtc,
                    pendingSubmission.BuildVersion,
                    pendingSubmission.SceneName);
            }
        }

        private static bool TryCreatePendingSubmission(
            string sessionId,
            string playerGuid,
            string playerName,
            string trackId,
            long trackElapsedMs,
            string buildVersion,
            string sceneName,
            out PendingMusicEventSubmission pendingSubmission)
        {
            string sanitizedTrackId = Sanitize(trackId);
            if (string.IsNullOrWhiteSpace(sanitizedTrackId))
            {
                pendingSubmission = default;
                return false;
            }

            pendingSubmission = new PendingMusicEventSubmission(
                Guid.NewGuid().ToString("N"),
                Sanitize(sessionId),
                Sanitize(playerGuid),
                Sanitize(playerName),
                sanitizedTrackId,
                Math.Max(0L, trackElapsedMs),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Sanitize(buildVersion),
                Sanitize(sceneName));

            return true;
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private readonly struct PendingMusicEventSubmission
        {
            public PendingMusicEventSubmission(
                string eventId,
                string sessionId,
                string playerGuid,
                string playerName,
                string trackId,
                long trackElapsedMs,
                long createdAtUnixMsUtc,
                string buildVersion,
                string sceneName)
            {
                EventId = eventId;
                SessionId = sessionId;
                PlayerGuid = playerGuid;
                PlayerName = playerName;
                TrackId = trackId;
                TrackElapsedMs = trackElapsedMs;
                CreatedAtUnixMsUtc = createdAtUnixMsUtc;
                BuildVersion = buildVersion;
                SceneName = sceneName;
            }

            public string EventId { get; }
            public string SessionId { get; }
            public string PlayerGuid { get; }
            public string PlayerName { get; }
            public string TrackId { get; }
            public long TrackElapsedMs { get; }
            public long CreatedAtUnixMsUtc { get; }
            public string BuildVersion { get; }
            public string SceneName { get; }
        }
    }
}
