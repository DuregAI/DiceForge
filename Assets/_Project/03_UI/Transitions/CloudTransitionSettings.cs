using UnityEngine;

namespace Diceforge.Transitions
{
    [CreateAssetMenu(menuName = "Glimblehop/UI/Cloud transition settings")]
    public sealed class CloudTransitionSettings : ScriptableObject
    {
        public Texture2D cloudTexture;
        [Header("Timing (unscaled seconds)")]
        [Range(0, 3)] public float closeDuration = 0.8f;
        [Range(0, 3)] public float openDuration = 0.95f;
        [Range(0, 1)] public float coveredHold = 0.12f;
        [Range(0, 0.4f)] public float stagger = 0.15f;
        [Min(1)] public float readinessTimeout = 30;
        [Header("Appearance")]
        public Color cloudTint = Color.white;
        public Color coverageColor = new Color(0.91f, 0.88f, 0.76f);
        [Tooltip("Use a short crossfade instead of large cloud movement.")]
        public bool reducedMotion;
    }
}
