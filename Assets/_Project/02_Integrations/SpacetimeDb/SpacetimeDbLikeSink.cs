using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Diceforge.Integrations.SpacetimeDb
{
    public sealed class SpacetimeDbLikeSink
    {
        private const string MusicTrackTargetType = "music_track";

        private readonly DbConnection _connection;
        private readonly Queue<PendingLikeSubmission> _pendingLikes = new Queue<PendingLikeSubmission>(4);

        public SpacetimeDbLikeSink(DbConnection connection)
        {
            _connection = connection;
        }

        public void SubmitMusicTrackLike(string sessionId, string playerGuid, string playerName, string trackId)
        {
            if (string.IsNullOrWhiteSpace(trackId))
                return;

            _pendingLikes.Enqueue(new PendingLikeSubmission(
                Guid.NewGuid().ToString("N"),
                Sanitize(sessionId),
                Sanitize(playerGuid),
                Sanitize(playerName),
                trackId,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

            TrySubmitPendingLikes();
        }

        public void HandleConnected()
        {
            TrySubmitPendingLikes();
        }

        public void HandleSubmitLike(
            ReducerEventContext ctx,
            string likeId,
            string sessionId,
            string playerGuid,
            string playerName,
            string targetType,
            string targetId,
            long createdAtUnixMsUtc)
        {
            Debug.Log(
                $"[SpacetimeDb] submit_like callback likeId={likeId} session={sessionId} playerGuid={playerGuid} playerName={playerName} targetType={targetType} targetId={targetId} createdAt={createdAtUnixMsUtc} status={ctx.Event.Status}");
        }

        private void TrySubmitPendingLikes()
        {
            if (_connection == null || !_connection.IsActive)
            {
                if (_pendingLikes.Count > 0)
                    Debug.Log("[SpacetimeDb] Like queued; connection is not active yet.");

                return;
            }

            while (_pendingLikes.Count > 0)
            {
                PendingLikeSubmission pendingLike = _pendingLikes.Dequeue();
                Debug.Log(
                    $"[SpacetimeDb] Submitting like_event likeId={pendingLike.LikeId} session={pendingLike.SessionId} playerGuid={pendingLike.PlayerGuid} playerName={pendingLike.PlayerName} targetType={MusicTrackTargetType} targetId={pendingLike.TrackId}");

                _connection.Reducers.SubmitLike(
                    pendingLike.LikeId,
                    pendingLike.SessionId,
                    pendingLike.PlayerGuid,
                    pendingLike.PlayerName,
                    MusicTrackTargetType,
                    pendingLike.TrackId,
                    pendingLike.CreatedAtUnixMsUtc);
            }
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private readonly struct PendingLikeSubmission
        {
            public PendingLikeSubmission(string likeId, string sessionId, string playerGuid, string playerName, string trackId, long createdAtUnixMsUtc)
            {
                LikeId = likeId;
                SessionId = sessionId;
                PlayerGuid = playerGuid;
                PlayerName = playerName;
                TrackId = trackId;
                CreatedAtUnixMsUtc = createdAtUnixMsUtc;
            }

            public string LikeId { get; }
            public string SessionId { get; }
            public string PlayerGuid { get; }
            public string PlayerName { get; }
            public string TrackId { get; }
            public long CreatedAtUnixMsUtc { get; }
        }
    }
}
