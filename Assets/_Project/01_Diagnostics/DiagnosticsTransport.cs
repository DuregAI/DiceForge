using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Diceforge.Diagnostics
{
    public interface IDiagnosticsTransport
    {
        void SendBatch(IReadOnlyList<AnalyticsEventData> batch);
    }

    public interface IDiagnosticsSessionSummarySink
    {
        void SubmitSessionSummary(SessionSummaryData summary);
    }

    public sealed class DebugLogDiagnosticsTransport : IDiagnosticsTransport
    {
        public void SendBatch(IReadOnlyList<AnalyticsEventData> batch)
        {
            if (batch == null || batch.Count == 0)
                return;

            var sb = new StringBuilder();
            sb.Append("[Diagnostics] Flush batch count=").Append(batch.Count);

            for (int i = 0; i < batch.Count; i++)
            {
                AnalyticsEventData item = batch[i];
                sb.AppendLine();
                sb.Append("  ");
                sb.Append(item.EventType);
                sb.Append(" ts=");
                sb.Append(item.TimestampUnixMsUtc);
                sb.Append(" session=");
                sb.Append(item.SessionId);
                AppendPayload(sb, item.Payload);
            }

            Debug.Log(sb.ToString());
        }

        private static void AppendPayload(StringBuilder sb, Dictionary<string, string> payload)
        {
            if (payload == null || payload.Count == 0)
                return;

            sb.Append(" payload={");

            bool first = true;
            foreach (var pair in payload)
            {
                if (!first)
                    sb.Append(", ");

                sb.Append(pair.Key);
                sb.Append('=');
                sb.Append(pair.Value);
                first = false;
            }

            sb.Append('}');
        }
    }

    internal sealed class AnalyticsEventQueue
    {
        private readonly Queue<AnalyticsEventData> _events = new Queue<AnalyticsEventData>();
        private int _maxCount;

        public AnalyticsEventQueue(int maxCount)
        {
            _maxCount = Mathf.Max(1, maxCount);
        }

        public int Count => _events.Count;

        public void Resize(int maxCount)
        {
            _maxCount = Mathf.Max(1, maxCount);
            TrimToCapacity();
        }

        public void Enqueue(AnalyticsEventData eventData)
        {
            if (eventData == null)
                return;

            TrimToCapacity();
            while (_events.Count >= _maxCount)
                _events.Dequeue();

            _events.Enqueue(eventData);
        }

        public void DrainTo(List<AnalyticsEventData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();

            while (_events.Count > 0)
                target.Add(_events.Dequeue());
        }

        private void TrimToCapacity()
        {
            while (_events.Count > _maxCount)
                _events.Dequeue();
        }
    }
}
