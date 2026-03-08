using UnityEngine;

namespace Diceforge.Diagnostics
{
    public sealed class DiagnosticsOptions
    {
        private const float MinimumSamplingWindowSeconds = 0.5f;
        private const int MinimumQueuedEvents = 1;

        public DiagnosticsOptions(float performanceSamplingWindowSeconds = 3f, int maxQueuedEvents = 128)
        {
            PerformanceSamplingWindowSeconds = Mathf.Max(MinimumSamplingWindowSeconds, performanceSamplingWindowSeconds);
            MaxQueuedEvents = Mathf.Max(MinimumQueuedEvents, maxQueuedEvents);
        }

        public float PerformanceSamplingWindowSeconds { get; private set; }
        public int MaxQueuedEvents { get; private set; }

        public void Apply(DiagnosticsOptions options)
        {
            if (options == null)
                return;

            PerformanceSamplingWindowSeconds = Mathf.Max(MinimumSamplingWindowSeconds, options.PerformanceSamplingWindowSeconds);
            MaxQueuedEvents = Mathf.Max(MinimumQueuedEvents, options.MaxQueuedEvents);
        }
    }
}