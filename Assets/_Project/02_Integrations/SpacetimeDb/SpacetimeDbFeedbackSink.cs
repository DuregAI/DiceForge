using System;
using System.Collections.Generic;
using SpacetimeDB.Types;
using UnityEngine;

namespace Diceforge.Integrations.SpacetimeDb
{
    public sealed class SpacetimeDbFeedbackSink
    {
        public const int MaxMessageLength = 1000;

        private readonly DbConnection _connection;
        private readonly Queue<PendingFeedbackSubmission> _pendingFeedback = new Queue<PendingFeedbackSubmission>(2);

        public SpacetimeDbFeedbackSink(DbConnection connection)
        {
            _connection = connection;
        }

        public void SubmitFeedback(string sessionId, string category, string message, string buildVersion, string sceneName)
        {
            string trimmedCategory = Sanitize(category);
            string trimmedMessage = Sanitize(message);
            if (string.IsNullOrWhiteSpace(trimmedCategory) || string.IsNullOrWhiteSpace(trimmedMessage))
            {
                Debug.LogWarning("[SpacetimeDb] Feedback submission ignored because category or message is empty.");
                return;
            }

            if (trimmedMessage.Length > MaxMessageLength)
            {
                Debug.LogWarning($"[SpacetimeDb] Feedback submission ignored because the message exceeds {MaxMessageLength} characters.");
                return;
            }

            _pendingFeedback.Enqueue(new PendingFeedbackSubmission(
                Guid.NewGuid().ToString("N"),
                Sanitize(sessionId),
                trimmedCategory,
                trimmedMessage,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Sanitize(buildVersion),
                Sanitize(sceneName)));

            TrySubmitPendingFeedback();
        }

        public void HandleConnected()
        {
            TrySubmitPendingFeedback();
        }

        public void HandleSubmitFeedback(
            ReducerEventContext ctx,
            string feedbackId,
            string sessionId,
            string category,
            string message,
            long createdAtUnixMsUtc,
            string buildVersion,
            string sceneName)
        {
            Debug.Log(
                $"[SpacetimeDb] submit_feedback callback feedbackId={feedbackId} session={sessionId} category={category} scene={sceneName} createdAt={createdAtUnixMsUtc} status={ctx.Event.Status}");
        }

        private void TrySubmitPendingFeedback()
        {
            if (_connection == null || !_connection.IsActive)
            {
                if (_pendingFeedback.Count > 0)
                    Debug.Log("[SpacetimeDb] Feedback queued; connection is not active yet.");

                return;
            }

            while (_pendingFeedback.Count > 0)
            {
                PendingFeedbackSubmission pendingFeedback = _pendingFeedback.Dequeue();
                Debug.Log(
                    $"[SpacetimeDb] Submitting feedback_entry feedbackId={pendingFeedback.FeedbackId} session={pendingFeedback.SessionId} category={pendingFeedback.Category} scene={pendingFeedback.SceneName}");

                _connection.Reducers.SubmitFeedback(
                    pendingFeedback.FeedbackId,
                    pendingFeedback.SessionId,
                    pendingFeedback.Category,
                    pendingFeedback.Message,
                    pendingFeedback.CreatedAtUnixMsUtc,
                    pendingFeedback.BuildVersion,
                    pendingFeedback.SceneName);
            }
        }

        private static string Sanitize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private readonly struct PendingFeedbackSubmission
        {
            public PendingFeedbackSubmission(
                string feedbackId,
                string sessionId,
                string category,
                string message,
                long createdAtUnixMsUtc,
                string buildVersion,
                string sceneName)
            {
                FeedbackId = feedbackId;
                SessionId = sessionId;
                Category = category;
                Message = message;
                CreatedAtUnixMsUtc = createdAtUnixMsUtc;
                BuildVersion = buildVersion;
                SceneName = sceneName;
            }

            public string FeedbackId { get; }
            public string SessionId { get; }
            public string Category { get; }
            public string Message { get; }
            public long CreatedAtUnixMsUtc { get; }
            public string BuildVersion { get; }
            public string SceneName { get; }
        }
    }
}
