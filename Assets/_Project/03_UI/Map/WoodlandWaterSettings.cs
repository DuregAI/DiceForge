using System;
using UnityEngine;

namespace Diceforge.Map
{
    [Serializable]
    public sealed class WoodlandWaterSettings
    {
        [Range(0, 3)] public float intensity = 1;
        [Header("River highlights")]
        [Range(0, 1)] public float riverOpacity = .42f;
        [Range(0, 4)] public float riverSpeed = 1;
        [Range(.25f, 3)] public float riverDensity = 1;
        [Range(.5f, 5)] public float riverLineWidth = 1.5f;
        [Header("Waterfall streaks")]
        [Range(0, 1)] public float waterfallOpacity = .8f;
        [Range(0, 4)] public float waterfallSpeed = 1.15f;
        [Range(.25f, 3)] public float waterfallDensity = 1.8f;
        [Range(.5f, 8)] public float waterfallLineWidth = 3.2f;
        [Range(3, 45)] public float waterfallStreakLength = 21;
        [Header("Foam at waterfall bases")]
        [Range(0, 1)] public float foamOpacity = .8f;
        [Range(0, 4)] public float foamSpeed = 1;
        [Range(1, 10)] public int foamRings = 5;
        [Range(.5f, 8)] public float foamLineWidth = 3;
        [Range(.5f, 1.5f)] public float foamSpread = 1;
    }
}
