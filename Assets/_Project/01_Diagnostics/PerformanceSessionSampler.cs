using UnityEngine;

namespace Diceforge.Diagnostics
{
    internal sealed class PerformanceSessionSampler
    {
        private const float MinimumSamplingWindowSeconds = 0.5f;

        private readonly PerformanceSessionAggregate _aggregate = new PerformanceSessionAggregate();
        private int _windowFrames;
        private float _windowElapsedSeconds;

        public PerformanceSessionSampler(float sampleWindowSeconds)
        {
            Configure(sampleWindowSeconds);
        }

        public void Configure(float sampleWindowSeconds)
        {
            _aggregate.SampleWindowSeconds = Mathf.Max(MinimumSamplingWindowSeconds, sampleWindowSeconds);
            _windowFrames = 0;
            _windowElapsedSeconds = 0f;
        }

        public void Sample(float unscaledDeltaTime)
        {
            if (unscaledDeltaTime <= 0f)
                return;

            _aggregate.TotalFrames++;
            _aggregate.TotalSampledSeconds += unscaledDeltaTime;
            _aggregate.AverageFps = _aggregate.TotalSampledSeconds > 0d
                ? (float)(_aggregate.TotalFrames / _aggregate.TotalSampledSeconds)
                : 0f;
            _aggregate.AverageFrameTimeMs = _aggregate.TotalFrames > 0
                ? (float)((_aggregate.TotalSampledSeconds * 1000d) / _aggregate.TotalFrames)
                : 0f;

            float frameTimeMs = unscaledDeltaTime * 1000f;
            if (frameTimeMs > _aggregate.WorstSpikeMs)
                _aggregate.WorstSpikeMs = frameTimeMs;

            _windowFrames++;
            _windowElapsedSeconds += unscaledDeltaTime;

            if (_windowElapsedSeconds < _aggregate.SampleWindowSeconds)
                return;

            CommitWindow(_windowFrames, _windowElapsedSeconds);
            _windowFrames = 0;
            _windowElapsedSeconds = 0f;
        }

        public PerformanceSessionAggregate CreateSnapshot()
        {
            PerformanceSessionAggregate snapshot = _aggregate.Copy();

            if (_windowFrames > 0 && _windowElapsedSeconds > 0f)
            {
                float windowFps = _windowFrames / _windowElapsedSeconds;
                if (snapshot.MinimumWindowFps <= 0f || windowFps < snapshot.MinimumWindowFps)
                    snapshot.MinimumWindowFps = windowFps;
            }

            return snapshot;
        }

        private void CommitWindow(int windowFrames, float windowElapsedSeconds)
        {
            if (windowFrames <= 0 || windowElapsedSeconds <= 0f)
                return;

            float windowFps = windowFrames / windowElapsedSeconds;
            if (_aggregate.MinimumWindowFps <= 0f || windowFps < _aggregate.MinimumWindowFps)
                _aggregate.MinimumWindowFps = windowFps;
        }
    }
}