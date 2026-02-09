using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Dialogue
{
    [CreateAssetMenu(menuName = "Diceforge/Dialogue/Sequence", fileName = "DialogueSequence")]
    public sealed class DialogueSequence : ScriptableObject
    {
        public List<DialogueLine> lines = new();
    }

    [Serializable]
    public sealed class DialogueLine
    {
        public string speakerId;
        [TextArea(2, 6)] public string text;
        public Sprite portrait;
    }
}
