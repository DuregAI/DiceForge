using UnityEngine;

namespace Diceforge.Dialogue
{
    [CreateAssetMenu(menuName = "Diceforge/Dialogue/Tutorial Portrait Library", fileName = "TutorialPortraitLibrary")]
    public sealed class TutorialPortraitLibrary : ScriptableObject
    {
        [Header("Chief Wombat")]
        [SerializeField] private Sprite chiefWombatNeutral;
        [SerializeField] private Sprite chiefWombatPoint;
        [SerializeField] private Sprite chiefWombatThumbsUp;
        [SerializeField] private Sprite chiefWombatWorried;

        [Header("Scout")]
        [SerializeField] private Sprite scoutWave;
        [SerializeField] private Sprite scoutMap;

        [Header("Critter")]
        [SerializeField] private Sprite critterSad;
        [SerializeField] private Sprite critterHappy;

        public Sprite DefaultPlayerAvatar => chiefWombatNeutral;

        public Sprite GetDefaultBySpeakerId(string speakerId)
        {
            if (string.IsNullOrWhiteSpace(speakerId))
            {
                return DefaultPlayerAvatar;
            }

            switch (speakerId.Trim().ToLowerInvariant())
            {
                case "chiefwombat":
                case "chief_wombat":
                case "chief":
                case "player":
                    return chiefWombatNeutral;
                case "scout":
                    return scoutWave;
                case "critter":
                    return critterHappy;
                case "narrator":
                default:
                    return DefaultPlayerAvatar;
            }
        }

        public Sprite ChiefWombatNeutral => chiefWombatNeutral;
        public Sprite ChiefWombatPoint => chiefWombatPoint;
        public Sprite ChiefWombatThumbsUp => chiefWombatThumbsUp;
        public Sprite ChiefWombatWorried => chiefWombatWorried;
        public Sprite ScoutWave => scoutWave;
        public Sprite ScoutMap => scoutMap;
        public Sprite CritterSad => critterSad;
        public Sprite CritterHappy => critterHappy;
    }
}
