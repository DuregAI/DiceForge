using UnityEngine;

namespace Diceforge.View
{
    [CreateAssetMenu(menuName = "Diceforge/Battle/Presentation Profile")]
    public sealed class BattlePresentationProfile : ScriptableObject
    {
        [Range(0f, 1f)] public float soundGain = 0.75f;
        [Range(0.05f, 0.3f)] public float selectionSeconds = 0.2f;
        [Range(0.05f, 0.3f)] public float hitSeconds = 0.25f;
        [Range(0.05f, 0.3f)] public float exitSeconds = 0.25f;
        [Range(0.05f, 0.6f)] public float victorySeconds = 0.5f;
        public AudioClip selectionClip;
        public AudioClip moveClip;
        public AudioClip hitClip;
        public AudioClip exitClip;
        public AudioClip victoryClip;
        public AudioClip defeatClip;
        public Texture2D countPlaque;
    }
}
