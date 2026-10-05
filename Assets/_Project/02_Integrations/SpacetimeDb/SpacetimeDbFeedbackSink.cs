using System;
using SpacetimeDB;
using SpacetimeDB.ClientApi;
using SpacetimeDB.Types;
using UnityEngine;

namespace Diceforge.Integrations.SpacetimeDb
{
    public enum FeedbackSubmissionResult { Sent, Failed, TimedOut, Unavailable, Busy }

    public static class FeedbackDraft
    {
        private const string MessageKey = "feedback.draft.message";
        private const string CategoryKey = "feedback.draft.category";
        public static string Message => PlayerPrefs.GetString(MessageKey, string.Empty);
        public static string Category => PlayerPrefs.GetString(CategoryKey, "bug");
        public static void Save(string category, string message)
        {
            PlayerPrefs.SetString(CategoryKey, category ?? "bug");
            PlayerPrefs.SetString(MessageKey, message ?? string.Empty);
            PlayerPrefs.Save();
        }
        public static string StatusText(FeedbackSubmissionResult result)
        {
            switch (result)
            {
                case FeedbackSubmissionResult.Sent: return "Feedback submitted.";
                case FeedbackSubmissionResult.TimedOut: return "No confirmation received. Delivery is unknown; your draft is saved.";
                case FeedbackSubmissionResult.Busy: return "Another feedback message is being sent. Please try again shortly.";
                default: return "Could not send feedback. Your draft is saved; please try again.";
            }
        }
    }

    public sealed class SpacetimeDbFeedbackSink
    {
        public const int MaxMessageLength = 1000;

        private readonly DbConnection _connection;
        public const float SubmissionTimeoutSeconds = 15f;
        private PendingFeedbackSubmission? _pendingFeedback;
        private Action<FeedbackSubmissionResult> _completion;
        private float _deadline;

        public SpacetimeDbFeedbackSink(DbConnection connection)
        {
            _connection = connection;
        }

        public void SubmitFeedback(string sessionId, string playerGuid, string playerName, string category, string message, string buildVersion, string sceneName, Action<FeedbackSubmissionResult> completion)
        {
            if (_pendingFeedback.HasValue)
            {
                completion?.Invoke(FeedbackSubmissionResult.Busy);
                return;
            }
            string trimmedCategory = Sanitize(category);
            string trimmedMessage = Sanitize(message);
            if (string.IsNullOrWhiteSpace(trimmedCategory) || string.IsNullOrWhiteSpace(trimmedMessage))
            {
                Debug.LogWarning("[SpacetimeDb] Feedback submission ignored because category or message is empty.");
                completion?.Invoke(FeedbackSubmissionResult.Failed);
                return;
            }

            if (trimmedMessage.Length > MaxMessageLength)
            {
                Debug.LogWarning($"[SpacetimeDb] Feedback submission ignored because the message exceeds {MaxMessageLength} characters.");
                completion?.Invoke(FeedbackSubmissionResult.Failed);
                return;
            }

            if (_connection == null || !_connection.IsActive)
            {
                completion?.Invoke(FeedbackSubmissionResult.Unavailable);
                return;
            }

            _completion = completion;
            _deadline = Time.realtimeSinceStartup + SubmissionTimeoutSeconds;
            _pendingFeedback = new PendingFeedbackSubmission(
                Guid.NewGuid().ToString("N"),
                Sanitize(sessionId),
                Sanitize(playerGuid),
                Sanitize(playerName),
                trimmedCategory,
                trimmedMessage,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Sanitize(buildVersion),
                Sanitize(sceneName));

            TrySubmitPendingFeedback();
        }

        public void HandleConnected()
        {
            // Offline drafts belong to the UI, and must not be sent unexpectedly on reconnect.
        }

        public void Tick(float realtime)
        {
            if (_pendingFeedback.HasValue && realtime >= _deadline)
                Complete(FeedbackSubmissionResult.TimedOut);
        }

        public void HandleDisconnected() => Complete(FeedbackSubmissionResult.Unavailable);

        public void RemoveCompletion(Action<FeedbackSubmissionResult> completion)
        {
            if (_completion == completion) _completion = null;
        }

        private void Complete(FeedbackSubmissionResult result)
        {
            if (!_pendingFeedback.HasValue) return;
            _pendingFeedback = null;
            var callback = _completion;
            _completion = null;
            callback?.Invoke(result);
        }

        public void HandleSubmitFeedback(
            ReducerEventContext ctx,
            string feedbackId,
            string sessionId,
            string playerGuid,
            string playerName,
            string category,
            string message,
            long createdAtUnixMsUtc,
            string buildVersion,
            string sceneName)
        {
            if (!_pendingFeedback.HasValue || _pendingFeedback.Value.FeedbackId != feedbackId) return;
            bool committed = ctx.Event.Status is Status.Committed;
            if (!committed) Debug.LogWarning($"[SpacetimeDb] Feedback rejected: {ctx.Event.Status}");
            Complete(committed ? FeedbackSubmissionResult.Sent : FeedbackSubmissionResult.Failed);
        }

        private void TrySubmitPendingFeedback()
        {
            if (!_pendingFeedback.HasValue) return;
            try
            {
                PendingFeedbackSubmission pendingFeedback = _pendingFeedback.Value;

                _connection.Reducers.SubmitFeedback(
                    pendingFeedback.FeedbackId,
                    pendingFeedback.SessionId,
                    pendingFeedback.PlayerGuid,
                    pendingFeedback.PlayerName,
                    pendingFeedback.Category,
                    pendingFeedback.Message,
                    pendingFeedback.CreatedAtUnixMsUtc,
                    pendingFeedback.BuildVersion,
                    pendingFeedback.SceneName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[SpacetimeDb] Feedback send failed: {exception.Message}");
                Complete(FeedbackSubmissionResult.Failed);
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
                string playerGuid,
                string playerName,
                string category,
                string message,
                long createdAtUnixMsUtc,
                string buildVersion,
                string sceneName)
            {
                FeedbackId = feedbackId;
                SessionId = sessionId;
                PlayerGuid = playerGuid;
                PlayerName = playerName;
                Category = category;
                Message = message;
                CreatedAtUnixMsUtc = createdAtUnixMsUtc;
                BuildVersion = buildVersion;
                SceneName = sceneName;
            }

            public string FeedbackId { get; }
            public string SessionId { get; }
            public string PlayerGuid { get; }
            public string PlayerName { get; }
            public string Category { get; }
            public string Message { get; }
            public long CreatedAtUnixMsUtc { get; }
            public string BuildVersion { get; }
            public string SceneName { get; }
        }
    }
}
