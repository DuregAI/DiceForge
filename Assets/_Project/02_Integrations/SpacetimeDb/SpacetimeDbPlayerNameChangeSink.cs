using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Diceforge.Integrations.SpacetimeDb
{
    public sealed class SpacetimeDbPlayerNameChangeSink
    {
        private const int MaxPlayerNameLength = 23;

        private readonly DbConnection _connection;
        private readonly Queue<PendingPlayerNameChangeSubmission> _pendingChanges = new Queue<PendingPlayerNameChangeSubmission>(2);

        public SpacetimeDbPlayerNameChangeSink(DbConnection connection)
        {
            _connection = connection;
        }

        public void SubmitPlayerNameChange(string sessionId, string playerGuid, string previousPlayerName, string newPlayerName, string buildVersion, string sceneName)
        {
            string sanitizedPreviousPlayerName = SanitizePlayerName(previousPlayerName);
            string sanitizedNewPlayerName = SanitizePlayerName(newPlayerName);
            if (string.IsNullOrWhiteSpace(sanitizedNewPlayerName))
            {
                Debug.LogWarning("[SpacetimeDb] Player name change ignored because the new player name is empty.");
                return;
            }

            if (string.Equals(sanitizedPreviousPlayerName, sanitizedNewPlayerName, StringComparison.Ordinal))
                return;

            _pendingChanges.Enqueue(new PendingPlayerNameChangeSubmission(
                Guid.NewGuid().ToString("N"),
                Sanitize(sessionId),
                Sanitize(playerGuid),
                sanitizedPreviousPlayerName,
                sanitizedNewPlayerName,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Sanitize(buildVersion),
                Sanitize(sceneName)));

            TrySubmitPendingChanges();
        }

        public void HandleConnected()
        {
            TrySubmitPendingChanges();
        }

        public void HandleSubmitPlayerNameChange(
            ReducerEventContext ctx,
            string eventId,
            string sessionId,
            string playerGuid,
            string previousPlayerName,
            string newPlayerName,
            long createdAtUnixMsUtc,
            string buildVersion,
            string sceneName)
        {
            Debug.Log(
                $"[SpacetimeDb] submit_player_name_change callback eventId={eventId} session={sessionId} playerGuid={playerGuid} previous={previousPlayerName} next={newPlayerName} scene={sceneName} createdAt={createdAtUnixMsUtc} status={ctx.Event.Status}");
        }

        private void TrySubmitPendingChanges()
        {
            if (_connection == null || !_connection.IsActive)
            {
                if (_pendingChanges.Count > 0)
                    Debug.Log("[SpacetimeDb] Player name change queued; connection is not active yet.");

                return;
            }

            while (_pendingChanges.Count > 0)
            {
                PendingPlayerNameChangeSubmission pendingChange = _pendingChanges.Dequeue();
                Debug.Log(
                    $"[SpacetimeDb] Submitting player_name_change_event eventId={pendingChange.EventId} session={pendingChange.SessionId} playerGuid={pendingChange.PlayerGuid} previous={pendingChange.PreviousPlayerName} next={pendingChange.NewPlayerName} scene={pendingChange.SceneName}");

                _connection.Reducers.SubmitPlayerNameChange(
                    pendingChange.EventId,
                    pendingChange.SessionId,
                    pendingChange.PlayerGuid,
                    pendingChange.PreviousPlayerName,
                    pendingChange.NewPlayerName,
                    pendingChange.CreatedAtUnixMsUtc,
                    pendingChange.BuildVersion,
                    pendingChange.SceneName);
            }
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string SanitizePlayerName(string value)
        {
            string sanitized = Sanitize(value);
            if (sanitized.Length > MaxPlayerNameLength)
            {
                Debug.LogWarning($"[SpacetimeDb] Player name change ignored because the player name exceeds {MaxPlayerNameLength} characters.");
                return string.Empty;
            }

            return sanitized;
        }

        private readonly struct PendingPlayerNameChangeSubmission
        {
            public PendingPlayerNameChangeSubmission(
                string eventId,
                string sessionId,
                string playerGuid,
                string previousPlayerName,
                string newPlayerName,
                long createdAtUnixMsUtc,
                string buildVersion,
                string sceneName)
            {
                EventId = eventId;
                SessionId = sessionId;
                PlayerGuid = playerGuid;
                PreviousPlayerName = previousPlayerName;
                NewPlayerName = newPlayerName;
                CreatedAtUnixMsUtc = createdAtUnixMsUtc;
                BuildVersion = buildVersion;
                SceneName = sceneName;
            }

            public string EventId { get; }
            public string SessionId { get; }
            public string PlayerGuid { get; }
            public string PreviousPlayerName { get; }
            public string NewPlayerName { get; }
            public long CreatedAtUnixMsUtc { get; }
            public string BuildVersion { get; }
            public string SceneName { get; }
        }
    }
}
