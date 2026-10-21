using UnityEngine;

namespace Diceforge.View
{
    [CreateAssetMenu(menuName="Diceforge/Living forest presentation")]
    public sealed class LivingForestProfile : ScriptableObject
    {
        [Range(0f,2f)] public float windStrength = 1f;
        [Range(1,30)] public int reflectionFramesPerSecond = 12;
        public int reflectionWidth = 512;
        public int mobileReflectionWidth = 256;
        public Color lanternColor = new Color(1f,.59f,.20f);
        public float lanternIntensity = 1.25f;
    }
}
