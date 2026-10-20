using UnityEngine;

namespace Diceforge.View
{
    [CreateAssetMenu(menuName = "Diceforge/Character presentation")]
    public sealed class CharacterPresentationProfile : ScriptableObject
    {
        public string characterId;
        public Material bodyMaterial, eyeMaterial;
        public float idleSpeed = 1, listenSpeed = 1, talkSpeed = 1;
        public float greetSpeed = 1, agreeSpeed = 1, laughSpeed = 1, doubtSpeed = 1;
        public float blendSeconds = .2f;
        public float greetSeconds = 2.8f, agreeSeconds = 1.5f, laughSeconds = 2.5f, doubtSeconds = 2.7f;
        public float blinkMin = 3.1f, blinkMax = 5.6f, gazeAmount = 30, gazeSpeed = 70;
        public float speechFrequency = 15.5f, speechAmount = 62, expressionAmount = 65;

        public float Speed(string state)
        {
            switch (state)
            {
                case "Idle_Scene": case "Idle": return idleSpeed;
                case "Dialogue_Listen": return listenSpeed;
                case "Dialogue_Talk": return talkSpeed;
                case "Dialogue_Greet": return greetSpeed;
                case "Dialogue_Agree": return agreeSpeed;
                case "Dialogue_Laugh": return laughSpeed;
                case "Dialogue_Doubt": return doubtSpeed;
                default: return 1;
            }
        }

        public float GestureSeconds(string gesture)
        {
            switch (gesture)
            {
                case "Greet": return greetSeconds;
                case "Agree": return agreeSeconds;
                case "Laugh": return laughSeconds;
                case "Doubt": return doubtSeconds;
                default: return 0;
            }
        }
    }
}

