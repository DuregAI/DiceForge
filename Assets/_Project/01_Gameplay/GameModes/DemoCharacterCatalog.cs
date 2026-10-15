using System;
using UnityEngine;

namespace Diceforge.GameModes
{
    [CreateAssetMenu(menuName = "Diceforge/Demo Character Catalog")]
    public sealed class DemoCharacterCatalog : ScriptableObject
    {
        public DemoCharacterDefinition[] characters = Array.Empty<DemoCharacterDefinition>();
        public DemoCharacterDefinition Find(string id) => Array.Find(characters, c => c.id == id);
        public static DemoCharacterCatalog Load() => Resources.Load<DemoCharacterCatalog>("DemoRC/Characters");
    }

    [Serializable]
    public sealed class DemoCharacterDefinition
    {
        public string id, nameRu, nameEn;
        public GameObject gameplayPrefab;
        public Sprite dialogueProfile;
        public Color identityColor = Color.white;
        public bool productionApproved;
        [TextArea] public string artNotes;
    }
}
