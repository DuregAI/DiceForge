using UnityEngine;

namespace Diceforge.View
{
    /// <summary>Presentation only: anticipation, one uninterrupted flight, then contact recovery.</summary>
    public static class DioramaHopMotion
    {
        public static void Sample(float time, out float progress, out float lift, out Vector3 scale, out float lean)
        {
            float t = Mathf.Clamp01(time);
            float compression;
            lean = 0f;
            if (t < .12f)
            {
                progress = 0f;
                lift = 0f;
                compression = -.06f * Mathf.SmoothStep(0f, 1f, t / .12f);
            }
            else if (t < .84f)
            {
                float flight = (t - .12f) / .72f;
                progress = Mathf.SmoothStep(0f, 1f, flight);
                lift = 4f * flight * (1f - flight);
                compression = .045f * Mathf.Sin(flight * Mathf.PI) - .06f * Mathf.Pow(1f - flight, 5f);
                lean = 6f * Mathf.Sin(flight * Mathf.PI);
            }
            else
            {
                progress = 1f;
                lift = 0f;
                compression = -.08f * Mathf.Sin(Mathf.PI * (t - .84f) / .16f);
            }
            scale = new Vector3(1f - compression * .5f, 1f + compression, 1f - compression * .5f);
        }
    }
}
