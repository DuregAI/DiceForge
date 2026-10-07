using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diceforge.Dialogue
{
    [CreateAssetMenu(menuName = "Diceforge/Demo narrative catalog")]
    public sealed class DemoNarrativeCatalog : ScriptableObject
    {
        public string sourceVersion = "0.1";
        public DemoDialogueEvent[] events = Array.Empty<DemoDialogueEvent>();
        public DemoStoryScene[] scenes = Array.Empty<DemoStoryScene>();
        public DemoSpeaker[] speakers = Array.Empty<DemoSpeaker>();
        public DemoDialogueEvent Find(string id) => Array.Find(events, e => e.id == id);
        public DemoStoryScene Scene(int index) => Array.Find(scenes, s => s.id == "S" + index.ToString("00"));
        public string Speaker(string id, bool russian) => Array.Find(speakers, s => s.id == id)?.Name(russian) ?? string.Empty;
    }

    [Serializable] public sealed class DemoDialogueEvent
    {
        public string id, levelId, speakerId, kind, domainEvent, helpTopic, speakerLabelRu, speakerLabelEn;
        public string ru, en;
        public int priority;
        public string Text(bool russian) => russian || string.IsNullOrWhiteSpace(en) ? ru : en;
    }
    [Serializable] public sealed class DemoSpeaker
    {
        public string id, ru, en;
        public Sprite neutralPortrait;
        public Sprite dialogueProfile;
        public string Name(bool russian) => russian ? ru : en;
    }
    [Serializable] public sealed class DemoStoryScene
    {
        public string id, title, titleEn;
        public Texture2D art;
        public DemoStoryFrame[] frames;
        public string Title(bool russian) => russian || string.IsNullOrWhiteSpace(titleEn) ? title : titleEn;
    }
    [Serializable] public sealed class DemoStoryFrame
    {
        public string id, title, titleEn;
        public DemoStoryLine[] lines;
        public string Title(bool russian) => russian || string.IsNullOrWhiteSpace(titleEn) ? title : titleEn;
    }
    [Serializable] public sealed class DemoStoryLine
    {
        public string id, speakerId, ru, en;
        public string Text(bool russian) => russian || string.IsNullOrWhiteSpace(en) ? ru : en;
    }
}
